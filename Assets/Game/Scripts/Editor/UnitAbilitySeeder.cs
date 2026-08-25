using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Writes shared <see cref="UnitAbilityDef"/> references (from the catalog built by
    /// <see cref="UnitAbilityAssetBuilder"/>) into hero/caster/titan combat settings. Does not run as part of
    /// BARAKI/Units/Sync Balance to Prefabs because balance snapshots and ability references are
    /// synchronized independently.
    /// </summary>
    public static class UnitAbilitySeeder
    {
        [MenuItem("BARAKI/Units/Seed Unit Abilities")]
        public static void SeedAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(UnitAbilityAssetBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"UnitAbilitySeeder: catalog not found at {UnitAbilityAssetBuilder.CatalogPath}. " +
                               "Run BARAKI/Abilities/Build Ability Defs first.");
                return;
            }

            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (visualCatalog == null)
            {
                Debug.LogError($"UnitAbilitySeeder: visual catalog not found at {UnitVisualPrefabBuilder.CatalogPath}.");
                return;
            }

            var seeded = 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Hero, 1, AbilityKitDefaults.CreateKing()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Hero, 2, AbilityKitDefaults.CreatePaladin()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Hero, 3, AbilityKitDefaults.CreatePriest()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Caster, 0, AbilityKitDefaults.CreateCaster()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Titan, 0, AbilityKitDefaults.CreateTitan()) ? 1 : 0;

            for (var bonusSlot = HumanBonusUnitRules.Hero1BonusSlot;
                 bonusSlot <= HumanBonusUnitRules.TitanBonusSlot;
                 bonusSlot++)
            {
                var isTitan = HumanBonusUnitRules.IsTitanBonusSlot(bonusSlot);
                seeded += Seed(
                    visualCatalog,
                    catalog,
                    isTitan ? UnitRole.Titan : UnitRole.Hero,
                    isTitan ? 0 : HumanBonusUnitRules.HeroSlotForBonusSlot(bonusSlot),
                    AbilityKitDefaults.CreateVeteranKit(bonusSlot),
                    bonusSlot)
                    ? 1
                    : 0;
            }

            foreach (var role in new[]
                     {
                         UnitRole.Melee,
                         UnitRole.Ranged,
                         UnitRole.Caster,
                         UnitRole.Siege,
                         UnitRole.Flying,
                         UnitRole.Super,
                     })
            {
                seeded += Seed(
                    visualCatalog,
                    catalog,
                    role,
                    0,
                    AbilityKitDefaults.CreateBonus(role),
                    bonusSlot: HumanBonusUnitRules.BonusSlotForRole(role))
                    ? 1
                    : 0;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"UnitAbilitySeeder: seeded {seeded} prefab(s).");
        }

        static bool Seed(
            UnitVisualCatalog catalog,
            UnitAbilityCatalog abilityCatalog,
            UnitRole role,
            int heroSlot,
            UnitAbilityDef[] defaults,
            int bonusSlot = 0)
        {
            if (!catalog.TryGetPrefab(GameIds.Races.Human, role, heroSlot, bonusSlot, out var prefab)
                || prefab == null)
            {
                Debug.LogWarning(
                    $"UnitAbilitySeeder: no prefab for {role} slot {heroSlot} bonus {bonusSlot}.");
                return false;
            }

            var assets = new UnitAbilityDef[defaults.Length];
            for (var i = 0; i < defaults.Length; i++)
            {
                var def = defaults[i];
                assets[i] = def != null ? abilityCatalog.Find(def.AbilityId) : null;
                if (assets[i] == null)
                {
                    Debug.LogWarning($"UnitAbilitySeeder: catalog has no def for id {def?.AbilityId ?? 0} ({role} slot {heroSlot}).");
                }
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var settings = root.GetComponentInChildren<UnitCombatSettings>(true);
                if (settings == null)
                {
                    settings = root.AddComponent<UnitCombatSettings>();
                }

                settings.ReplaceAbilities(assets);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
