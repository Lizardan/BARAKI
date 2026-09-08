using Game.Core;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Which combat animator state an active ability should drive.</summary>
    public enum AbilityAnimKind
    {
        /// <summary>Not authored yet; runtime/viewer take <see cref="AbilityAnimRules.ResolveKind"/>.</summary>
        Unspecified = 0,
        /// <summary>Stand — no attack/cast clip.</summary>
        None = 1,
        /// <summary>Attack animator state (optional authored BlendTree child via <c>AnimVariant</c>).</summary>
        Attack = 2,
        /// <summary>Cast animator state (optional authored BlendTree child via <c>AnimVariant</c>).</summary>
        Cast = 3,
    }

    /// <summary>
    /// Maps ability ids to cast/attack animation kinds and cast-lock duration.
    /// Clip lengths match ToonyTinyPeople TT_RTS authored clips (measured in Editor).
    /// </summary>
    public static class AbilityAnimRules
    {
        /// <summary>TT infantry/staff attack &amp; cast clips (A/B).</summary>
        public const float InfantryAttackClipSeconds = 1.5f;

        /// <summary>TT cavalry / ballista / punch clips.</summary>
        public const float CavalryOrMachineAttackClipSeconds = 1f;

        /// <summary>Staff cast A/B and cav_staff cast B (use max so A/B lock covers longer).</summary>
        public const float StaffCastClipSeconds = 1.5f;

        /// <summary>Titan Rally punch A/B.</summary>
        public const float PunchCastClipSeconds = 1f;

        public static AbilityAnimKind ResolveKind(int abilityId) =>
            abilityId switch
            {
                AbilityIds.Strike
                    or AbilityIds.Ultimate
                    or AbilityIds.Smite
                    or AbilityIds.Consecration
                    or AbilityIds.Slam
                    or AbilityIds.Stomp
                    or AbilityIds.MeleeCleave
                    or AbilityIds.RangedCrit
                    or AbilityIds.CasterHybrid
                    or AbilityIds.SuperCatapult => AbilityAnimKind.Attack,

                AbilityIds.CasterHeal
                    or AbilityIds.Frost
                    or AbilityIds.Resurrect
                    or AbilityIds.Heal
                    or AbilityIds.HolyNova
                    or AbilityIds.GreaterHeal
                    or AbilityIds.Revive
                    or AbilityIds.Shield
                    or AbilityIds.Rally
                    or AbilityIds.BlightingGaze
                    or AbilityIds.VoidDrain
                    or AbilityIds.RaiseDrowned => AbilityAnimKind.Cast,

                _ => AbilityAnimKind.None,
            };

        public static AbilityAnimKind ResolveKindFromState(string animState)
        {
            if (string.IsNullOrEmpty(animState))
            {
                return AbilityAnimKind.Unspecified;
            }

            if (animState == "Attack")
            {
                return AbilityAnimKind.Attack;
            }

            if (animState == "Cast")
            {
                return AbilityAnimKind.Cast;
            }

            return AbilityAnimKind.None;
        }

        public static AbilityAnimKind ResolveAnim(
            int abilityId,
            AbilityAnimKind authored,
            string animState = null)
        {
            var fromState = ResolveKindFromState(animState);
            if (fromState != AbilityAnimKind.Unspecified)
            {
                return fromState;
            }

            return authored != AbilityAnimKind.Unspecified
                ? authored
                : ResolveKind(abilityId);
        }

        public static string ResolveDefaultState(int abilityId) =>
            ResolveKind(abilityId) switch
            {
                AbilityAnimKind.Attack => "Attack",
                AbilityAnimKind.Cast => "Cast",
                _ => string.Empty,
            };

        /// <summary>
        /// Authored Attack clip length for this unit. Used as
        /// <c>Animator.speed = clipLength / attackInterval</c> so mid-clip impact stays mid-clip
        /// when attack speed (and Haste Aura) change.
        /// Faceless cannot reuse TT_RTS lengths: their Attack clips were baked from MDX v800
        /// sequences and differ per role (measured in Editor from Production/Anim).
        /// </summary>
        public static float ResolveAttackClipSeconds(
            UnitRole role,
            int heroSlot = 0,
            int bonusSlot = 0,
            string raceId = null)
        {
            if (raceId == GameIds.Races.Faceless)
            {
                return role switch
                {
                    UnitRole.Melee => 0.99f,
                    UnitRole.Ranged => 1.49f,
                    UnitRole.Caster => 1.06f,
                    UnitRole.Siege => 0.96f,
                    UnitRole.Flying => 0.99f,
                    UnitRole.Super => 0.99f,
                    UnitRole.Hero => heroSlot switch
                    {
                        1 => 0.96f,
                        2 => 1.06f,
                        _ => 1.16f,
                    },
                    UnitRole.Titan => 1.32f,
                    _ => CavalryOrMachineAttackClipSeconds,
                };
            }

            if (role == UnitRole.Hero && heroSlot == HeroAbilityRules.KingSlot)
            {
                return InfantryAttackClipSeconds;
            }

            // Siege BONUS is foot Paladin (infantry Shield); base Siege stays mounted cavalry.
            if (role == UnitRole.Siege
                && bonusSlot == HumanBonusUnitRules.BonusSlotForRole(UnitRole.Siege))
            {
                return InfantryAttackClipSeconds;
            }

            return role switch
            {
                UnitRole.Melee or UnitRole.Ranged or UnitRole.Caster or UnitRole.Titan =>
                    InfantryAttackClipSeconds,
                _ => CavalryOrMachineAttackClipSeconds,
            };
        }

        /// <summary>Host cast-lock duration at animator speed 1 (full Cast clip).</summary>
        public static float ResolveCastLockSeconds(int abilityId) =>
            abilityId == AbilityIds.Rally ? PunchCastClipSeconds : StaffCastClipSeconds;

        public static float ResolveLockSeconds(
            AbilityAnimKind kind,
            float attackIntervalSeconds,
            int abilityId = 0) =>
            kind switch
            {
                AbilityAnimKind.Cast => ResolveCastLockSeconds(abilityId),
                AbilityAnimKind.Attack => Mathf.Max(0.05f, attackIntervalSeconds),
                _ => 0f,
            };
    }
}
