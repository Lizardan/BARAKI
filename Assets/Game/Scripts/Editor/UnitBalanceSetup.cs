using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Copies the current human UnitDefinition / HeroDefinition values onto the unit prefabs as
    /// read-only UnitCombatSettings runtime snapshots. Balance is edited on the definition assets;
    /// run this sync after a change so runtime prefab values stay current.
    /// </summary>
    public static class UnitBalanceSetup
    {
        public const string RaceCatalogPath = ContentAssetPaths.RaceCatalog;

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

            var synced = new List<string>();
            SyncRace(raceCatalog, visualCatalog, GameIds.Races.Human, includeHumanExtras: true, synced);
            AssetDatabase.SaveAssets();
            Debug.Log($"UnitBalanceSetup: synced {synced.Count} prefab(s).\n{string.Join("\n", synced)}");
        }

        [MenuItem("BARAKI/Faceless/Sync Balance to Prefabs")]
        public static void SyncFaceless()
        {
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(RaceCatalogPath);
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (raceCatalog == null || visualCatalog == null)
            {
                Debug.LogError($"UnitBalanceSetup: catalogs not found (race={raceCatalog} visual={visualCatalog}).");
                return;
            }

            var race = raceCatalog.GetRace(GameIds.Races.Faceless);
            if (race == null)
            {
                Debug.LogError($"UnitBalanceSetup: race '{GameIds.Races.Faceless}' not found in {RaceCatalogPath}.");
                return;
            }

            var synced = new List<string>();
            SyncRace(raceCatalog, visualCatalog, GameIds.Races.Faceless, includeHumanExtras: false, synced);
            AssetDatabase.SaveAssets();
            Debug.Log($"UnitBalanceSetup: synced {synced.Count} Faceless prefab(s).\n{string.Join("\n", synced)}");
        }

        static void SyncRace(
            RaceCatalog raceCatalog,
            UnitVisualCatalog visualCatalog,
            string raceId,
            bool includeHumanExtras,
            List<string> synced)
        {
            var race = raceCatalog.GetRace(raceId);
            if (race == null)
            {
                Debug.LogError($"UnitBalanceSetup: race '{raceId}' not found in {RaceCatalogPath}.");
                return;
            }

            foreach (var role in Roles)
            {
                var definition = race.GetUnit(role);
                if (definition == null)
                {
                    Debug.LogWarning($"UnitBalanceSetup: no UnitDefinition for role {role} ({raceId}).");
                    continue;
                }

                if (!TrySyncPrefab(
                        visualCatalog,
                        raceId,
                        role,
                        heroSlot: 0,
                        settings => settings.CopyFrom(definition),
                        out var path))
                {
                    continue;
                }

                synced.Add($"{raceId} {role}: {path}");
            }

            // Bonus units 1–6 come from race-specific bonus definitions (Human-authored).
            // Faceless bonus units are marker-only clones of the base units and keep the base stats.
            if (includeHumanExtras)
            {
                for (var bonusSlot = 1; bonusSlot <= 6; bonusSlot++)
                {
                    var role = BonusKitRules.RoleForBonusSlot(bonusSlot);
                    var definition = race.GetUnitBonus(role);
                    if (definition == null)
                    {
                        continue;
                    }

                    if (!TrySyncPrefab(
                            visualCatalog,
                            raceId,
                            role,
                            heroSlot: 0,
                            settings => settings.CopyFrom(definition),
                            out var path,
                            bonusSlot))
                    {
                        continue;
                    }

                    synced.Add($"{role} BONUS ({raceId}): {path}");
                }
            }

            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                var hero = race.GetHeroBySlot(slot);
                if (hero == null)
                {
                    Debug.LogWarning($"UnitBalanceSetup: no HeroDefinition for slot {slot} ({raceId}).");
                    continue;
                }

                if (!TrySyncPrefab(
                        visualCatalog,
                        raceId,
                        UnitRole.Hero,
                        slot,
                        settings => settings.CopyFrom(hero),
                        out var path))
                {
                    continue;
                }

                synced.Add($"Hero{slot} ({raceId}): {path}");
            }

            var titan = race.GetHeroBySlot(1);
            if (titan == null)
            {
                Debug.LogWarning($"UnitBalanceSetup: no HeroDefinition for titan seed (slot 1, {raceId}).");
            }
            else if (TrySyncPrefab(
                         visualCatalog,
                         raceId,
                         UnitRole.Titan,
                         heroSlot: 0,
                         settings => settings.CopyFrom(
                             titan,
                             TitanRules.BaseStatMultiplier,
                             TitanRules.AttackRange),
                         out var titanPath))
            {
                synced.Add($"Titan ({raceId}): {titanPath}");
            }

            // Veteran champions (bonus slots 7–10, PRE-006b / FACELESS-012): both races sync here.
            for (var bonusSlot = BonusKitRules.Hero1BonusSlot;
                 bonusSlot <= BonusKitRules.TitanBonusSlot;
                 bonusSlot++)
            {
                var isTitan = BonusKitRules.IsTitanBonusSlot(bonusSlot);
                var heroSlot = isTitan ? 1 : BonusKitRules.HeroSlotForBonusSlot(bonusSlot);
                var baseHero = race.GetHeroBySlot(heroSlot);
                if (baseHero == null)
                {
                    Debug.LogWarning($"UnitBalanceSetup: no HeroDefinition for veteran slot {bonusSlot}.");
                    continue;
                }

                float? rangeOverride = isTitan ? TitanRules.AttackRange : null;
                // The titan veteran kit multiplies the 3× titan seed (hero1 × BaseStatMultiplier),
                // not the raw hero1 numbers — HP ×1.4, damage ×1.35, armor +2 on top of it.
                var titanScale = isTitan ? TitanRules.BaseStatMultiplier : 1f;
                var hpMultiplier = titanScale * BonusKitRules.VeteranHpMultiplier;
                var damageMultiplier = titanScale * BonusKitRules.VeteranDamageMultiplier;
                var armorBonus = baseHero.Armor * (titanScale - 1f) + BonusKitRules.VeteranArmorBonus;
                if (!TrySyncPrefab(
                        visualCatalog,
                        raceId,
                        isTitan ? UnitRole.Titan : UnitRole.Hero,
                        isTitan ? 0 : heroSlot,
                        settings => settings.CopyFromVeteran(
                            baseHero,
                            hpMultiplier,
                            damageMultiplier,
                            armorBonus,
                            rangeOverride),
                        out var veteranPath,
                        bonusSlot))
                {
                    continue;
                }

                synced.Add($"Veteran slot {bonusSlot} ({raceId}): {veteranPath}");
            }
        }

        static bool TrySyncPrefab(
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role,
            int heroSlot,
            System.Action<UnitCombatSettings> apply,
            out string path,
            int bonusSlot = 0)
        {
            path = null;
            if (!visualCatalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var prefab) || prefab == null)
            {
                Debug.LogWarning(
                    $"UnitBalanceSetup: no prefab for {role} slot {heroSlot} bonus {bonusSlot}.");
                return false;
            }

            path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var settings = root.GetComponentInChildren<UnitCombatSettings>(true);
                if (settings == null)
                {
                    settings = root.AddComponent<UnitCombatSettings>();
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
