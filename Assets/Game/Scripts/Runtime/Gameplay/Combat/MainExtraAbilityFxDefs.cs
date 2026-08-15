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

        public static int ToSpellAbilityId(int pickAbilityId) => pickAbilityId switch
        {
            MainExtraAbilityRules.BuildingSmiteId => AbilityIds.MainBuildingSmite,
            MainExtraAbilityRules.UnitSmiteId => AbilityIds.MainUnitSmite,
            _ => 0,
        };

        public static UnitAbilityDef GetForPick(int pickAbilityId)
        {
            var spellId = ToSpellAbilityId(pickAbilityId);
            return spellId == 0 ? null : Get(spellId);
        }

        public static UnitAbilityDef Get(int spellAbilityId)
        {
            return spellAbilityId switch
            {
                AbilityIds.MainBuildingSmite => BuildingSmite(),
                AbilityIds.MainUnitSmite => UnitSmite(),
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
            if (s_buildingSmite != null)
            {
                return s_buildingSmite;
            }

            s_buildingSmite = UnitAbilityDef.Create(
                AbilityIds.MainBuildingSmite,
                MainExtraAbilityRules.GetDisplayName(MainExtraAbilityRules.BuildingSmiteId),
                MainExtraAbilityRules.GetEffectDescription(MainExtraAbilityRules.BuildingSmiteId),
                AbilityKind.Active,
                AbilityUnlock.Always,
                unlockValue: 1,
                behaviour: null,
                fx: AbilityFx.SkyBeam(AbilityFxColors.DivineSmite, duration: 1.15f, height: 40f),
                damage: MainExtraAbilityRules.BuildingSmiteDamage,
                radius: 2.2f,
                cooldownSeconds: MainExtraAbilityRules.CooldownSeconds,
                manaCost: MainExtraAbilityRules.ManaCost);
            return s_buildingSmite;
        }

        static UnitAbilityDef UnitSmite()
        {
            if (s_unitSmite != null)
            {
                return s_unitSmite;
            }

            s_unitSmite = UnitAbilityDef.Create(
                AbilityIds.MainUnitSmite,
                MainExtraAbilityRules.GetDisplayName(MainExtraAbilityRules.UnitSmiteId),
                MainExtraAbilityRules.GetEffectDescription(MainExtraAbilityRules.UnitSmiteId),
                AbilityKind.Active,
                AbilityUnlock.Always,
                unlockValue: 1,
                behaviour: null,
                fx: AbilityFx.SkyBeam(AbilityFxColors.DivineSmite, duration: 1.15f, height: 40f),
                damage: MainExtraAbilityRules.UnitSmiteDamage,
                radius: 1.6f,
                cooldownSeconds: MainExtraAbilityRules.CooldownSeconds,
                manaCost: MainExtraAbilityRules.ManaCost);
            return s_unitSmite;
        }
    }
}
