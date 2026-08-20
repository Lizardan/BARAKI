using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-window authoring: kit list, live preview, collapsible VFX palette.
    /// Open: main toolbar <c>BARAKI Studio</c>, <c>Ctrl+Shift+F</c>, or <c>BARAKI/Abilities/BARAKI Studio</c>.
    /// </summary>
    public sealed class AbilityFxStudioWindow : EditorWindow
    {
        const float DefaultListWidth = 268f;
        const float MinListWidth = 180f;
        const float MaxListWidth = 420f;
        const float DefaultPaletteWidth = 340f;
        const float MinPaletteWidth = 220f;
        const float MaxPaletteWidth = 560f;
        const float SplitterWidth = 6f;
        const float SplitterHitPad = 4f;
        static readonly int SplitterHint = "BARAKI.FxStudio.Splitter".GetHashCode();
        const float MarkerHitPx = 12f;
        const string SelectedIdKey = "BARAKI.AbilityFxStudio.SelectedId";
        const string SelectedIdLegacyKey = "BARAKI.AbilityFxViewer.SelectedId";
        const string ListWidthKey = "BARAKI.AbilityFxStudio.ListWidth";
        const string PaletteWidthKey = "BARAKI.AbilityFxStudio.PaletteWidth";
        const string PaletteCollapsedKey = "BARAKI.AbilityFxStudio.PaletteCollapsed";
        const string DivineBuildingSmitePrefabPath =
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR3 Fire Explosion B.prefab";
        const string DivineUnitSmitePrefabPath =
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab";

        static readonly AbilityVfxAnchor[] MarkerOrder =
        {
            AbilityVfxAnchor.Caster,
            AbilityVfxAnchor.Ground,
            AbilityVfxAnchor.Target,
            AbilityVfxAnchor.Impact,
        };

        static readonly Vector3 EulerHorizontal = Vector3.zero;
        static readonly Vector3 EulerVertical = new(0f, 0f, 90f);
        static readonly Vector3 EulerFloor = new(90f, 0f, 0f);

        Vector2 _listScroll;
        string _search = "";
        int _selectedId;
        float _listWidth = DefaultListWidth;
        float _paletteWidth = DefaultPaletteWidth;
        bool _paletteCollapsed;
        int _dragSplitter;
        bool _dragInvert;
        int _splitterControlId;
        AbilityVfxAnchor _markerArmedAnchor;
        bool _markerArmed;
        Vector2 _mouseDownGui;
        List<AbilityVfxPrefabIndex.Entry> _index;
        AbilityFxPreviewSession _preview;
        AbilityVfxPrefabPalette _palette;
        UnitVisualCatalog _visualCatalog;
        BuildingVisualCatalog _buildingCatalog;
        double _lastTick;
        GUIStyle _overlayLabel;
        GUIStyle _anchorTitleStyle;
        GUIStyle _anchorCaptionStyle;
        GameObject _clipSource;
        List<AbilityAnimClipIndex.Entry> _clips;

        [MenuItem("BARAKI/Abilities/BARAKI Studio %#f", false, 0)]
        public static void Open()
        {
            var window = GetWindow<AbilityFxStudioWindow>("BARAKI Studio");
            window.minSize = new Vector2(1120f, 700f);
            window.Show();
            window.Focus();
        }

        void OnEnable()
        {
            _index = AbilityVfxPrefabIndex.Scan();
            _visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            _buildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingVisualCatalog>(
                BuildingVisualPrefabBuilder.CatalogPath);
            _selectedId = SessionState.GetInt(
                SelectedIdKey,
                SessionState.GetInt(SelectedIdLegacyKey, AbilityIds.Strike));
            _listWidth = SessionState.GetFloat(ListWidthKey, DefaultListWidth);
            _paletteWidth = SessionState.GetFloat(PaletteWidthKey, DefaultPaletteWidth);
            _paletteCollapsed = SessionState.GetBool(PaletteCollapsedKey, false);
            _lastTick = EditorApplication.timeSinceStartup;
            _preview ??= new AbilityFxPreviewSession();
            _palette ??= new AbilityVfxPrefabPalette();
            _palette.Bind(OnPrefabPicked);
            _palette.SetIndex(_index);
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EndSplitterDrag();
            EditorApplication.update -= OnEditorUpdate;
            SessionState.SetInt(SelectedIdKey, _selectedId);
            SessionState.SetFloat(ListWidthKey, _listWidth);
            SessionState.SetFloat(PaletteWidthKey, _paletteWidth);
            SessionState.SetBool(PaletteCollapsedKey, _paletteCollapsed);
            _preview?.Dispose();
            _preview = null;
            _palette?.Dispose();
            _palette = null;
        }

        void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _lastTick);
            _lastTick = now;
            if (dt > 0f)
            {
                _preview?.Tick(dt);
                if (!_paletteCollapsed)
                {
                    _palette?.Tick(dt);
                }
            }

            Repaint();
        }

        void OnGUI()
        {
            _splitterControlId = GUIUtility.GetControlID(SplitterHint, FocusType.Passive);
            TickActiveSplitterDrag();
            DrawToolbar();
            var rows = CollectRows();
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Нет UnitAbilityCatalog. Сначала BARAKI/Abilities/Build Ability Defs.",
                    MessageType.Warning);
                return;
            }

            EnsureSelection(rows);
            var selected = FindRow(rows, _selectedId);
            _palette?.Sync(selected.Fx.VfxPrefab);

            EditorGUILayout.BeginHorizontal();
            DrawList(rows);
            DrawSplitter(1, invert: false);
            DrawVisual(selected);
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Обновить список эффектов", EditorStyles.toolbarButton, GUILayout.Width(200f)))
            {
                _index = AbilityVfxPrefabIndex.Scan();
                _palette?.SetIndex(_index);
                _preview?.Replay();
            }

            var showPalette = !_paletteCollapsed;
            var nextShow = GUILayout.Toggle(showPalette, "Палитра", EditorStyles.toolbarButton, GUILayout.Width(72f));
            if (nextShow != showPalette)
            {
                SetPaletteCollapsed(!nextShow);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"{(_index?.Count ?? 0)} префабов · правки пишутся в AbilityFx и переживают Build Ability Defs",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawList(List<Row> rows)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_listWidth));
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            string lastKit = null;
            var filter = _search?.Trim() ?? "";
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (filter.Length > 0
                    && row.DisplayName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0
                    && AbilityVfxKindRules.KitLabel(row.AbilityId)
                        .IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var kit = AbilityVfxKindRules.KitLabel(row.AbilityId);
                if (kit != lastKit)
                {
                    lastKit = kit;
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(kit, EditorStyles.boldLabel);
                }

                DrawListRow(row);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawListRow(Row row)
        {
            var selected = row.AbilityId == _selectedId;
            var rect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var bg = selected
                    ? new Color(0.24f, 0.42f, 0.72f, 0.45f)
                    : new Color(0f, 0f, 0f, 0.12f);
                EditorGUI.DrawRect(rect, bg);
            }

            var swatch = new Rect(rect.x + 4f, rect.y + 4f, 14f, 14f);
            EditorGUI.DrawRect(swatch, row.Fx.Color.a > 0.01f ? row.Fx.Color : Color.gray);
            var label = new Rect(rect.x + 22f, rect.y, rect.width - 26f, rect.height);
            GUI.Label(label, row.DisplayName, selected ? EditorStyles.whiteLabel : EditorStyles.label);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedId = row.AbilityId;
                SessionState.SetInt(SelectedIdKey, _selectedId);
                _preview?.Replay();
                _palette?.Sync(row.Fx.VfxPrefab);
                Event.current.Use();
                GUI.FocusControl(null);
            }
        }

        void DrawVisual(Row selected)
        {
            EditorGUILayout.BeginHorizontal();
            DrawPreviewColumn(selected);
            DrawPaletteSplitter();
            if (!_paletteCollapsed)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(_paletteWidth));
                _palette?.DrawFilters();
                _palette?.DrawGrid();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawPreviewColumn(Row selected)
        {
            EditorGUILayout.BeginVertical();
            var previewRect = GUILayoutUtility.GetRect(
                16f,
                16f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(320f));
            HandlePreviewInput(previewRect, selected);
            var caster = AbilityFxPreviewCasterRules.Resolve(selected.AbilityId);
            DrawPreview(previewRect, selected, caster);
            EditorGUILayout.LabelField(
                $"Слева: {caster.DisplayName}  ·  Справа: мечник  ·  кольцо = боевой радиус 1:1  ·  ЛКМ орбита, колёсико зум",
                EditorStyles.miniLabel);
            DrawSettings(selected);
            EditorGUILayout.EndVertical();
        }

        void DrawPaletteSplitter()
        {
            var width = _paletteCollapsed ? 18f : SplitterWidth;
            var rect = GUILayoutUtility.GetRect(
                width,
                1f,
                GUILayout.Width(width),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f, 1f));
            var chevron = new Rect(rect.x, rect.y + 10f, rect.width, 22f);
            var symbol = _paletteCollapsed ? "‹" : "›";
            if (_dragSplitter == 0 && GUI.Button(chevron, symbol, EditorStyles.miniButton))
            {
                SetPaletteCollapsed(!_paletteCollapsed);
            }

            if (_paletteCollapsed)
            {
                return;
            }

            var hit = InflateSplitterHit(rect);
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.ResizeHorizontal);
            HandleSplitterMouseDown(hit, 2, invert: true);
        }

        void DrawSplitter(int id, bool invert)
        {
            var rect = GUILayoutUtility.GetRect(
                SplitterWidth,
                1f,
                GUILayout.Width(SplitterWidth),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f, 1f));
            var hit = InflateSplitterHit(rect);
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.ResizeHorizontal);
            HandleSplitterMouseDown(hit, id, invert);
        }

        static Rect InflateSplitterHit(Rect rect) =>
            new(rect.x - SplitterHitPad, rect.y, rect.width + SplitterHitPad * 2f, rect.height);

        void HandleSplitterMouseDown(Rect hit, int id, bool invert)
        {
            var evt = Event.current;
            if (evt.type != EventType.MouseDown || evt.button != 0 || !hit.Contains(evt.mousePosition))
            {
                return;
            }

            _dragSplitter = id;
            _dragInvert = invert;
            GUIUtility.hotControl = _splitterControlId;
            EditorGUIUtility.SetWantsMouseJumping(1);
            evt.Use();
        }

        void TickActiveSplitterDrag()
        {
            if (_dragSplitter == 0)
            {
                return;
            }

            var evt = Event.current;
            if (evt.type == EventType.MouseDrag)
            {
                var delta = _dragInvert ? -evt.delta.x : evt.delta.x;
                if (_dragSplitter == 1)
                {
                    _listWidth = Mathf.Clamp(_listWidth + delta, MinListWidth, MaxListWidth);
                }
                else
                {
                    _paletteWidth = Mathf.Clamp(_paletteWidth + delta, MinPaletteWidth, MaxPaletteWidth);
                }

                GUIUtility.hotControl = _splitterControlId;
                evt.Use();
            }
            else if (evt.rawType == EventType.MouseUp)
            {
                EndSplitterDrag();
                evt.Use();
            }
        }

        void EndSplitterDrag()
        {
            if (_dragSplitter == 0)
            {
                return;
            }

            _dragSplitter = 0;
            if (GUIUtility.hotControl == _splitterControlId)
            {
                GUIUtility.hotControl = 0;
            }

            EditorGUIUtility.SetWantsMouseJumping(0);
            SessionState.SetFloat(ListWidthKey, _listWidth);
            SessionState.SetFloat(PaletteWidthKey, _paletteWidth);
        }

        void SetPaletteCollapsed(bool collapsed)
        {
            _paletteCollapsed = collapsed;
            SessionState.SetBool(PaletteCollapsedKey, collapsed);
            if (collapsed)
            {
                _palette?.ClearThumbs();
            }
        }

        void HandlePreviewInput(Rect previewRect, Row selected)
        {
            if (_dragSplitter != 0)
            {
                return;
            }

            var evt = Event.current;
            if (!previewRect.Contains(evt.mousePosition) || _preview == null)
            {
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                _preview.Zoom(-evt.delta.y);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                _mouseDownGui = evt.mousePosition;
                if (TryHitMarker(previewRect, evt.mousePosition, out var hit)
                    && selected.VfxKind != AbilityVfxKind.Aura)
                {
                    _markerArmed = true;
                    _markerArmedAnchor = hit;
                    evt.Use();
                    return;
                }

                _markerArmed = false;
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                if (_markerArmed)
                {
                    if ((evt.mousePosition - _mouseDownGui).sqrMagnitude > 36f)
                    {
                        _markerArmed = false;
                        _preview.Orbit(evt.delta.x, evt.delta.y);
                    }
                }
                else
                {
                    _preview.Orbit(evt.delta.x, evt.delta.y);
                }

                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0 && _markerArmed)
            {
                if (TryHitMarker(previewRect, evt.mousePosition, out var hit) && hit == _markerArmedAnchor)
                {
                    ApplyAnchor(selected, hit);
                }

                _markerArmed = false;
                evt.Use();
            }
        }

        bool TryHitMarker(Rect previewRect, Vector2 mouse, out AbilityVfxAnchor hit)
        {
            hit = AbilityVfxAnchor.Caster;
            if (_preview == null)
            {
                return false;
            }

            var best = MarkerHitPx * MarkerHitPx;
            var found = false;
            for (var i = 0; i < MarkerOrder.Length; i++)
            {
                var anchor = MarkerOrder[i];
                if (!_preview.TryWorldToGui(_preview.AnchorMarkerWorld(anchor), previewRect, out var gui))
                {
                    continue;
                }

                var dist = (mouse - gui).sqrMagnitude;
                if (dist < best)
                {
                    best = dist;
                    hit = anchor;
                    found = true;
                }
            }

            return found;
        }

        void DrawPreview(Rect previewRect, Row selected, AbilityFxPreviewCaster caster)
        {
            EditorGUI.DrawRect(previewRect, new Color(0.1f, 0.1f, 0.12f, 1f));
            if (_preview == null)
            {
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                _preview.EnsureSize(Mathf.RoundToInt(previewRect.width), Mathf.RoundToInt(previewRect.height));
                _preview.SetCaster(
                    LoadCasterPrefab(caster),
                    caster.Role,
                    caster.HeroSlot,
                    caster.BonusSlot,
                    caster.IsBuilding);
                _preview.SetTarget(
                    LoadTargetPrefab(selected.AbilityId),
                    isBuilding: selected.AbilityId == AbilityIds.MainBuildingSmite);
                PushEffect(selected, selected.Fx);
                var tex = _preview.Target;
                if (tex != null)
                {
                    GUI.DrawTexture(previewRect, tex, ScaleMode.StretchToFill, false);
                }
            }

            DrawOverlayLabel(previewRect, _preview.CasterLabelWorld, caster.DisplayName);
            DrawOverlayLabel(
                previewRect,
                _preview.DummyLabelWorld,
                selected.AbilityId == AbilityIds.MainBuildingSmite ? "Здание" : "Цель");
            DrawMechanicOverlay(previewRect, ResolveMechanic(selected));

            if (selected.VfxKind != AbilityVfxKind.Aura)
            {
                var current = AbilityVfxKindRules.ResolveAnchor(selected.AbilityId, selected.Fx.Anchor);
                for (var i = 0; i < MarkerOrder.Length; i++)
                {
                    DrawAnchorMarker(previewRect, MarkerOrder[i], MarkerOrder[i] == current);
                }
            }
        }

        void DrawAnchorMarker(Rect previewRect, AbilityVfxAnchor anchor, bool selected)
        {
            if (_preview == null || !_preview.TryWorldToGui(_preview.AnchorMarkerWorld(anchor), previewRect, out var gui))
            {
                return;
            }

            var size = selected ? 12f : 8f;
            var rect = new Rect(gui.x - size * 0.5f, gui.y - size * 0.5f, size, size);
            EditorGUI.DrawRect(rect, selected ? new Color(0.35f, 0.78f, 1f, 1f) : new Color(1f, 1f, 1f, 0.82f));
            if (selected)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f),
                    new Color(0.35f, 0.78f, 1f, 0.35f));
            }
        }

        void DrawOverlayLabel(Rect previewRect, Vector3 world, string text)
        {
            if (_preview == null || !_preview.TryWorldToGui(world, previewRect, out var gui))
            {
                return;
            }

            _overlayLabel ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            var content = new GUIContent(text);
            var size = _overlayLabel.CalcSize(content);
            var rect = new Rect(gui.x - size.x * 0.5f - 5f, gui.y - 11f, size.x + 10f, 20f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(rect, content, _overlayLabel);
        }

        void DrawMechanicOverlay(Rect previewRect, AbilityFxMechanic mechanic)
        {
            var text = $"{mechanic.Title}  ·  {mechanic.RadiusLabel}  ·  {mechanic.Motion}";
            var content = new GUIContent(text);
            _overlayLabel ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            var size = _overlayLabel.CalcSize(content);
            var rect = new Rect(
                previewRect.x + 8f,
                previewRect.yMax - 28f,
                size.x + 16f,
                20f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.62f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), mechanic.ChipColor);
            GUI.Label(rect, content, _overlayLabel);
        }

        static AbilityFxMechanic ResolveMechanic(Row row)
        {
            var def = row.Def != null ? row.Def : MainExtraAbilityFxDefs.Get(row.AbilityId);
            return AbilityFxMechanicRules.Resolve(
                row.AbilityId,
                def != null ? def.Radius : 0f,
                def != null ? def.SecondaryRadius : 0f,
                def != null ? def.CastRange : 0f);
        }

        static string ResolveDescription(Row row)
        {
            var def = row.Def != null ? row.Def : MainExtraAbilityFxDefs.Get(row.AbilityId);
            if (def == null)
            {
                return string.Empty;
            }

            var desc = def.Description != null ? def.Description.Trim() : string.Empty;
            if (desc.Length > 0)
            {
                return desc;
            }

            var extra = def.DescribeParams();
            return extra != null ? extra.Trim() : string.Empty;
        }

        GameObject LoadCasterPrefab(AbilityFxPreviewCaster caster)
        {
            if (caster.IsBuilding)
            {
                if (_buildingCatalog != null
                    && _buildingCatalog.TryGetPrefab(GameIds.Buildings.Main, out var building))
                {
                    return building;
                }

                return null;
            }

            if (_visualCatalog != null
                && _visualCatalog.TryGetPrefab(
                    GameIds.Races.Human,
                    caster.Role,
                    caster.HeroSlot,
                    caster.BonusSlot,
                    out var prefab))
            {
                return prefab;
            }

            return null;
        }

        GameObject LoadTargetPrefab(int abilityId)
        {
            if (abilityId == AbilityIds.MainBuildingSmite)
            {
                if (_buildingCatalog != null
                    && _buildingCatalog.TryGetPrefab(GameIds.Buildings.Main, out var building))
                {
                    return building;
                }

                return null;
            }

            return LoadMeleePrefab();
        }

        GameObject LoadMeleePrefab()
        {
            if (_visualCatalog != null
                && _visualCatalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab))
            {
                return prefab;
            }

            return null;
        }

        void DrawSettings(Row row)
        {
            var mechanic = ResolveMechanic(row);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            AbilityFxStudioMechanicUi.DrawSchematic(mechanic);
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(row.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Replay", GUILayout.Width(64f)))
            {
                _preview?.Replay();
            }

            using (new EditorGUI.DisabledScope(row.PingTarget == null))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(44f)))
                {
                    EditorGUIUtility.PingObject(row.PingTarget);
                    Selection.activeObject = row.PingTarget;
                }
            }

            EditorGUILayout.EndHorizontal();
            AbilityFxStudioMechanicUi.DrawBody(
                $"{AbilityVfxKindRules.KitLabel(row.AbilityId)}  ·  {row.Kind}  ·  {row.VfxKind}  ·  id {row.AbilityId}",
                ResolveDescription(row),
                mechanic);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            DrawColorAndScale(row, out var color, out var scale);
            GUILayout.Space(10f);
            var anchor = AbilityVfxKindRules.ResolveAnchor(row.AbilityId, row.Fx.Anchor);
            DrawAnchorBlock(row, ref anchor);
            GUILayout.Space(10f);
            var euler = row.Fx.Euler;
            if (row.VfxKind != AbilityVfxKind.Aura)
            {
                DrawEulerBlock(ref euler);
                GUILayout.Space(10f);
            }

            var fxVisualChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            DrawRangeBlock(row, mechanic, out var rangeValue, out var rangeKind);
            var rangeChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndHorizontal();

            var prefab = row.Fx.VfxPrefab;
            var animState = row.Fx.AnimState;
            var animVariant = row.Fx.AnimVariant;
            var animKind = AbilityAnimRules.ResolveAnim(row.AbilityId, row.Fx.AnimKind, animState);
            EditorGUI.BeginChangeCheck();
            DrawAnimBlock(row, ref animState, ref animVariant, ref animKind);
            var fxChanged = fxVisualChanged || EditorGUI.EndChangeCheck();
            if (fxChanged || rangeChanged)
            {
                var fx = new AbilityFx
                {
                    Color = color,
                    VfxPrefab = prefab,
                    Anchor = anchor,
                    AnimKind = animKind,
                    AnimState = animState,
                    AnimVariant = animVariant,
                    Scale = scale,
                    Euler = euler,
                };
                if (fxChanged)
                {
                    SaveFx(row, fx);
                }

                if (rangeChanged)
                {
                    SaveRange(row, rangeKind, rangeValue);
                }

                PushEffect(row, fx);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawColorAndScale(Row row, out Color color, out float scale)
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(168f), GUILayout.MaxWidth(240f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Вид", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Цвет", GUILayout.Width(44f));
            color = EditorGUILayout.ColorField(
                GUIContent.none,
                row.Fx.Color,
                showEyedropper: true,
                showAlpha: false,
                hdr: false,
                GUILayout.Height(18f));
            color.a = 1f;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Масштаб", GUILayout.Width(58f));
            scale = EditorGUILayout.Slider(AbilityFx.ResolveScale(row.Fx.Scale), 0.25f, 8f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        void DrawAnchorBlock(Row row, ref AbilityVfxAnchor anchor)
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Где", EditorStyles.miniBoldLabel);
            if (row.VfxKind == AbilityVfxKind.Aura)
            {
                EditorGUILayout.LabelField("всегда на носителе", EditorStyles.miniLabel);
                anchor = AbilityVfxAnchor.Caster;
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (DrawChoiceCell("На себе", "едет с моделью", anchor == AbilityVfxAnchor.Caster))
                {
                    anchor = AbilityVfxAnchor.Caster;
                }

                if (DrawChoiceCell("На цели", "едет с моделью", anchor == AbilityVfxAnchor.Target))
                {
                    anchor = AbilityVfxAnchor.Target;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (DrawChoiceCell("Под собой", "остаётся на земле", anchor == AbilityVfxAnchor.Ground))
                {
                    anchor = AbilityVfxAnchor.Ground;
                }

                if (DrawChoiceCell("Под целью", "остаётся на земле", anchor == AbilityVfxAnchor.Impact))
                {
                    anchor = AbilityVfxAnchor.Impact;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        bool DrawChoiceCell(string title, string caption, bool active)
        {
            _anchorTitleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
            };
            _anchorCaptionStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
            };

            var prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            var rect = GUILayoutUtility.GetRect(96f, 36f, GUILayout.MinWidth(96f), GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 16f), title, _anchorTitleStyle);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 17f, rect.width - 4f, 16f), caption, _anchorCaptionStyle);
            GUI.backgroundColor = prev;
            if (clicked)
            {
                GUI.changed = true;
            }

            return clicked;
        }

        void DrawEulerBlock(ref Vector3 euler)
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(188f), GUILayout.MaxWidth(280f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Поворот", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (DrawPresetButton("Горизонталь", Approximately(euler, EulerHorizontal)))
            {
                euler = EulerHorizontal;
            }

            if (DrawPresetButton("Вертикаль", Approximately(euler, EulerVertical)))
            {
                euler = EulerVertical;
            }

            if (DrawPresetButton("В пол", Approximately(euler, EulerFloor)))
            {
                euler = EulerFloor;
            }

            EditorGUILayout.EndHorizontal();
            euler = EditorGUILayout.Vector3Field(GUIContent.none, euler);
            EditorGUILayout.EndVertical();
        }

        void DrawRangeBlock(Row row, AbilityFxMechanic mechanic, out float value, out RangeField kind)
        {
            kind = ResolveRangeField(row, mechanic);
            value = ReadRangeValue(row, mechanic, kind);
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(200f), GUILayout.ExpandWidth(true));
            var title = kind == RangeField.CastRange ? "Досягаемость" : "Радиус";
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(kind == RangeField.None || row.Def == null))
            {
                if (kind == RangeField.None)
                {
                    EditorGUILayout.LabelField("без круга — точка на юните", EditorStyles.miniLabel);
                    value = 0f;
                }
                else
                {
                    value = EditorGUILayout.Slider(value, 0.5f, 16f);
                    var caption = kind == RangeField.CastRange
                        ? "метры поиска цели, не AoE"
                        : "боевые метры · круг в превью 1:1";
                    EditorGUILayout.LabelField($"{caption}  ·  {value:0.#} м", EditorStyles.miniLabel);
                }
            }

            if (row.Def == null && kind != RangeField.None)
            {
                EditorGUILayout.LabelField("Divine Blessing: радиус не в этом ассете", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        enum RangeField
        {
            None = 0,
            Radius = 1,
            CastRange = 2,
        }

        static RangeField ResolveRangeField(Row row, AbilityFxMechanic mechanic)
        {
            if (mechanic.ShowRing)
            {
                return RangeField.Radius;
            }

            if (row.AbilityId is AbilityIds.CasterHeal or AbilityIds.Resurrect)
            {
                return RangeField.CastRange;
            }

            if (mechanic.Reach > 0.01f)
            {
                return RangeField.Radius;
            }

            return RangeField.None;
        }

        static float ReadRangeValue(Row row, AbilityFxMechanic mechanic, RangeField kind)
        {
            if (kind == RangeField.CastRange)
            {
                var authored = row.Def != null ? row.Def.CastRange : 0f;
                return authored > 0.01f ? authored : mechanic.Reach;
            }

            if (kind == RangeField.Radius)
            {
                var authored = row.Def != null ? row.Def.Radius : 0f;
                var fallback = mechanic.ShowRing ? mechanic.Radius : mechanic.Reach;
                return authored > 0.01f ? authored : fallback;
            }

            return 0f;
        }

        static void SaveRange(Row row, RangeField kind, float value)
        {
            if (row.Def == null || kind == RangeField.None)
            {
                return;
            }

            if (kind == RangeField.CastRange)
            {
                Undo.RecordObject(row.Def, "Ability Cast Range");
                row.Def.ApplyCastRange(value);
            }
            else
            {
                Undo.RecordObject(row.Def, "Ability Radius");
                row.Def.ApplyRadius(value);
            }

            EditorUtility.SetDirty(row.Def);
        }

        static bool DrawPresetButton(string label, bool active)
        {
            var prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            var clicked = GUILayout.Button(label, EditorStyles.miniButton);
            GUI.backgroundColor = prev;
            if (clicked)
            {
                GUI.changed = true;
            }

            return clicked;
        }

        static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.01f;

        void DrawAnimBlock(Row row, ref string animState, ref int animVariant, ref AbilityAnimKind animKind)
        {
            var caster = AbilityFxPreviewCasterRules.Resolve(row.AbilityId);
            var clips = ClipsFor(LoadCasterPrefab(caster));
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Анимация", EditorStyles.miniBoldLabel);
            if (clips.Count == 0)
            {
                EditorGUILayout.LabelField("нет клипов", EditorStyles.miniLabel);
                return;
            }

            var selectedClip = AbilityAnimClipIndex.IndexOf(clips, animState, animVariant);
            var displayClip = selectedClip;
            if (displayClip < 0)
            {
                displayClip = AbilityAnimClipIndex.IndexOfState(
                    clips,
                    AbilityAnimClipIndex.DefaultState(animKind));
                if (displayClip < 0)
                {
                    displayClip = 0;
                }
            }

            var columns = clips.Count <= 8 ? Mathf.Max(1, clips.Count) : 6;
            for (var i = 0; i < clips.Count; i++)
            {
                if (i % columns == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                }

                var clip = clips[i];
                var caption = !string.IsNullOrEmpty(clip.ClipName) && clip.ClipName != clip.StateName
                    ? clip.ClipName
                    : clip.Variant >= 0 ? $"вариант {clip.Variant + 1}" : "клип";
                if (DrawChoiceCell(clip.StateName, caption, i == displayClip))
                {
                    animState = clip.StateName;
                    animVariant = clip.Variant < 0 ? 0 : clip.Variant;
                    animKind = AbilityAnimRules.ResolveKindFromState(animState);
                    if (animKind == AbilityAnimKind.Unspecified)
                    {
                        animKind = AbilityAnimKind.None;
                    }
                }

                if (i % columns == columns - 1 || i == clips.Count - 1)
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        void ApplyAnchor(Row row, AbilityVfxAnchor anchor)
        {
            var fx = row.Fx;
            fx.Anchor = anchor;
            SaveFx(row, fx);
            PushEffect(row, fx);
        }

        void OnPrefabPicked(GameObject picked)
        {
            if (_preview == null)
            {
                return;
            }

            var current = FindRow(CollectRows(), _selectedId);
            var fx = current.Fx;
            fx.VfxPrefab = picked;
            SaveFx(current, fx);
            PushEffect(current, fx);
            Repaint();
        }

        List<AbilityAnimClipIndex.Entry> ClipsFor(GameObject prefab)
        {
            if (prefab == _clipSource && _clips != null)
            {
                return _clips;
            }

            _clipSource = prefab;
            _clips = AbilityAnimClipIndex.Collect(prefab);
            return _clips;
        }

        void EnsureSelection(List<Row> rows)
        {
            if (FindRowIndex(rows, _selectedId) >= 0)
            {
                return;
            }

            _selectedId = rows[0].AbilityId;
        }

        static int FindRowIndex(List<Row> rows, int abilityId)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].AbilityId == abilityId)
                {
                    return i;
                }
            }

            return -1;
        }

        static Row FindRow(List<Row> rows, int abilityId)
        {
            var index = FindRowIndex(rows, abilityId);
            return rows[index >= 0 ? index : 0];
        }

        static List<Row> CollectRows()
        {
            var rows = new List<Row>();
            var catalog = AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(ContentAssetPaths.UnitAbilityCatalog);
            if (catalog != null)
            {
                foreach (var def in catalog.Abilities)
                {
                    if (def != null)
                    {
                        rows.Add(Row.FromDef(def));
                    }
                }
            }

            rows.Add(Row.FromMainExtra(AbilityIds.MainBuildingSmite));
            rows.Add(Row.FromMainExtra(AbilityIds.MainUnitSmite));
            return rows;
        }

        static void SaveFx(Row row, AbilityFx fx)
        {
            if (row.Def != null)
            {
                Undo.RecordObject(row.Def, "Ability FX");
                row.Def.ApplyFx(fx);
                EditorUtility.SetDirty(row.Def);
                return;
            }

            var catalog = LoadOrCreateMainExtraCatalog();
            Undo.RecordObject(catalog, "Main Extra Ability FX");
            catalog.EditorSetFx(row.AbilityId, fx);
            EditorUtility.SetDirty(catalog);
        }

        void PushEffect(Row selected, AbilityFx fx)
        {
            if (_preview == null)
            {
                return;
            }

            var extra = selected.Def == null ? MainExtraAbilityFxDefs.Get(selected.AbilityId) : null;
            var radius = selected.Def != null
                ? selected.Def.Radius
                : extra != null ? extra.Radius : 0f;
            var stun = selected.Def != null
                ? selected.Def.StunSeconds
                : extra != null ? extra.StunSeconds : 0f;
            _preview.SetEffect(
                fx.VfxPrefab,
                fx.Color,
                selected.VfxKind,
                AbilityVfxKindRules.ResolveAnchor(selected.AbilityId, fx.Anchor),
                selected.AbilityId,
                AbilityAnimRules.ResolveAnim(selected.AbilityId, fx.AnimKind, fx.AnimState),
                radius,
                stun,
                fx.AnimState,
                fx.AnimVariant,
                fx.Scale,
                fx.Euler);
        }

        static MainExtraAbilityFxCatalog LoadOrCreateMainExtraCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MainExtraAbilityFxCatalog>(
                MainExtraAbilityFxCatalog.AssetPath);
            if (catalog != null)
            {
                SeedMainExtraIfMissing(catalog);
                return catalog;
            }

            ContentAssetPaths.EnsureFolder("Assets/Game/Resources/Fx");
            catalog = CreateInstance<MainExtraAbilityFxCatalog>();
            catalog.EditorSetFx(
                AbilityIds.MainBuildingSmite,
                new AbilityFx
                {
                    Color = AbilityFxColors.DivineSmite,
                    VfxPrefab = LoadSeedPrefab(DivineBuildingSmitePrefabPath),
                    Anchor = AbilityVfxAnchor.Impact,
                });
            catalog.EditorSetFx(
                AbilityIds.MainUnitSmite,
                new AbilityFx
                {
                    Color = AbilityFxColors.DivineSmite,
                    VfxPrefab = LoadSeedPrefab(DivineUnitSmitePrefabPath),
                    Anchor = AbilityVfxAnchor.Target,
                });
            AssetDatabase.CreateAsset(catalog, MainExtraAbilityFxCatalog.AssetPath);
            return catalog;
        }

        static void SeedMainExtraIfMissing(MainExtraAbilityFxCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            var dirty = false;
            var building = catalog.GetFx(AbilityIds.MainBuildingSmite);
            if (building.VfxPrefab == null)
            {
                catalog.EditorSetFx(
                    AbilityIds.MainBuildingSmite,
                    new AbilityFx
                    {
                        Color = building.Color.a > 0.01f ? building.Color : AbilityFxColors.DivineSmite,
                        VfxPrefab = LoadSeedPrefab(DivineBuildingSmitePrefabPath),
                        Anchor = building.Anchor != AbilityVfxAnchor.Unspecified
                            ? building.Anchor
                            : AbilityVfxAnchor.Impact,
                    });
                dirty = true;
            }

            var unit = catalog.GetFx(AbilityIds.MainUnitSmite);
            if (unit.VfxPrefab == null)
            {
                catalog.EditorSetFx(
                    AbilityIds.MainUnitSmite,
                    new AbilityFx
                    {
                        Color = unit.Color.a > 0.01f ? unit.Color : AbilityFxColors.DivineSmite,
                        VfxPrefab = LoadSeedPrefab(DivineUnitSmitePrefabPath),
                        Anchor = unit.Anchor != AbilityVfxAnchor.Unspecified
                            ? unit.Anchor
                            : AbilityVfxAnchor.Target,
                    });
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(catalog);
            }
        }

        static GameObject LoadSeedPrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path);

        readonly struct Row
        {
            public Row(
                int abilityId,
                string displayName,
                AbilityKind kind,
                AbilityVfxKind vfxKind,
                AbilityFx fx,
                UnitAbilityDef def,
                Object pingTarget)
            {
                AbilityId = abilityId;
                DisplayName = displayName;
                Kind = kind;
                VfxKind = vfxKind;
                Fx = fx;
                Def = def;
                PingTarget = pingTarget;
            }

            public int AbilityId { get; }
            public string DisplayName { get; }
            public AbilityKind Kind { get; }
            public AbilityVfxKind VfxKind { get; }
            public AbilityFx Fx { get; }
            public UnitAbilityDef Def { get; }
            public Object PingTarget { get; }

            public static Row FromDef(UnitAbilityDef def) => new(
                def.AbilityId,
                def.DisplayName,
                def.Kind,
                AbilityVfxKindRules.Resolve(def.AbilityId),
                def.Fx,
                def,
                def);

            public static Row FromMainExtra(int abilityId)
            {
                var def = MainExtraAbilityFxDefs.Get(abilityId);
                var catalog = LoadOrCreateMainExtraCatalog();
                var fx = catalog != null ? catalog.GetFx(abilityId) : def != null ? def.Fx : default;
                return new Row(
                    abilityId,
                    def != null ? def.DisplayName : $"Main extra {abilityId}",
                    AbilityKind.Active,
                    AbilityVfxKindRules.Resolve(abilityId),
                    fx,
                    null,
                    catalog);
            }
        }
    }
}
