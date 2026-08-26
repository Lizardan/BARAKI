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
        const float MaxPaletteWidth = 1100f;
        const float SplitterWidth = 6f;
        const float SplitterHitPad = 8f;
        static readonly int SplitterHint = "BARAKI.FxStudio.Splitter".GetHashCode();
        const float MarkerHitPx = 12f;
        const string SelectedIdKey = "BARAKI.AbilityFxStudio.SelectedId";
        const string SelectedIdLegacyKey = "BARAKI.AbilityFxViewer.SelectedId";
        const string ListWidthKey = "BARAKI.AbilityFxStudio.ListWidth";
        const string PaletteWidthKey = "BARAKI.AbilityFxStudio.PaletteWidth";
        const string PaletteCollapsedKey = "BARAKI.AbilityFxStudio.PaletteCollapsed";
        const string DivineSkyBeamPrefabPath = ContentAssetPaths.SkyBeamPrefab;
        const string LegacyDivineBuildingSmitePrefabName = "CFXR3 Fire Explosion B";
        const string LegacyDivineUnitSmitePrefabName = "CFXR Hit A (Red)";
        // MAIN-001 seeds: ice burst for the ring, light burst for the wave.
        const string IceRingSeedPrefabPath =
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Ice/CFXR3 Hit Ice B (Air).prefab";
        const string WaveOfLightSeedPrefabPath =
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 Hit Light B (Air).prefab";

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
        float _dragStartMouseX;
        float _dragStartWidth;
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
        GUIStyle _barLabelStyle;
        GUIStyle _barStatusStyle;
        GUIStyle _nameHeaderStyle;
        GUIStyle _cardTitleStyle;
        GUIStyle _kitStyle;
        GUIStyle _fieldLabelStyle;
        GUIStyle _hintStyle;
        GUIStyle _sectionTitleStyle;
        GUIStyle _sectionHintStyle;
        GUIStyle _presetButtonStyle;
        GUIStyle _fillHintStyle;
        GUIStyle _cardBoxStyle;
        GUIStyle _presetCaptionStyle;
        GUIStyle _animHeaderStyle;
        GUIStyle _animClipButtonStyle;
        GUIStyle _animClipTitleStyle;
        GUIStyle _animClipCaptionStyle;
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
            _paletteWidth = Mathf.Clamp(
                SessionState.GetFloat(PaletteWidthKey, DefaultPaletteWidth),
                MinPaletteWidth,
                MaxPaletteWidth);
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
            DrawSplitter(1);
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
                EditorGUILayout.BeginVertical(
                    GUILayout.Width(_paletteWidth),
                    GUILayout.MinWidth(_paletteWidth),
                    GUILayout.MaxWidth(_paletteWidth));
                _palette?.DrawFilters();
                _palette?.DrawGrid();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawPreviewColumn(Row selected)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            var previewRect = GUILayoutUtility.GetRect(
                16f,
                16f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(320f));
            HandlePreviewInput(previewRect, selected);
            var caster = AbilityFxPreviewCasterRules.Resolve(selected.AbilityId);
            DrawPreview(previewRect, selected, caster);
            DrawTransportBar(selected);
            var prevCaption = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            EditorGUILayout.LabelField(
                $"Слева: {caster.DisplayName}  ·  Справа: мечник  ·  кольцо = боевой радиус 1:1  ·  ЛКМ орбита, колёсико зум",
                EditorStyles.miniLabel);
            GUI.color = prevCaption;
            DrawSettings(selected);
            EditorGUILayout.EndVertical();
        }

        void DrawTransportBar(Row selected)
        {
            const float height = 28f;
            var rect = GUILayoutUtility.GetRect(
                10f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.16f, 1f));
                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                    new Color(1f, 1f, 1f, 0.07f));
            }

            var y = rect.y + 4f;
            var innerH = height - 8f;
            var x = rect.x + 6f;

            if (TransportButton(
                    new Rect(x, y, 88f, innerH),
                    "Ещё раз",
                    "PlayButton",
                    "Проиграть эффект заново"))
            {
                _preview?.Replay();
            }

            x += 92f;
            var hasEffect = _preview != null && _preview.HasEffect;
            using (new EditorGUI.DisabledScope(!hasEffect))
            {
                var paused = _preview != null && _preview.IsPaused;
                if (TransportButton(
                        new Rect(x, y, 80f, innerH),
                        paused ? "Дальше" : "Пауза",
                        paused ? "PlayButton" : "PauseButton",
                        paused ? "Продолжить воспроизведение" : "Заморозить кадр, орбита и зум работают"))
                {
                    _preview?.SetPaused(!paused);
                }

                x += 84f;
                if (TransportButton(
                        new Rect(x, y, 68f, innerH),
                        "Стоп",
                        "PreMatQuad",
                        "Убрать эффект из сцены — кольцо радиуса останется; смена визуала или «Ещё раз» вернут эффект"))
                {
                    _preview?.StopEffect();
                }

                x += 72f;
            }

            if (TransportButton(
                    new Rect(x, y, 68f, innerH),
                    "Кадр",
                    "SceneViewCamera",
                    "Вернуть камеру к авто-кадрированию по радиусу"))
            {
                _preview?.FocusCamera();
            }

            x += 72f;
            DrawTransportProgress(rect, x, y, innerH, selected);
        }

        void DrawTransportProgress(Rect bar, float x, float y, float height, Row selected)
        {
            const float statusWidth = 210f;
            var right = bar.xMax - 6f;
            var statusRect = new Rect(right - statusWidth, y, statusWidth, height);
            var progressRect = new Rect(
                x,
                y + 4f,
                Mathf.Max(48f, statusRect.x - 10f - x),
                height - 8f);

            var preview = _preview;
            var looping = preview != null && preview.IsLooping;
            var stopped = preview == null || preview.IsStopped;
            var noEffect = preview == null || !preview.HasEffect;
            var paused = preview != null && preview.IsPaused;

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(progressRect, new Color(0f, 0f, 0f, 0.35f));
                var p = stopped || noEffect ? 0f : looping ? 1f : preview.PlaybackProgress01;
                if (p > 0.001f)
                {
                    var fill = progressRect;
                    fill.width = Mathf.Max(progressRect.width * p, 2f);
                    var color = paused
                        ? new Color(0.95f, 0.75f, 0.3f, 0.9f)
                        : looping
                            ? new Color(0.45f, 0.72f, 1f, 0.4f)
                            : new Color(0.45f, 0.72f, 1f, 0.9f);
                    EditorGUI.DrawRect(fill, color);
                }
            }

            _barLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = new Color(1f, 1f, 1f, 0.85f) },
            };
            var timeText = stopped
                ? "остановлено"
                : noEffect
                    ? "нет эффекта"
                    : looping
                        ? "∞ аура"
                        : paused
                            ? $"{preview.PlaybackElapsed:0.00} / {preview.PlaybackDuration:0.00} с · пауза"
                            : $"{preview.PlaybackElapsed:0.00} / {preview.PlaybackDuration:0.00} с";
            GUI.Label(progressRect, timeText, _barLabelStyle);

            _barStatusStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 10,
                clipping = TextClipping.Clip,
            };
            string status;
            Color statusColor;
            if (selected.Fx.VfxPrefab == null && selected.VfxKind != AbilityVfxKind.Aura)
            {
                status = "нет префаба — выбери в палитре →";
                statusColor = new Color(1f, 0.62f, 0.45f, 1f);
            }
            else if (stopped)
            {
                status = "эффект убран · «Ещё раз» вернёт";
                statusColor = new Color(1f, 1f, 1f, 0.55f);
            }
            else if (selected.Fx.VfxPrefab != null)
            {
                status = selected.Fx.VfxPrefab.name;
                statusColor = new Color(0.6f, 0.85f, 0.6f, 1f);
            }
            else
            {
                status = "аура из MatchFxCatalog";
                statusColor = new Color(1f, 1f, 1f, 0.55f);
            }

            var prev = GUI.color;
            GUI.color = statusColor;
            GUI.Label(statusRect, new GUIContent(status, status), _barStatusStyle);
            GUI.color = prev;
        }

        static bool TransportButton(Rect rect, string label, string icon, string tooltip)
        {
            var content = new GUIContent(label, tooltip);
            var iconName = EditorGUIUtility.isProSkin ? "d_" + icon : icon;
            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                content = new GUIContent(" " + label, iconContent.image, tooltip);
            }

            return GUI.Button(rect, content, EditorStyles.miniButton);
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
            HandleSplitterMouseDown(hit, 2);
        }

        void DrawSplitter(int id)
        {
            var rect = GUILayoutUtility.GetRect(
                SplitterWidth,
                1f,
                GUILayout.Width(SplitterWidth),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f, 1f));
            var hit = InflateSplitterHit(rect);
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.ResizeHorizontal);
            HandleSplitterMouseDown(hit, id);
        }

        static Rect InflateSplitterHit(Rect rect) =>
            new(rect.x - SplitterHitPad, rect.y, rect.width + SplitterHitPad * 2f, rect.height);

        void HandleSplitterMouseDown(Rect hit, int id)
        {
            var evt = Event.current;
            if (evt.type != EventType.MouseDown || evt.button != 0 || !hit.Contains(evt.mousePosition))
            {
                return;
            }

            _dragSplitter = id;
            _dragStartMouseX = evt.mousePosition.x;
            _dragStartWidth = id == 1 ? _listWidth : _paletteWidth;
            GUIUtility.hotControl = _splitterControlId;
            evt.Use();
        }

        void TickActiveSplitterDrag()
        {
            if (_dragSplitter == 0)
            {
                return;
            }

            var evt = Event.current;
            if (evt.rawType is EventType.MouseUp or EventType.Ignore)
            {
                EndSplitterDrag();
                if (evt.rawType == EventType.MouseUp)
                {
                    evt.Use();
                }

                return;
            }

            if (evt.type != EventType.MouseDrag)
            {
                return;
            }

            ApplySplitterFromMouse(evt.mousePosition.x);
            GUIUtility.hotControl = _splitterControlId;
            evt.Use();
        }

        void ApplySplitterFromMouse(float mouseX)
        {
            var dx = mouseX - _dragStartMouseX;
            if (_dragSplitter == 1)
            {
                _listWidth = Mathf.Clamp(_dragStartWidth + dx, MinListWidth, MaxListWidth);
                return;
            }

            _paletteWidth = Mathf.Clamp(_dragStartWidth - dx, MinPaletteWidth, PaletteDragMax());
        }

        float PaletteDragMax() =>
            Mathf.Min(
                MaxPaletteWidth,
                Mathf.Max(MinPaletteWidth, position.width - _listWidth - MinListWidth - 48f));

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
                    caster.Hidden ? null : LoadCasterPrefab(caster),
                    caster.Role,
                    caster.HeroSlot,
                    caster.BonusSlot,
                    caster.IsBuilding,
                    hideCaster: caster.Hidden);
                _preview.SetTarget(
                    LoadTargetPrefab(selected.AbilityId),
                    AbilityFxPreviewTargetRules.Resolve(selected.AbilityId).IsBuilding);
                PushEffect(selected, selected.Fx);
                var tex = _preview.Target;
                if (tex != null)
                {
                    GUI.DrawTexture(previewRect, tex, ScaleMode.StretchToFill, false);
                }
            }

            if (!caster.Hidden)
            {
                DrawOverlayLabel(previewRect, _preview.CasterLabelWorld, caster.DisplayName);
            }
            DrawOverlayLabel(
                previewRect,
                _preview.DummyLabelWorld,
                AbilityFxPreviewTargetRules.Resolve(selected.AbilityId).DisplayName);
            DrawMechanicOverlay(previewRect, ResolveMechanic(selected));

            if (selected.VfxKind != AbilityVfxKind.Aura)
            {
                var current = AbilityVfxKindRules.ResolveAnchor(selected.AbilityId, selected.Fx.Anchor);
                for (var i = 0; i < MarkerOrder.Length; i++)
                {
                    if (caster.Hidden && MarkerOrder[i] == AbilityVfxAnchor.Caster)
                    {
                        continue;
                    }

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
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (_preview == null || !_preview.TryWorldToGui(world, previewRect, out var gui))
            {
                return;
            }

            _overlayLabel ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
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
            _overlayLabel ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
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
            if (row.Def == null)
            {
                var blessing = MainExtraAbilityFxDefs.GetEffectDescription(row.AbilityId);
                if (!string.IsNullOrEmpty(blessing))
                {
                    return blessing;
                }
            }

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
            var target = AbilityFxPreviewTargetRules.Resolve(abilityId);
            if (target.IsBuilding)
            {
                if (_buildingCatalog != null
                    && _buildingCatalog.TryGetPrefab(target.BuildingId, out var building))
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
            var anchor = AbilityVfxKindRules.ResolveAnchor(row.AbilityId, row.Fx.Anchor);
            var euler = row.Fx.Euler;
            var cardW = ResolveSettingsCardWidth();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal(GUILayout.Height(SettingsCardHeight), GUILayout.ExpandWidth(true));
            DrawInfoCard(row, mechanic, cardW);
            GUILayout.Space(SettingsCardGap);
            DrawVisualCard(
                row,
                mechanic,
                cardW,
                out var color,
                out var scale,
                out var rangeValue,
                out var rangeKind,
                out var rangeChanged);
            GUILayout.Space(SettingsCardGap);
            DrawAnchorCard(row, ref anchor, cardW);
            GUILayout.Space(SettingsCardGap);
            DrawEulerCard(row, ref euler, cardW);
            EditorGUILayout.EndHorizontal();

            var prefab = row.Fx.VfxPrefab;
            var animState = row.Fx.AnimState;
            var animVariant = row.Fx.AnimVariant;
            var animKind = AbilityAnimRules.ResolveAnim(row.AbilityId, row.Fx.AnimKind, animState);
            DrawAnimBlock(row, ref animState, ref animVariant, ref animKind);
            var fxChanged = EditorGUI.EndChangeCheck();
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
        }

        const float SettingsCardHeight = 228f;
        const float SettingsCardGap = 6f;
        const float FieldLabelWidth = 96f;
        const float AnchorCellHeight = 46f;
        const float PresetButtonHeight = 26f;
        const float AnimCellHeight = 42f;

        float ResolveSettingsCardWidth()
        {
            var paletteSplitter = _paletteCollapsed ? 18f : SplitterWidth;
            var palette = _paletteCollapsed ? 0f : _paletteWidth;
            var row = position.width - _listWidth - SplitterWidth - paletteSplitter - palette - 8f;
            return Mathf.Max(1f, (row - SettingsCardGap * 3f) / 4f);
        }

        void OpenCard(float width)
        {
            EnsureCardStyles();
            EditorGUILayout.BeginVertical(
                _cardBoxStyle,
                GUILayout.Width(width),
                GUILayout.MaxWidth(width),
                GUILayout.MinWidth(0f),
                GUILayout.Height(SettingsCardHeight),
                GUILayout.MinHeight(SettingsCardHeight),
                GUILayout.MaxHeight(SettingsCardHeight),
                GUILayout.ExpandWidth(false),
                GUILayout.ExpandHeight(false));
        }

        static void CloseCard() => EditorGUILayout.EndVertical();

        void DrawInfoCard(Row row, AbilityFxMechanic mechanic, float width)
        {
            OpenCard(width);
            EditorGUILayout.BeginHorizontal();
            var schematicH = 148f;
            var schematicW = Mathf.Clamp(width * 0.42f, 96f, 148f);
            AbilityFxStudioMechanicUi.DrawSchematic(mechanic, schematicH, schematicW);
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(row.DisplayName, _nameHeaderStyle);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(row.PingTarget == null))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(56f), GUILayout.Height(22f)))
                {
                    EditorGUIUtility.PingObject(row.PingTarget);
                    Selection.activeObject = row.PingTarget;
                }
            }

            EditorGUILayout.EndHorizontal();
            var kit = AbilityVfxKindRules.KitLabel(row.AbilityId);
            if (!string.IsNullOrEmpty(kit))
            {
                EditorGUILayout.LabelField(kit, _kitStyle);
            }

            EditorGUILayout.Space(4f);
            AbilityFxStudioMechanicUi.DrawMechanicLine(mechanic);
            EditorGUILayout.Space(4f);
            AbilityFxStudioMechanicUi.DrawDescription(ResolveDescription(row));
            DrawFillHint(mechanic.FxHint);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            CloseCard();
        }

        void BeginCard(string title, float width)
        {
            OpenCard(width);
            EditorGUILayout.LabelField(title, _cardTitleStyle, GUILayout.MinWidth(0f));
        }

        void EnsureCardStyles()
        {
            _nameHeaderStyle ??= new GUIStyle(EditorStyles.label) { fontSize = 16 };
            _cardTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                clipping = TextClipping.Clip,
            };
            _kitStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                clipping = TextClipping.Clip,
            };
            _fieldLabelStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
            };
            _hintStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 12,
                wordWrap = true,
            };
            _sectionTitleStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
            };
            _sectionHintStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) },
            };
            _presetButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            _presetCaptionStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) },
            };
            _fillHintStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
            };
            _cardBoxStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(0, 0, 0, 0),
            };
        }

        void DrawSectionLine(string title, string hint)
        {
            var rect = GUILayoutUtility.GetRect(
                10f, 18f, GUILayout.ExpandWidth(true), GUILayout.Height(18f));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var titleW = Mathf.Min(_sectionTitleStyle.CalcSize(new GUIContent(title)).x + 8f, rect.width * 0.38f);
            GUI.Label(new Rect(rect.x, rect.y, titleW, rect.height), title, _sectionTitleStyle);
            GUI.Label(
                new Rect(rect.x + titleW, rect.y, rect.width - titleW, rect.height),
                hint,
                _sectionHintStyle);
        }

        void DrawFillHint(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                GUILayout.FlexibleSpace();
                return;
            }

            var rect = GUILayoutUtility.GetRect(
                10f,
                24f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(24f));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.16f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), new Color(1f, 1f, 1f, 0.12f));
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 4f, rect.width - 12f, rect.height - 8f),
                text,
                _fillHintStyle);
        }

        void DrawVisualCard(
            Row row,
            AbilityFxMechanic mechanic,
            float width,
            out Color color,
            out float scale,
            out float rangeValue,
            out RangeField rangeKind,
            out bool rangeChanged)
        {
            BeginCard("Вид и размер", width);
            rangeKind = ResolveRangeField(row, mechanic);
            rangeValue = ReadRangeValue(row, mechanic, rangeKind);
            var prevRange = rangeValue;
            var changedBeforeRange = GUI.changed;

            DrawSectionLine("Вид", "цвет и префаб");
            EditorGUILayout.BeginHorizontal(GUILayout.Height(22f));
            EditorGUILayout.LabelField("Цвет", _fieldLabelStyle, GUILayout.Width(FieldLabelWidth));
            color = EditorGUILayout.ColorField(
                GUIContent.none,
                row.Fx.Color,
                showEyedropper: true,
                showAlpha: false,
                hdr: false,
                GUILayout.Height(22f));
            color.a = 1f;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                row.Fx.VfxPrefab != null
                    ? $"Префаб: {row.Fx.VfxPrefab.name}"
                    : row.VfxKind == AbilityVfxKind.Aura
                        ? "Префаб: дефолт ауры из MatchFxCatalog"
                        : "Префаб не задан — выбор в палитре →",
                _hintStyle);

            EditorGUILayout.Space(4f);
            DrawSectionLine("Размер", "масштаб картинки и метры боя");
            EditorGUILayout.BeginHorizontal(GUILayout.Height(18f));
            EditorGUILayout.LabelField("Масштаб", _fieldLabelStyle, GUILayout.Width(FieldLabelWidth));
            scale = EditorGUILayout.Slider(
                GUIContent.none,
                AbilityFx.ResolveScale(row.Fx.Scale),
                0.25f,
                8f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(GUILayout.Height(18f));
            using (new EditorGUI.DisabledScope(rangeKind == RangeField.None || row.Def == null))
            {
                if (rangeKind == RangeField.None)
                {
                    EditorGUILayout.LabelField("Радиус", _fieldLabelStyle, GUILayout.Width(FieldLabelWidth));
                    EditorGUILayout.LabelField("без круга — точка на юните", _hintStyle);
                    rangeValue = 0f;
                }
                else
                {
                    EditorGUILayout.LabelField(
                        rangeKind == RangeField.CastRange ? "Досягаемость" : "Радиус",
                        _fieldLabelStyle,
                        GUILayout.Width(FieldLabelWidth));
                    rangeValue = EditorGUILayout.Slider(GUIContent.none, rangeValue, 0.5f, 16f);
                }
            }

            EditorGUILayout.EndHorizontal();
            rangeChanged = !Mathf.Approximately(prevRange, rangeValue);
            if (rangeChanged)
            {
                GUI.changed = changedBeforeRange;
            }

            DrawFillHint(VisualFooter(row, rangeKind));
            CloseCard();
        }

        static string VisualFooter(Row row, RangeField rangeKind)
        {
            if (row.Def == null && rangeKind != RangeField.None)
            {
                return "Divine Blessing: радиус не в этом ассете — правь его на герое.";
            }

            return rangeKind switch
            {
                RangeField.CastRange =>
                    "Цвет тинтует партиклы. Масштаб — только картинка, не урон. Досягаемость — как далеко ищет цель, круга AoE нет.",
                RangeField.None =>
                    "Цвет тинтует партиклы. Масштаб — только картинка. Это точечный эффект: боевого круга нет.",
                _ =>
                    "Цвет тинтует партиклы. Масштаб — только картинка, не урон. Радиус — боевые метры, кольцо в превью 1:1.",
            };
        }

        void DrawAnchorCard(Row row, ref AbilityVfxAnchor anchor, float width)
        {
            BeginCard("Где — якорь спавна", width);
            if (row.VfxKind == AbilityVfxKind.Aura)
            {
                DrawFillHint(
                    "Аура всегда на носителе и едет с ним. Якорь не выбирается — карточка остаётся, чтобы ряд не прыгал.");
                anchor = AbilityVfxAnchor.Caster;
                CloseCard();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (DrawChoiceCell("На себе", "едет с моделью", anchor == AbilityVfxAnchor.Caster, AnchorCellHeight))
            {
                anchor = AbilityVfxAnchor.Caster;
            }

            if (DrawChoiceCell("На цели", "едет с моделью", anchor == AbilityVfxAnchor.Target, AnchorCellHeight))
            {
                anchor = AbilityVfxAnchor.Target;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (DrawChoiceCell("Под собой", "на земле", anchor == AbilityVfxAnchor.Ground, AnchorCellHeight))
            {
                anchor = AbilityVfxAnchor.Ground;
            }

            if (DrawChoiceCell("Под целью", "на земле", anchor == AbilityVfxAnchor.Impact, AnchorCellHeight))
            {
                anchor = AbilityVfxAnchor.Impact;
            }

            EditorGUILayout.EndHorizontal();
            DrawFillHint(AnchorFooter(anchor));
            CloseCard();
        }

        static string AnchorFooter(AbilityVfxAnchor anchor) => anchor switch
        {
            AbilityVfxAnchor.Caster =>
                "На теле кастера. Следует за анимацией и поворотом модели — удар, щит, вспышка у себя.",
            AbilityVfxAnchor.Target =>
                "На теле цели. Едет вместе с врагом или союзником, пока живёт эффект.",
            AbilityVfxAnchor.Ground =>
                "Точка на земле у кастера. Не следует: лужа, consecration, круг остаются где поставлены.",
            AbilityVfxAnchor.Impact =>
                "Точка на земле у цели — куда прилетело. Сплэш и взрыв остаются на месте.",
            _ => "Выбери, к чему привязан спавн: тело едет с моделью, земля остаётся.",
        };

        bool DrawChoiceCell(string title, string caption, bool active) =>
            DrawChoiceCell(title, caption, active, 36f);

        bool DrawChoiceCell(string title, string caption, bool active, float height)
        {
            _anchorTitleStyle ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
            };
            _anchorCaptionStyle ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 1f, 1f, 0.58f) },
            };

            var prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            var rect = GUILayoutUtility.GetRect(
                48f,
                height,
                GUILayout.MinWidth(48f),
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, GUIContent.none);
            GUI.Label(
                new Rect(rect.x + 3f, rect.y + 4f, rect.width - 6f, 18f),
                title,
                _anchorTitleStyle);
            GUI.Label(
                new Rect(rect.x + 3f, rect.y + 22f, rect.width - 6f, 18f),
                caption,
                _anchorCaptionStyle);
            GUI.backgroundColor = prev;
            if (clicked)
            {
                GUI.changed = true;
            }

            return clicked;
        }

        void DrawEulerCard(Row row, ref Vector3 euler, float width)
        {
            BeginCard("Поворот", width);
            if (row.VfxKind == AbilityVfxKind.Aura)
            {
                DrawFillHint(
                    "У аур поворот не применяется: кольцо стоит как у префаба и едет с носителем.");
                CloseCard();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (DrawPresetCell("Горизонталь", "как в префабе", Approximately(euler, EulerHorizontal)))
            {
                euler = EulerHorizontal;
            }

            if (DrawPresetCell("Вертикаль", "столбом", Approximately(euler, EulerVertical)))
            {
                euler = EulerVertical;
            }

            if (DrawPresetCell("В пол", "плашмя", Approximately(euler, EulerFloor)))
            {
                euler = EulerFloor;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            euler.x = DrawAxisField("X", euler.x);
            euler.y = DrawAxisField("Y", euler.y);
            euler.z = DrawAxisField("Z", euler.z);
            EditorGUILayout.EndHorizontal();
            DrawFillHint(EulerFooter(euler));
            CloseCard();
        }

        static string EulerFooter(Vector3 euler)
        {
            if (Approximately(euler, EulerVertical))
            {
                return "Столб: Z 90°. Луч, колонна, фонтан вверх. Крутится только VFX, не юнит.";
            }

            if (Approximately(euler, EulerFloor))
            {
                return "Плашмя: X 90°. Круг и декаль лежат на земле. 0/0/0 — ориентация как в префабе.";
            }

            if (Approximately(euler, EulerHorizontal))
            {
                return "Горизонталь: 0/0/0, как заложено в префабе. Крутится только эффект, не персонаж.";
            }

            return "Свои градусы. 0/0/0 — как в префабе. Крутится только VFX, не юнит.";
        }

        float DrawAxisField(string axis, float value)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(axis, _fieldLabelStyle, GUILayout.Width(14f));
            var next = EditorGUILayout.FloatField(GUIContent.none, value, GUILayout.MinWidth(36f));
            EditorGUILayout.EndHorizontal();
            return next;
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

        bool DrawPresetCell(string title, string caption, bool active)
        {
            EnsureCardStyles();
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MinWidth(0f));
            var prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            var clicked = GUILayout.Button(
                title,
                _presetButtonStyle,
                GUILayout.Height(PresetButtonHeight),
                GUILayout.MinWidth(0f),
                GUILayout.ExpandWidth(true));
            GUI.backgroundColor = prev;
            EditorGUILayout.LabelField(
                caption,
                _presetCaptionStyle,
                GUILayout.Height(16f),
                GUILayout.MinWidth(0f));
            EditorGUILayout.EndVertical();
            if (clicked)
            {
                GUI.changed = true;
            }

            return clicked;
        }

        bool DrawAnimClipCell(string title, string caption, string tooltip, bool active)
        {
            _animClipButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4),
            };
            _animClipTitleStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _animClipCaptionStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 1f, 1f, 0.62f) },
            };

            var hasCaption = !string.IsNullOrEmpty(caption) && caption != title;
            var prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            var rect = GUILayoutUtility.GetRect(
                80f,
                AnimCellHeight,
                GUILayout.MinWidth(56f),
                GUILayout.Height(AnimCellHeight),
                GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), _animClipButtonStyle);
            if (hasCaption)
            {
                GUI.Label(
                    new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f),
                    title,
                    _animClipTitleStyle);
                GUI.Label(
                    new Rect(rect.x + 6f, rect.y + 20f, rect.width - 12f, 16f),
                    caption,
                    _animClipCaptionStyle);
            }
            else
            {
                GUI.Label(rect, title, _animClipTitleStyle);
            }

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
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            _animHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
            };
            EditorGUILayout.LabelField(
                "Анимация",
                _animHeaderStyle,
                GUILayout.Width(92f),
                GUILayout.Height(AnimCellHeight));
            if (clips.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "нет клипов",
                    EditorStyles.centeredGreyMiniLabel,
                    GUILayout.Height(AnimCellHeight));
                EditorGUILayout.EndHorizontal();
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

            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var tooltip = string.IsNullOrEmpty(clip.DisplayName) ? clip.StateName : clip.DisplayName;
                if (DrawAnimClipCell(clip.StateName, clip.ClipName, tooltip, i == displayClip))
                {
                    animState = clip.StateName;
                    animVariant = clip.Variant < 0 ? 0 : clip.Variant;
                    animKind = AbilityAnimRules.ResolveKindFromState(animState);
                    if (animKind == AbilityAnimKind.Unspecified)
                    {
                        animKind = AbilityAnimKind.None;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
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
            rows.Add(Row.FromMainExtra(AbilityIds.MainIceRing));
            rows.Add(Row.FromMainExtra(AbilityIds.MainWaveOfLight));
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
                    VfxPrefab = LoadSeedPrefab(DivineSkyBeamPrefabPath),
                    Anchor = AbilityVfxAnchor.Target,
                });
            catalog.EditorSetFx(
                AbilityIds.MainUnitSmite,
                new AbilityFx
                {
                    Color = AbilityFxColors.DivineSmite,
                    VfxPrefab = LoadSeedPrefab(DivineSkyBeamPrefabPath),
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
            if (ShouldRestoreDivineSkyBeam(building.VfxPrefab, LegacyDivineBuildingSmitePrefabName)
                || building.Anchor == AbilityVfxAnchor.Impact)
            {
                catalog.EditorSetFx(
                    AbilityIds.MainBuildingSmite,
                    new AbilityFx
                    {
                        Color = building.Color.a > 0.01f ? building.Color : AbilityFxColors.DivineSmite,
                        VfxPrefab = building.VfxPrefab != null
                            ? building.VfxPrefab
                            : LoadSeedPrefab(DivineSkyBeamPrefabPath),
                        Anchor = AbilityVfxAnchor.Target,
                    });
                dirty = true;
            }

            var unit = catalog.GetFx(AbilityIds.MainUnitSmite);
            if (ShouldRestoreDivineSkyBeam(unit.VfxPrefab, LegacyDivineUnitSmitePrefabName))
            {
                catalog.EditorSetFx(
                    AbilityIds.MainUnitSmite,
                    new AbilityFx
                    {
                        Color = unit.Color.a > 0.01f ? unit.Color : AbilityFxColors.DivineSmite,
                        VfxPrefab = LoadSeedPrefab(DivineSkyBeamPrefabPath),
                        Anchor = unit.Anchor != AbilityVfxAnchor.Unspecified
                            ? unit.Anchor
                            : AbilityVfxAnchor.Target,
                    });
                dirty = true;
            }

            var iceRing = catalog.GetFx(AbilityIds.MainIceRing);
            if (iceRing.VfxPrefab == null)
            {
                catalog.EditorSetFx(
                    AbilityIds.MainIceRing,
                    new AbilityFx
                    {
                        Color = iceRing.Color.a > 0.01f ? iceRing.Color : AbilityFxColors.Frost,
                        VfxPrefab = LoadSeedPrefab(IceRingSeedPrefabPath),
                        Scale = 2f,
                    });
                dirty = true;
            }

            var waveOfLight = catalog.GetFx(AbilityIds.MainWaveOfLight);
            if (waveOfLight.VfxPrefab == null)
            {
                catalog.EditorSetFx(
                    AbilityIds.MainWaveOfLight,
                    new AbilityFx
                    {
                        Color = waveOfLight.Color.a > 0.01f ? waveOfLight.Color : AbilityFxColors.Priest,
                        VfxPrefab = LoadSeedPrefab(WaveOfLightSeedPrefabPath),
                        Scale = 3f,
                    });
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(catalog);
            }
        }

        static GameObject LoadSeedPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null && path == DivineSkyBeamPrefabPath)
            {
                prefab = CustomAbilityFxPrefabBuilder.EnsureSkyBeam();
            }

            return prefab;
        }

        static bool ShouldRestoreDivineSkyBeam(GameObject current, string legacyPrefabName)
        {
            if (current == null)
            {
                return true;
            }

            var path = AssetDatabase.GetAssetPath(current);
            return !string.IsNullOrEmpty(path)
                && path.IndexOf(legacyPrefabName, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

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
                var displayName = MainExtraAbilityFxDefs.GetDisplayName(abilityId);
                if (string.IsNullOrEmpty(displayName) && def != null)
                {
                    displayName = def.DisplayName;
                }

                return new Row(
                    abilityId,
                    !string.IsNullOrEmpty(displayName) ? displayName : $"Main extra {abilityId}",
                    AbilityKind.Active,
                    AbilityVfxKindRules.Resolve(abilityId),
                    fx,
                    null,
                    catalog);
            }
        }
    }
}
