using System.Collections.Generic;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Copies the current human UnitDefinition / HeroDefinition values onto the unit prefabs as
    /// UnitBalanceSettings components (migration / reset). After that, balance is
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

                if (!TrySyncPrefab(
                        visualCatalog,
                        GameIds.Races.Human,
                        role,
                        heroSlot: 0,
                        settings => settings.CopyFrom(definition),
                        out var path))
                {
                    continue;
                }

                synced.Add($"{role}: {path}");
            }

            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                var hero = race.GetHeroBySlot(slot);
                if (hero == null)
                {
                    Debug.LogWarning($"UnitBalanceSetup: no HeroDefinition for slot {slot}.");
                    continue;
                }

                if (!TrySyncPrefab(
                        visualCatalog,
                        GameIds.Races.Human,
                        UnitRole.Hero,
                        slot,
                        settings => settings.CopyFrom(hero),
                        out var path))
                {
                    continue;
                }

                synced.Add($"Hero{slot}: {path}");
            }

            var titanBase = race.GetHeroBySlot(1);
            if (titanBase == null)
            {
                Debug.LogWarning("UnitBalanceSetup: no HeroDefinition for titan seed (slot 1).");
            }
            else if (TrySyncPrefab(
                         visualCatalog,
                         GameIds.Races.Human,
                         UnitRole.Titan,
                         heroSlot: 0,
                         settings => settings.CopyFrom(titanBase, TitanRules.BaseStatMultiplier),
                         out var titanPath))
            {
                synced.Add($"Titan: {titanPath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"UnitBalanceSetup: synced {synced.Count} prefab(s).\n{string.Join("\n", synced)}");
        }

        static bool TrySyncPrefab(
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role,
            int heroSlot,
            System.Action<UnitBalanceSettings> apply,
            out string path)
        {
            path = null;
            if (!visualCatalog.TryGetPrefab(raceId, role, heroSlot, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"UnitBalanceSetup: no prefab for {role} slot {heroSlot}.");
                return false;
            }

            path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var settings = root.GetComponentInChildren<UnitBalanceSettings>(true);
                if (settings == null)
                {
                    settings = root.AddComponent<UnitBalanceSettings>();
                }

                apply(settings);
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
