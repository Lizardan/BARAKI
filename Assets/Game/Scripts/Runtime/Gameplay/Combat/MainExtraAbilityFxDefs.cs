using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Runtime FX-only defs for main Divine Blessing smites (not seeded onto unit prefabs).
    /// Host emit + client snapshot resolve when the unit ability catalog has no matching id.
    /// </summary>
    public static class MainExtraAbilityFxDefs
    {
        static UnitAbilityDef s_buildingSmite;
        static UnitAbilityDef s_unitSmite;
        static UnitAbilityDef s_iceRing;
        static UnitAbilityDef s_waveOfLight;

        public static int ToSpellAbilityId(int pickAbilityId) => pickAbilityId switch
        {
            MainExtraAbilityRules.BuildingSmiteId => AbilityIds.MainBuildingSmite,
            MainExtraAbilityRules.UnitSmiteId => AbilityIds.MainUnitSmite,
            _ => 0,
        };

        public static int ToPickAbilityId(int spellAbilityId) => spellAbilityId switch
        {
            AbilityIds.MainBuildingSmite => MainExtraAbilityRules.BuildingSmiteId,
            AbilityIds.MainUnitSmite => MainExtraAbilityRules.UnitSmiteId,
            _ => 0,
        };

        public static string GetDisplayName(int spellAbilityId) =>
            MainExtraAbilityRules.GetDisplayName(ToPickAbilityId(spellAbilityId));

        public static string GetEffectDescription(int spellAbilityId) =>
            MainExtraAbilityRules.GetEffectDescription(ToPickAbilityId(spellAbilityId));

        public static UnitAbilityDef GetForPick(int pickAbilityId)
        {
            var spellId = ToSpellAbilityId(pickAbilityId);
            return spellId == 0 ? null : Get(spellId);
        }

        /// <summary>Runtime def by BuildingAbilityRules id (MAIN-001: 1 = ice ring, 2 = wave).</summary>
        public static UnitAbilityDef GetForBuildingAbility(int buildingAbilityId)
        {
            var spellId = buildingAbilityId switch
            {
                BuildingAbilityRules.IceRingId => AbilityIds.MainIceRing,
                BuildingAbilityRules.WaveOfLightId => AbilityIds.MainWaveOfLight,
                _ => 0,
            };
            return spellId == 0 ? null : Get(spellId);
        }

        public static UnitAbilityDef Get(int spellAbilityId)
        {
            return spellAbilityId switch
            {
                AbilityIds.MainBuildingSmite => BuildingSmite(),
                AbilityIds.MainUnitSmite => UnitSmite(),
                AbilityIds.MainIceRing => IceRing(),
                AbilityIds.MainWaveOfLight => WaveOfLight(),
                _ => null,
            };
        }

        public static bool TryGet(int spellAbilityId, out UnitAbilityDef def)
        {
            def = Get(spellAbilityId);
            return def != null;
        }

        static UnitAbilityDef BuildingSmite()
        {
            if (s_buildingSmite == null)
            {
                s_buildingSmite = CreateRuntimeDef(
                    AbilityIds.MainBuildingSmite,
                    MainExtraAbilityRules.BuildingSmiteId,
                    MainExtraAbilityRules.BuildingSmiteDamage,
                    radius: 2.2f);
            }

            ApplyCatalogFx(s_buildingSmite, AbilityIds.MainBuildingSmite);
            return s_buildingSmite;
        }

        static UnitAbilityDef UnitSmite()
        {
            if (s_unitSmite == null)
            {
                s_unitSmite = CreateRuntimeDef(
                    AbilityIds.MainUnitSmite,
                    MainExtraAbilityRules.UnitSmiteId,
                    MainExtraAbilityRules.UnitSmiteDamage,
                    radius: 1.6f);
            }

            ApplyCatalogFx(s_unitSmite, AbilityIds.MainUnitSmite);
            return s_unitSmite;
        }

        static UnitAbilityDef IceRing()
        {
            if (s_iceRing == null)
            {
                s_iceRing = CreateBuildingAbilityDef(
                    AbilityIds.MainIceRing,
                    BuildingAbilityRules.IceRingId,
                    radius: BuildingAbilityRules.IceRingRadius);
            }

            ApplyCatalogFx(s_iceRing, AbilityIds.MainIceRing);
            return s_iceRing;
        }

        static UnitAbilityDef WaveOfLight()
        {
            if (s_waveOfLight == null)
            {
                // Radius is dynamic (base→barracks × factor); carried by the cast event.
                s_waveOfLight = CreateBuildingAbilityDef(
                    AbilityIds.MainWaveOfLight,
                    BuildingAbilityRules.WaveOfLightId,
                    radius: 0f);
            }

            ApplyCatalogFx(s_waveOfLight, AbilityIds.MainWaveOfLight);
            return s_waveOfLight;
        }

        static UnitAbilityDef CreateBuildingAbilityDef(int spellAbilityId, int buildingAbilityId, float radius)
        {
            var fallbackColor = buildingAbilityId == BuildingAbilityRules.WaveOfLightId
                ? AbilityFxColors.Priest
                : AbilityFxColors.Frost;
            var def = UnitAbilityDef.Create(
                spellAbilityId,
                BuildingAbilityRules.GetDisplayName(buildingAbilityId),
                BuildingAbilityRules.GetEffectDescription(buildingAbilityId),
                AbilityKind.Active,
                AbilityUnlock.Always,
                unlockValue: 1,
                behaviour: null,
                fx: new AbilityFx { Color = fallbackColor },
                damage: BuildingAbilityRules.GetDamage(buildingAbilityId),
                radius: radius,
                cooldownSeconds: BuildingAbilityRules.GetCooldownSeconds(buildingAbilityId),
                manaCost: BuildingAbilityRules.GetManaCost(buildingAbilityId));
            def.hideFlags = HideFlags.HideAndDontSave;
            return def;
        }

        static UnitAbilityDef CreateRuntimeDef(int spellAbilityId, int pickAbilityId, float damage, float radius)
        {
            var def = UnitAbilityDef.Create(
                spellAbilityId,
                MainExtraAbilityRules.GetDisplayName(pickAbilityId),
                MainExtraAbilityRules.GetEffectDescription(pickAbilityId),
                AbilityKind.Active,
                AbilityUnlock.Always,
                unlockValue: 1,
                behaviour: null,
                fx: new AbilityFx { Color = AbilityFxColors.DivineSmite },
                damage: damage,
                radius: radius,
                cooldownSeconds: MainExtraAbilityRules.CooldownSeconds,
                manaCost: MainExtraAbilityRules.ManaCost);
            def.hideFlags = HideFlags.HideAndDontSave;
            return def;
        }

        static void ApplyCatalogFx(UnitAbilityDef def, int abilityId)
        {
            var catalog = MainExtraAbilityFxCatalog.Load();
            if (catalog == null || def == null)
            {
                return;
            }

            def.ApplyFx(catalog.GetFx(abilityId));
        }
    }
}
