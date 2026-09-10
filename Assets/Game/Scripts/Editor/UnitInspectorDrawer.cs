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
            public string RaceId { get; }
            public UnitRole Role { get; }
            public int HeroSlot { get; }
            public int BonusSlot { get; }
            public bool HasKey { get; }

            public BalanceSourceInfo(ScriptableObject definition)
                : this(definition, null, default, 0, 0, false)
            {
            }

            public BalanceSourceInfo(
                ScriptableObject definition,
                string raceId,
                UnitRole role,
                int heroSlot,
                int bonusSlot)
                : this(definition, raceId, role, heroSlot, bonusSlot, true)
            {
            }

            BalanceSourceInfo(
                ScriptableObject definition,
                string raceId,
                UnitRole role,
                int heroSlot,
                int bonusSlot,
                bool hasKey)
            {
                Definition = definition;
                RaceId = raceId;
                Role = role;
                HeroSlot = heroSlot;
                BonusSlot = bonusSlot;
                HasKey = hasKey;
            }
        }

        // ---------- Balance card ----------

        public static void DrawBalanceCard(UnitCombatSettings settings, GameObject prefabRoot)
        {
            if (settings == null)
            {
                return;
            }

            var source = ResolveBalanceSource(settings, prefabRoot);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetPrefabDisplayName(prefabRoot), TitleStyle());
            DrawOpenBalanceButton(source);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            // Prefer resolved stats from the source definition asset, so the inspector always
            // shows the latest values (including derived hero/titan/veteran variants).
            if (source.HasKey)
            {
                var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(ContentAssetPaths.RaceCatalog);
                var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
                if (raceCatalog != null && visualCatalog != null)
                {
                    var adapter = new RaceCatalogCombatCatalog(raceCatalog);
                    DrawStats(UnitStatsResolver.ResolveBase(
                        adapter,
                        visualCatalog,
                        source.RaceId,
                        source.Role,
                        source.HeroSlot,
                        source.BonusSlot));
                    DrawVariantBadge(source);
                }
            }
            else if (source.Definition is HeroDefinition hero)
            {
                DrawRawHero(hero);
            }
            else if (source.Definition is UnitDefinition unit)
            {
                DrawRawUnit(unit);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Источник статов не задан — запусти BARAKI/Units/Assign Stats Sources.",
                    MessageType.Warning);
            }

            DrawVisualScaleRow(settings, prefabRoot);

            EditorGUILayout.EndVertical();
        }

        static void DrawVisualScaleRow(UnitCombatSettings settings, GameObject prefabRoot)
        {
            if (settings == null)
            {
                return;
            }

            var definition = settings.UnitDefinition != null
                ? (ScriptableObject)settings.UnitDefinition
                : settings.HeroDefinition;

            var authored = 0f;
            if (definition is UnitDefinition unit)
            {
                authored = unit.VisualScale;
            }
            else if (definition is HeroDefinition hero)
            {
                authored = hero.VisualScale;
            }

            var prefabScale = prefabRoot != null ? prefabRoot.transform.localScale.x : 1f;
            var effective = authored > 0f ? authored : prefabScale;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Визуальный масштаб (в бою)", StatLabelStyle());
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                EffectiveVisualScale(settings, prefabRoot).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                StatValueStyle(),
                GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        static float EffectiveVisualScale(UnitCombatSettings settings, GameObject prefabRoot)
        {
            var definition = settings.UnitDefinition != null
                ? (ScriptableObject)settings.UnitDefinition
                : settings.HeroDefinition;

            var authored = 0f;
            if (definition is UnitDefinition unit)
            {
                authored = unit.VisualScale;
            }
            else if (definition is HeroDefinition hero)
            {
                authored = hero.VisualScale;
            }

            return authored > 0f
                ? authored
                : prefabRoot != null
                    ? prefabRoot.transform.localScale.x
                    : 1f;
        }

        static void DrawStats(UnitCombatStats stats)
        {
            DrawStatRow("Запас здоровья", FormatNumber(stats.MaxHp));
            DrawStatRow("Броня", FormatNumber(stats.Armor));
            DrawStatRow("Урон", $"{FormatNumber(stats.DamageMin)}–{FormatNumber(stats.DamageMax)}");
            DrawStatRow("Скорость атаки", $"{FormatNumber(stats.AttackSpeed)}/с");
            DrawStatRow("Дальность атаки", FormatNumber(stats.AttackRange));
            DrawStatRow("Скорость", FormatNumber(stats.MoveSpeed));
            DrawStatRow("Награда золотом", stats.GoldBounty.ToString());
            if (stats.HasMana)
            {
                DrawStatRow("Макс. мана", FormatNumber(stats.MaxMana));
            }
        }

        static void DrawRawHero(HeroDefinition hero)
        {
            DrawStatRow("Запас здоровья", FormatNumber(hero.MaxHp));
            DrawStatRow("Броня", FormatNumber(hero.Armor));
            DrawStatRow("Урон", $"{FormatNumber(hero.DamageMin)}–{FormatNumber(hero.DamageMax)}");
            DrawStatRow("Скорость атаки", $"{FormatNumber(hero.AttackSpeed)}/с");
            DrawStatRow("Дальность атаки", FormatNumber(hero.AttackRange));
            DrawStatRow("Скорость", FormatNumber(hero.MoveSpeed));
            DrawStatRow("Награда золотом", hero.GoldBounty.ToString());
        }

        static void DrawRawUnit(UnitDefinition unit)
        {
            DrawStatRow("Запас здоровья", FormatNumber(unit.MaxHp));
            DrawStatRow("Броня", FormatNumber(unit.Armor));
            DrawStatRow("Урон", $"{FormatNumber(unit.DamageMin)}–{FormatNumber(unit.DamageMax)}");
            DrawStatRow("Скорость атаки", $"{FormatNumber(unit.AttackSpeed)}/с");
            DrawStatRow("Дальность атаки", FormatNumber(unit.AttackRange));
            DrawStatRow("Скорость", FormatNumber(unit.MoveSpeed));
            DrawStatRow("Награда золотом", unit.GoldBounty.ToString());
            if (unit.MaxMana > 0f)
            {
                DrawStatRow("Макс. мана", FormatNumber(unit.MaxMana));
            }

            if (unit.MarchSpeedOverride > 0f)
            {
                DrawStatRow("Скорость марша", FormatNumber(unit.MarchSpeedOverride));
            }
        }

        static void DrawVariantBadge(BalanceSourceInfo source)
        {
            string text;
            if (source.Role == UnitRole.Titan)
            {
                text = BonusKitRules.IsChampionBonusSlot(source.BonusSlot)
                    ? "Ветеран-титан: ×3 от героя 1 · ×1.4 HP · ×1.35 урон · броня +2"
                    : "Титан: ×3 от героя 1";
            }
            else if (BonusKitRules.IsChampionBonusSlot(source.BonusSlot))
            {
                text = "Ветеран: ×1.4 HP · ×1.35 урон · броня +2";
            }
            else if (source.BonusSlot >= 1 && source.BonusSlot <= 6)
            {
                text = $"Бонус: слот {source.BonusSlot}";
            }
            else
            {
                return;
            }

            EditorGUILayout.LabelField(text, HintStyle());
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
                var catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(ContentAssetPaths.RaceCatalog);
                if (catalog != null)
                {
                    Selection.activeObject = catalog;
                    EditorGUIUtility.PingObject(catalog);
                }
            }
        }

        static BalanceSourceInfo ResolveBalanceSource(UnitCombatSettings settings, GameObject prefabRoot)
        {
            if (prefabRoot == null)
            {
                return DefinedOrNone(settings);
            }

            var prefabPath = ResolvePrefabPath(prefabRoot);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return DefinedOrNone(settings);
            }

            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(ContentAssetPaths.RaceCatalog);
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (raceCatalog == null || visualCatalog == null)
            {
                return DefinedOrNone(settings);
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
                    if (TryMatchKey(visualCatalog, race, raceId, role, 0, 0, prefabPath, out var info))
                    {
                        return info;
                    }
                }

                for (var bonusSlot = 1; bonusSlot <= 6; bonusSlot++)
                {
                    if (TryMatchKey(
                            visualCatalog, race, raceId, BonusKitRules.RoleForBonusSlot(bonusSlot), 0, bonusSlot,
                            prefabPath, out var info))
                    {
                        return info;
                    }
                }

                for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
                {
                    if (TryMatchKey(visualCatalog, race, raceId, UnitRole.Hero, slot, 0, prefabPath, out var info))
                    {
                        return info;
                    }
                }

                if (TryMatchKey(visualCatalog, race, raceId, UnitRole.Titan, 0, 0, prefabPath, out var titanInfo))
                {
                    return titanInfo;
                }

                for (var bonusSlot = BonusKitRules.Hero1BonusSlot;
                     bonusSlot <= BonusKitRules.TitanBonusSlot;
                     bonusSlot++)
                {
                    var isTitan = BonusKitRules.IsTitanBonusSlot(bonusSlot);
                    var heroSlot = isTitan ? 1 : BonusKitRules.HeroSlotForBonusSlot(bonusSlot);
                    if (TryMatchKey(
                            visualCatalog, race, raceId,
                            isTitan ? UnitRole.Titan : UnitRole.Hero,
                            isTitan ? 0 : heroSlot,
                            bonusSlot,
                            prefabPath, out var info))
                    {
                        return info;
                    }
                }
            }

            return DefinedOrNone(settings);
        }

        static bool TryMatchKey(
            UnitVisualCatalog visualCatalog,
            RaceDefinition race,
            string raceId,
            UnitRole role,
            int heroSlot,
            int bonusSlot,
            string prefabPath,
            out BalanceSourceInfo info)
        {
            info = default;
            if (!visualCatalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var prefab)
                || prefab == null
                || AssetDatabase.GetAssetPath(prefab) != prefabPath)
            {
                return false;
            }

            var definition = ResolveDefinition(race, role, heroSlot, bonusSlot);
            if (definition == null)
            {
                return false;
            }

            info = new BalanceSourceInfo(definition, raceId, role, heroSlot, bonusSlot);
            return true;
        }

        static ScriptableObject ResolveDefinition(RaceDefinition race, UnitRole role, int heroSlot, int bonusSlot)
        {
            if (bonusSlot >= BonusKitRules.Hero1BonusSlot && bonusSlot <= BonusKitRules.TitanBonusSlot)
            {
                var slot = bonusSlot == BonusKitRules.TitanBonusSlot
                    ? 1
                    : BonusKitRules.HeroSlotForBonusSlot(bonusSlot);
                return race.GetHeroBySlot(slot);
            }

            if (role is UnitRole.Hero or UnitRole.Titan)
            {
                return role == UnitRole.Titan ? race.GetHeroBySlot(1) : race.GetHeroBySlot(heroSlot);
            }

            if (bonusSlot >= 1 && bonusSlot <= 6)
            {
                return (ScriptableObject)(race.GetUnitBonus(role) ?? race.GetUnit(role));
            }

            return race.GetUnit(role);
        }

        static BalanceSourceInfo DefinedOrNone(UnitCombatSettings settings)
        {
            if (settings != null && settings.HeroDefinition != null)
            {
                return new BalanceSourceInfo(settings.HeroDefinition);
            }

            if (settings != null && settings.UnitDefinition != null)
            {
                return new BalanceSourceInfo(settings.UnitDefinition);
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
