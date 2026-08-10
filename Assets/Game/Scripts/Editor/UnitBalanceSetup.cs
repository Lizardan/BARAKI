using System.Collections.Generic;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Copies the current human UnitDefinition values onto the unit prefabs as
    /// UnitBalanceSettings components (single migration step). After that, balance is
    /// tuned directly on the prefabs and runtime uses the prefab values.
    /// </summary>
    public static class UnitBalanceSetup
    {
        public const string RaceCatalogPath = "Assets/Game/ScriptableObjects/RaceCatalog.asset";

        static readonly UnitRole[] Roles =
        {
            UnitRole.Melee,
            UnitRole.Ranged,
            UnitRole.Caster,
            UnitRole.Siege,
            UnitRole.Flying,
            UnitRole.Super,
        };

        [MenuItem("BARAKI/Units/Sync Balance to Prefabs")]
        public static void SyncAll()
        {
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(RaceCatalogPath);
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (raceCatalog == null || visualCatalog == null)
            {
                Debug.LogError($"UnitBalanceSetup: catalogs not found (race={raceCatalog} visual={visualCatalog}).");
                return;
            }

            var race = raceCatalog.GetRace(GameIds.Races.Human);
            if (race == null)
            {
                Debug.LogError($"UnitBalanceSetup: race '{GameIds.Races.Human}' not found in {RaceCatalogPath}.");
                return;
            }

            var synced = new List<string>();
            foreach (var role in Roles)
            {
                var definition = race.GetUnit(role);
                if (definition == null)
                {
                    Debug.LogWarning($"UnitBalanceSetup: no UnitDefinition for role {role}.");
                    continue;
                }

                if (!visualCatalog.TryGetPrefab(GameIds.Races.Human, role, out var prefab) || prefab == null)
                {
                    Debug.LogWarning($"UnitBalanceSetup: no prefab for role {role}.");
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var settings = root.GetComponentInChildren<UnitBalanceSettings>(true);
                    if (settings == null)
                    {
                        settings = root.AddComponent<UnitBalanceSettings>();
                    }

                    settings.CopyFrom(definition);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    synced.Add($"{role}: {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"UnitBalanceSetup: synced {synced.Count} prefab(s).\n{string.Join("\n", synced)}");
        }
    }
}
