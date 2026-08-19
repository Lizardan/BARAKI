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
    /// Master-detail authoring: kit list on the left, one live caster/target preview on the right.
    /// Open: main toolbar <c>Ability FX</c> or <c>BARAKI/Ability FX Viewer</c>.
    /// </summary>
    public sealed class AbilityFxViewerWindow : EditorWindow
    {
        const float ListWidth = 268f;
        const string SelectedIdKey = "BARAKI.AbilityFxViewer.SelectedId";
        const string DivineBuildingSmitePrefabPath =
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR3 Fire Explosion B.prefab";
        const string DivineUnitSmitePrefabPath =
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab";

        Vector2 _listScroll;
        string _search = "";
        int _selectedId;
        List<AbilityVfxPrefabIndex.Entry> _index;
        AbilityFxPreviewSession _preview;
        UnitVisualCatalog _visualCatalog;
        BuildingVisualCatalog _buildingCatalog;
        double _lastTick;
        GUIStyle _overlayLabel;
        GameObject _clipSource;
        List<AbilityAnimClipIndex.Entry> _clips;

        [MenuItem("BARAKI/Ability FX Viewer", false, 0)]
        [MenuItem("BARAKI/Abilities/Ability FX Viewer", false, 0)]
        public static void Open()
        {
            var window = GetWindow<AbilityFxViewerWindow>("Ability FX Viewer");
            window.minSize = new Vector2(1080f, 680f);
            window.Show();
            window.Focus();
        }

        void OnEnable()
        {
            _index = AbilityVfxPrefabIndex.Scan();
            _visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            _buildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingVisualCatalog>(
                BuildingVisualPrefabBuilder.CatalogPath);
            _selectedId = SessionState.GetInt(SelectedIdKey, AbilityIds.Strike);
            _lastTick = EditorApplication.timeSinceStartup;
            _preview ??= new AbilityFxPreviewSession();
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SessionState.SetInt(SelectedIdKey, _selectedId);
            _preview?.Dispose();
            _preview = null;
        }

        void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _lastTick);
            _lastTick = now;
            if (dt > 0f)
            {
                _preview?.Tick(dt);
            }

            Repaint();
        }

        void OnGUI()
        {
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

            EditorGUILayout.BeginHorizontal();
            DrawList(rows);
            DrawDetail(selected);
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Обновить список эффектов", EditorStyles.toolbarButton, GUILayout.Width(200f)))
            {
                _index = AbilityVfxPrefabIndex.Scan();
                _preview?.Replay();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"{(_index?.Count ?? 0)} префабов · правки пишутся в AbilityFx и переживают Build Ability Defs",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawList(List<Row> rows)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
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
                Event.current.Use();
                GUI.FocusControl(null);
            }
        }

        void DrawDetail(Row selected)
        {
            EditorGUILayout.BeginVertical();
            var previewRect = GUILayoutUtility.GetRect(
                16f,
                16f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(360f));
            HandlePreviewInput(previewRect);
            var caster = AbilityFxPreviewCasterRules.Resolve(selected.AbilityId);
            DrawPreview(previewRect, selected, caster);
            EditorGUILayout.LabelField(
                $"Слева: {caster.DisplayName}  ·  Справа: мечник",
                EditorStyles.miniLabel);
            DrawSettings(selected);
            EditorGUILayout.EndVertical();
        }

        void HandlePreviewInput(Rect previewRect)
        {
            var evt = Event.current;
            if (!previewRect.Contains(evt.mousePosition) || _preview == null)
            {
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                _preview.Zoom(-evt.delta.y);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                _preview.Orbit(evt.delta.x, evt.delta.y);
                evt.Use();
            }
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
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(row.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"{AbilityVfxKindRules.KitLabel(row.AbilityId)}  ·  {row.Kind}  ·  {row.VfxKind}  ·  id {row.AbilityId}",
                EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField(
                new GUIContent("Цвет RGB"),
                row.Fx.Color,
                showEyedropper: true,
                showAlpha: false,
                hdr: false);
            color.a = 1f;

            var prefab = row.Fx.VfxPrefab;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Эффект");
            var effectLabel = prefab != null ? prefab.name : "— нет —";
            if (GUILayout.Button(effectLabel, EditorStyles.popup))
            {
                OpenPrefabPicker(row);
            }

            EditorGUILayout.EndHorizontal();
            prefab = (GameObject)EditorGUILayout.ObjectField("Префаб", prefab, typeof(GameObject), false);

            var scale = EditorGUILayout.Slider(
                "Масштаб",
                AbilityFx.ResolveScale(row.Fx.Scale),
                0.25f,
                8f);

            var anchor = AbilityVfxKindRules.ResolveAnchor(row.AbilityId, row.Fx.Anchor);
            if (row.VfxKind == AbilityVfxKind.Aura)
            {
                EditorGUILayout.LabelField("Где играет", "всегда на носителе");
                anchor = AbilityVfxAnchor.Caster;
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Где играет");
                if (DrawAnchorButton("Кастер", anchor == AbilityVfxAnchor.Caster))
                {
                    anchor = AbilityVfxAnchor.Caster;
                }

                if (DrawAnchorButton("Цель", anchor == AbilityVfxAnchor.Target))
                {
                    anchor = AbilityVfxAnchor.Target;
                }

                if (DrawAnchorButton("Земля", anchor == AbilityVfxAnchor.Ground))
                {
                    anchor = AbilityVfxAnchor.Ground;
                }

                if (DrawAnchorButton("Удар", anchor == AbilityVfxAnchor.Impact))
                {
                    anchor = AbilityVfxAnchor.Impact;
                }

                EditorGUILayout.EndHorizontal();
            }

            var animState = row.Fx.AnimState;
            var animVariant = row.Fx.AnimVariant;
            var animKind = AbilityAnimRules.ResolveAnim(row.AbilityId, row.Fx.AnimKind, animState);
            var caster = AbilityFxPreviewCasterRules.Resolve(row.AbilityId);
            var clips = ClipsFor(LoadCasterPrefab(caster));
            if (clips.Count == 0)
            {
                EditorGUILayout.LabelField("Анимация", "нет клипов");
            }
            else
            {
                var clipNames = new string[clips.Count];
                for (var i = 0; i < clips.Count; i++)
                {
                    clipNames[i] = clips[i].DisplayName;
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

                var nextClip = EditorGUILayout.Popup("Анимация", displayClip, clipNames);
                if (nextClip != displayClip)
                {
                    animState = clips[nextClip].StateName;
                    animVariant = clips[nextClip].Variant < 0 ? 0 : clips[nextClip].Variant;
                    animKind = AbilityAnimRules.ResolveKindFromState(animState);
                    if (animKind == AbilityAnimKind.Unspecified)
                    {
                        animKind = AbilityAnimKind.None;
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
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
                };
                SaveFx(row, fx);
                PushEffect(row, fx);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Replay", GUILayout.Width(80f)))
            {
                _preview?.Replay();
            }

            using (new EditorGUI.DisabledScope(row.PingTarget == null))
            {
                if (GUILayout.Button("Ping asset", GUILayout.Width(90f)))
                {
                    EditorGUIUtility.PingObject(row.PingTarget);
                    Selection.activeObject = row.PingTarget;
                }
            }

            GUILayout.Label("ЛКМ — орбита, колёсико — зум", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void OpenPrefabPicker(Row row)
        {
            var abilityId = row.AbilityId;
            AbilityVfxPrefabPickerWindow.Open(
                _index,
                row.VfxKind,
                row.Fx.VfxPrefab,
                picked =>
                {
                    var current = FindRow(CollectRows(), abilityId);
                    var fx = current.Fx;
                    fx.VfxPrefab = picked;
                    SaveFx(current, fx);
                    PushEffect(current, fx);
                    Repaint();
                });
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

        static bool DrawAnchorButton(string label, bool active)
        {
            var prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            var clicked = GUILayout.Button(label, GUILayout.Width(72f));
            GUI.backgroundColor = prev;
            return clicked;
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
                fx.Scale);
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
