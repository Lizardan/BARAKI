using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Shared read-only rendering for the prefab inspector: a balance stats card (with navigation
    /// to the race definition asset that seeds <see cref="UnitCombatSettings"/>) and the ability
    /// cards. Both are hosted together by <see cref="UnitCombatSettingsEditor"/> so one prefab
    /// shows one viewer; editing lives on the definition/def assets.
    /// </summary>
    public static class UnitInspectorDrawer
    {
        static readonly Color ActiveColor = new(0.35f, 0.8f, 0.45f);
        static readonly Color PassiveColor = new(0.55f, 0.65f, 1f);
        static readonly Color MutedColor = new(0.6f, 0.6f, 0.6f);

        static readonly UnitRole[] UnitRoles =
        {
            UnitRole.Melee,
            UnitRole.Ranged,
            UnitRole.Caster,
            UnitRole.Siege,
            UnitRole.Flying,
            UnitRole.Super,
        };

        readonly struct BalanceSourceInfo
        {
            public ScriptableObject Definition { get; }

            public BalanceSourceInfo(ScriptableObject definition)
            {
                Definition = definition;
            }
        }

        // ---------- Balance card ----------

        public static void DrawBalanceCard(UnitCombatSettings settings, GameObject prefabRoot)
        {
            var source = ResolveBalanceSource(prefabRoot);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetPrefabDisplayName(prefabRoot), TitleStyle());
            DrawOpenBalanceButton(source);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            DrawStatRow("Запас здоровья", FormatNumber(settings.MaxHp));
            DrawStatRow("Броня", FormatNumber(settings.Armor));
            DrawStatRow("Урон", $"{FormatNumber(settings.DamageMin)}–{FormatNumber(settings.DamageMax)}");
            DrawStatRow("Скорость атаки", $"{FormatNumber(settings.AttackSpeed)}/с");
            DrawStatRow("Дальность атаки", FormatNumber(settings.AttackRange));
            DrawStatRow("Скорость", FormatNumber(settings.MoveSpeed));
            DrawStatRow("Награда золотом", settings.GoldBounty.ToString());

            if (settings.MaxMana > 0f)
            {
                DrawStatRow("Макс. мана", FormatNumber(settings.MaxMana));
            }

            if (settings.MarchSpeedOverride > 0f)
            {
                DrawStatRow("Скорость марша", FormatNumber(settings.MarchSpeedOverride));
            }

            EditorGUILayout.EndVertical();
        }

        static void DrawOpenBalanceButton(BalanceSourceInfo source)
        {
            var tooltip = source.Definition != null
                ? $"Открыть {source.Definition.name}"
                : "Баланс-ассет не найден — открыть RaceCatalog";

            if (!GUILayout.Button(new GUIContent("Открыть", tooltip), GUILayout.Width(72f)))
            {
                return;
            }

            if (source.Definition != null)
            {
                Selection.activeObject = source.Definition;
                EditorGUIUtility.PingObject(source.Definition);
            }
            else
            {
                var catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(UnitBalanceSetup.RaceCatalogPath);
                if (catalog != null)
                {
                    Selection.activeObject = catalog;
                    EditorGUIUtility.PingObject(catalog);
                }
            }
        }

        static BalanceSourceInfo ResolveBalanceSource(GameObject prefabRoot)
        {
            if (prefabRoot == null)
            {
                return default;
            }

            var prefabPath = ResolvePrefabPath(prefabRoot);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return default;
            }

            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(UnitBalanceSetup.RaceCatalogPath);
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (raceCatalog == null || visualCatalog == null)
            {
                return default;
            }

            // Resolve the balance source for both races so the Faceless prefabs' "Open" button
            // (and the balance card) behave uniformly with the Humans ones.
            foreach (var raceId in new[] { GameIds.Races.Human, GameIds.Races.Faceless })
            {
                var race = raceCatalog.GetRace(raceId);
                if (race == null)
                {
                    continue;
                }

                foreach (var role in UnitRoles)
                {
                    if (visualCatalog.TryGetPrefab(raceId, role, 0, out var prefab)
                        && prefab != null
                        && AssetDatabase.GetAssetPath(prefab) == prefabPath)
                    {
                        return new BalanceSourceInfo(race.GetUnit(role));
                    }
                }

                for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
                {
                    if (visualCatalog.TryGetPrefab(raceId, UnitRole.Hero, slot, out var prefab)
                        && prefab != null
                        && AssetDatabase.GetAssetPath(prefab) == prefabPath)
                    {
                        return new BalanceSourceInfo(race.GetHeroBySlot(slot));
                    }
                }

                if (visualCatalog.TryGetPrefab(raceId, UnitRole.Titan, 0, out var titan)
                    && titan != null
                    && AssetDatabase.GetAssetPath(titan) == prefabPath)
                {
                    return new BalanceSourceInfo(race.GetHeroBySlot(1));
                }
            }

            return default;
        }

        static string ResolvePrefabPath(GameObject prefabRoot)
        {
            var prefabPath = AssetDatabase.GetAssetPath(prefabRoot);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                return prefabPath;
            }

            prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                return prefabPath;
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage != null && prefabStage.IsPartOfPrefabContents(prefabRoot)
                ? prefabStage.assetPath
                : string.Empty;
        }

        static string GetPrefabDisplayName(GameObject prefabRoot)
        {
            var prefabPath = ResolvePrefabPath(prefabRoot);
            return !string.IsNullOrEmpty(prefabPath)
                ? Path.GetFileNameWithoutExtension(prefabPath)
                : prefabRoot.name;
        }

        static void DrawStatRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, StatLabelStyle());
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(value, StatValueStyle(), GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        // ---------- Ability cards ----------

        public static void DrawAbilityCards(UnitCombatSettings settings)
        {
            if (settings == null || settings.Abilities.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Способности — {settings.Abilities.Length} шт.", TitleStyle());
            EditorGUILayout.LabelField(
                "Порядок списка = приоритет каста AI (первая кастуется первой). «Открыть» — перейти к def-ассету.",
                HintStyle());
            EditorGUILayout.Space(4);

            for (var i = 0; i < settings.Abilities.Length; i++)
            {
                var def = settings.Abilities[i];
                if (def == null)
                {
                    EditorGUILayout.HelpBox(
                        $"{i}. <null> — ссылка потеряна. Перезапусти BARAKI/Units/Seed Unit Abilities.",
                        MessageType.Warning);
                    continue;
                }

                DrawAbility(def, i);
            }
        }

        static void DrawAbility(UnitAbilityDef def, int index)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            DrawColorSwatch(def.Fx.Color, 16f);
            var kindColor = def.IsPassiveAura ? PassiveColor : ActiveColor;
            EditorGUILayout.LabelField($"{index + 1}. {def.DisplayName}", Bold(kindColor));
            EditorGUILayout.LabelField(
                def.IsActive ? "Активная" : "Пассивная",
                Mini(def.IsActive ? ActiveColor : MutedColor),
                GUILayout.Width(82f));

            if (GUILayout.Button("Открыть", GUILayout.Width(72f)))
            {
                Selection.activeObject = def;
                EditorGUIUtility.PingObject(def);
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(def.Description))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(def.Description, DescriptionStyle());
            }

            var paramsText = BuildParamsText(def);
            if (!string.IsNullOrEmpty(paramsText))
            {
                EditorGUILayout.LabelField(paramsText, ParamsStyle());
            }

            if (def.IsPassiveAura && def.Behaviour is AuraBehaviour aura)
            {
                EditorGUILayout.LabelField(
                    $"Аура армии: {StatName(aura.Stat)} {FormatSignedPercent(def.Percent)}",
                    ParamsStyle());
            }

            EditorGUILayout.Space(3);
            var meta = $"id {def.AbilityId}  ·  {UnlockText(def)}";
            if (def.Behaviour != null)
            {
                meta += $"  ·  {def.Behaviour.GetType().Name}";
            }

            meta += def.Fx.VfxPrefab != null
                ? $"  ·  FX: {def.Fx.VfxPrefab.name}"
                : $"  ·  FX: {(def.Fx.Color.a > 0f ? "color only" : "none")}";
            EditorGUILayout.LabelField(meta, MetaStyle());
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        static string BuildParamsText(UnitAbilityDef def)
        {
            var parts = new List<string>();
            Add("Урон", def.Damage);
            Add("Лечение", def.Heal);
            Add("Лечение/с", def.HealPerSecond);
            Add("Радиус", def.Radius);
            Add("Дальность", def.CastRange);
            Add("КД", def.CooldownSeconds);
            Add("Длит.", def.DurationSeconds);
            if (!Mathf.Approximately(def.Percent, 0f) && !def.IsPassiveAura)
            {
                parts.Add($"Эффект {FormatPercent(def.Percent)}");
            }

            Add("Мана", def.ManaCost);
            Add("Оглушение", def.StunSeconds);
            Add("Бонус", def.FlatBonus);
            Add("Радиус²", def.SecondaryRadius);
            Add("Лечение²", def.SecondaryHeal);
            return parts.Count > 0 ? "Параметры: " + string.Join("   ·   ", parts) : string.Empty;

            void Add(string label, float value)
            {
                if (!Mathf.Approximately(value, 0f))
                {
                    parts.Add($"{label} {FormatNumber(value)}");
                }
            }
        }

        static string UnlockText(UnitAbilityDef def) => def.Unlock switch
        {
            AbilityUnlock.Always => "всегда",
            AbilityUnlock.HeroLevel => $"уровень героя {def.UnlockValue}+",
            AbilityUnlock.MagicLevel => $"магия {def.UnlockValue}+",
            _ => "?",
        };

        static string StatName(AuraStat stat) => stat switch
        {
            AuraStat.Damage => "урон",
            AuraStat.AttackSpeed => "скорость атаки",
            AuraStat.Armor => "броня",
            AuraStat.MaxHp => "запас здоровья",
            _ => "?",
        };

        static string FormatNumber(float value) =>
            Mathf.Abs(value % 1f) < 0.001f ? value.ToString("0") : value.ToString("0.#");

        static string FormatPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        static string FormatSignedPercent(float value) =>
            value > 0f ? $"+{FormatPercent(value)}" : FormatPercent(value);

        static GUIStyle TitleStyle() => new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };

        static GUIStyle HintStyle() => new GUIStyle(EditorStyles.miniLabel) { fontSize = 11, wordWrap = true };

        static GUIStyle StatLabelStyle() => new GUIStyle(EditorStyles.miniLabel) { fontSize = 12 };

        static GUIStyle StatValueStyle() =>
            new GUIStyle(EditorStyles.miniLabel) { fontSize = 12, fontStyle = FontStyle.Bold };

        static GUIStyle Bold(Color color) =>
            new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 14, normal = { textColor = color } };

        static GUIStyle Mini(Color color) =>
            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color } };

        static GUIStyle DescriptionStyle() => new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 13,
            wordWrap = true,
            richText = true,
        };

        static GUIStyle ParamsStyle() => new GUIStyle(EditorStyles.miniLabel) { fontSize = 12, wordWrap = true };

        static GUIStyle MetaStyle() => new GUIStyle(EditorStyles.miniLabel) { fontSize = 11, wordWrap = true };

        static void DrawColorSwatch(Color color, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (color.a <= 0f)
            {
                color = new Color(0.35f, 0.35f, 0.35f);
            }

            EditorGUI.DrawRect(rect, color);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        }
    }
}
