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
    /// <see cref="UnitAbilityAssetBuilder"/>) onto hero/caster/titan prefabs. Does not run as part of
    /// BARAKI/Units/Sync Balance to Prefabs so later tuning on the prefab is kept.
    /// </summary>
    public static class UnitAbilityKitSeeder
    {
        [MenuItem("BARAKI/Units/Seed Ability Kits")]
        public static void SeedAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(UnitAbilityAssetBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"UnitAbilityKitSeeder: catalog not found at {UnitAbilityAssetBuilder.CatalogPath}. " +
                               "Run BARAKI/Abilities/Build Ability Defs first.");
                return;
            }

            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (visualCatalog == null)
            {
                Debug.LogError($"UnitAbilityKitSeeder: visual catalog not found at {UnitVisualPrefabBuilder.CatalogPath}.");
                return;
            }

            var seeded = 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Hero, 1, AbilityKitDefaults.CreateKing()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Hero, 2, AbilityKitDefaults.CreatePaladin()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Hero, 3, AbilityKitDefaults.CreatePriest()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Caster, 0, AbilityKitDefaults.CreateCaster()) ? 1 : 0;
            seeded += Seed(visualCatalog, catalog, UnitRole.Titan, 0, AbilityKitDefaults.CreateTitan()) ? 1 : 0;
            AssetDatabase.SaveAssets();
            Debug.Log($"UnitAbilityKitSeeder: seeded {seeded} prefab(s).");
        }

        static bool Seed(
            UnitVisualCatalog catalog,
            UnitAbilityCatalog abilityCatalog,
            UnitRole role,
            int heroSlot,
            UnitAbilityDef[] defaults)
        {
            if (!catalog.TryGetPrefab(GameIds.Races.Human, role, heroSlot, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"UnitAbilityKitSeeder: no prefab for {role} slot {heroSlot}.");
                return false;
            }

            var assets = new UnitAbilityDef[defaults.Length];
            for (var i = 0; i < defaults.Length; i++)
            {
                var def = defaults[i];
                assets[i] = def != null ? abilityCatalog.Find(def.AbilityId) : null;
                if (assets[i] == null)
                {
                    Debug.LogWarning($"UnitAbilityKitSeeder: catalog has no def for id {def?.AbilityId ?? 0} ({role} slot {heroSlot}).");
                }
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var kit = root.GetComponentInChildren<UnitAbilityKit>(true);
                if (kit == null)
                {
                    kit = root.AddComponent<UnitAbilityKit>();
                }

                kit.ReplaceAbilities(assets);
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
