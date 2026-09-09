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

            for (var bonusSlot = BonusKitRules.Hero1BonusSlot;
                 bonusSlot <= BonusKitRules.TitanBonusSlot;
                 bonusSlot++)
            {
                var isTitan = BonusKitRules.IsTitanBonusSlot(bonusSlot);
                seeded += Seed(
                    visualCatalog,
                    catalog,
                    isTitan ? UnitRole.Titan : UnitRole.Hero,
                    isTitan ? 0 : BonusKitRules.HeroSlotForBonusSlot(bonusSlot),
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
                    bonusSlot: BonusKitRules.BonusSlotForRole(role))
                    ? 1
                    : 0;
            }

            // Faceless: mirror Humans — seed readable spells onto the (now existing) Faceless prefabs.
            seeded += SeedFaceless(visualCatalog, catalog);

            AssetDatabase.SaveAssets();
            Debug.Log($"UnitAbilitySeeder: seeded {seeded} prefab(s).");
        }

        /// <summary>
        /// Seeds Faceless prefabs with the readable-spell kits:
        /// caster kit (FACELESS-016), unit bonus marker kits (slots 1–6, FACELESS-011), champion
        /// veteran kits (slots 7–10, FACELESS-012) and the shared base hero/titan kits on the base
        /// hero/titan prefabs (FACELESS-012 canon: a race's base hero = the base kit, veteran =
        /// base with one signature replaced). The runtime reads abilities from the prefab settings
        /// (authoritative); the <see cref="AbilityKitDefaults.CreateForSpawn"/> fallback stays
        /// gated for Faceless non-caster roles (FACELESS-014).
        /// </summary>
        static int SeedFaceless(UnitVisualCatalog visualCatalog, UnitAbilityCatalog abilityCatalog)
        {
            var seeded = 0;
            var roles = new[]
            {
                UnitRole.Melee,
                UnitRole.Ranged,
                UnitRole.Caster,
                UnitRole.Siege,
                UnitRole.Flying,
                UnitRole.Super,
            };

            foreach (var role in roles)
            {
                if (Seed(
                        visualCatalog,
                        abilityCatalog,
                        GameIds.Races.Faceless,
                        role,
                        0,
                        AbilityKitDefaults.CreateForSpawn(GameIds.Races.Faceless, role, 0, 0),
                        bonusSlot: 0))
                {
                    seeded++;
                }
            }

            // Base heroes/titan carry the shared base kits (same Human defs; FACELESS-012 canon).
            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                if (Seed(
                        visualCatalog,
                        abilityCatalog,
                        GameIds.Races.Faceless,
                        UnitRole.Hero,
                        slot,
                        AbilityKitDefaults.Create(UnitRole.Hero, slot),
                        bonusSlot: 0))
                {
                    seeded++;
                }
            }

            if (Seed(
                    visualCatalog,
                    abilityCatalog,
                    GameIds.Races.Faceless,
                    UnitRole.Titan,
                    0,
                    AbilityKitDefaults.Create(UnitRole.Titan, 0),
                    bonusSlot: 0))
            {
                seeded++;
            }

            for (var bonusSlot = 1; bonusSlot <= 10; bonusSlot++)
            {
                UnitRole role;
                int heroSlot;
                if (BonusKitRules.IsBonusSlot(bonusSlot))
                {
                    role = BonusKitRules.RoleForBonusSlot(bonusSlot);
                    heroSlot = 0;
                }
                else if (BonusKitRules.IsHeroBonusSlot(bonusSlot))
                {
                    role = UnitRole.Hero;
                    heroSlot = BonusKitRules.HeroSlotForBonusSlot(bonusSlot);
                }
                else
                {
                    role = UnitRole.Titan;
                    heroSlot = 0;
                }

                if (Seed(
                        visualCatalog,
                        abilityCatalog,
                        GameIds.Races.Faceless,
                        role,
                        heroSlot,
                        AbilityKitDefaults.CreateForSpawn(GameIds.Races.Faceless, role, heroSlot, bonusSlot),
                        bonusSlot))
                {
                    seeded++;
                }
            }

            return seeded;
        }

        static bool Seed(
            UnitVisualCatalog catalog,
            UnitAbilityCatalog abilityCatalog,
            UnitRole role,
            int heroSlot,
            UnitAbilityDef[] defaults,
            int bonusSlot = 0)
        {
            return Seed(catalog, abilityCatalog, GameIds.Races.Human, role, heroSlot, defaults, bonusSlot);
        }

        static bool Seed(
            UnitVisualCatalog catalog,
            UnitAbilityCatalog abilityCatalog,
            string raceId,
            UnitRole role,
            int heroSlot,
            UnitAbilityDef[] defaults,
            int bonusSlot = 0)
        {
            if (!catalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var prefab)
                || prefab == null)
            {
                Debug.LogWarning(
                    $"UnitAbilitySeeder: no prefab for {raceId} {role} slot {heroSlot} bonus {bonusSlot}.");
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
