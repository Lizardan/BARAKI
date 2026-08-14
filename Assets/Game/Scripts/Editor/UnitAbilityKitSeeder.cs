using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Writes default ability kits onto hero/caster/titan prefabs. Does not run as part of
    /// BARAKI/Units/Sync Balance to Prefabs so later tuning on the prefab is kept.
    /// </summary>
    public static class UnitAbilityKitSeeder
    {
        [MenuItem("BARAKI/Units/Seed Ability Kits")]
        public static void SeedAll()
        {
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (visualCatalog == null)
            {
                Debug.LogError($"UnitAbilityKitSeeder: catalog not found at {UnitVisualPrefabBuilder.CatalogPath}.");
                return;
            }

            var seeded = 0;
            seeded += Seed(visualCatalog, UnitRole.Hero, 1, AbilityKitDefaults.CreateKing()) ? 1 : 0;
            seeded += Seed(visualCatalog, UnitRole.Hero, 2, AbilityKitDefaults.CreatePaladin()) ? 1 : 0;
            seeded += Seed(visualCatalog, UnitRole.Hero, 3, AbilityKitDefaults.CreatePriest()) ? 1 : 0;
            seeded += Seed(visualCatalog, UnitRole.Caster, 0, AbilityKitDefaults.CreateCaster()) ? 1 : 0;
            seeded += Seed(visualCatalog, UnitRole.Titan, 0, AbilityKitDefaults.CreateTitan()) ? 1 : 0;
            AssetDatabase.SaveAssets();
            Debug.Log($"UnitAbilityKitSeeder: seeded {seeded} prefab(s).");
        }

        static bool Seed(UnitVisualCatalog catalog, UnitRole role, int heroSlot, UnitAbilitySlot[] slots)
        {
            if (!catalog.TryGetPrefab(GameIds.Races.Human, role, heroSlot, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"UnitAbilityKitSeeder: no prefab for {role} slot {heroSlot}.");
                return false;
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

                kit.ReplaceSlots(slots);
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
