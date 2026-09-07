using System;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>MVP combat math from <c>Units.md</c> / <c>Economy.md</c>.</summary>
    public static class CombatRules
    {
        public const float MinDamage = 1f;

        /// <summary>
        /// Melee bodies need a bit of reach beyond raw AttackRange to hit building surfaces
        /// when units stack around the footprint.
        /// </summary>
        public static float GetBuildingAttackReach(float attackRange) =>
            attackRange + MatchPickFootprint.DefaultUnitDiameter * 0.5f;

        /// <summary>
        /// Unit-vs-unit hit reach. Oversized titan body: attackers get at least
        /// <see cref="TitanRules.AttackRange"/> against a titan (same as the titan's own melee).
        /// </summary>
        public static float GetUnitAttackReach(float attackRange, UnitRole targetRole) =>
            targetRole == UnitRole.Titan
                ? Mathf.Max(attackRange, TitanRules.AttackRange)
                : attackRange;

        public static float RollDamage(float damageMin, float damageMax, System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (damageMax < damageMin)
            {
                (damageMin, damageMax) = (damageMax, damageMin);
            }

            var t = (float)random.NextDouble();
            return Mathf.Lerp(damageMin, damageMax, t);
        }

        public static float ApplyArmor(float rawDamage, float armor)
        {
            return Mathf.Max(MinDamage, rawDamage - armor);
        }

        public static int ComputeKillBounty(int baseBounty, bool isHero = false)
        {
            return isHero ? baseBounty * 2 : baseBounty;
        }

        public static bool CanAttackTarget(UnitRole attackerRole, UnitRole targetRole)
        {
            if (targetRole != UnitRole.Flying)
            {
                return true;
            }

            return attackerRole is UnitRole.Ranged
                or UnitRole.Flying
                or UnitRole.Caster
                or UnitRole.Hero
                or UnitRole.Titan;
        }

        public static float GetAttackIntervalSeconds(float attackSpeed)
        {
            return attackSpeed > 0f ? 1f / attackSpeed : float.MaxValue;
        }

        public static float GetAggroRadius(UnitCombatStats stats)
        {
            return Mathf.Max(stats.AttackRange * 2.5f, 8f);
        }

        /// <summary>
        /// Super artillery cannot fire inside this radius (inclusive band is
        /// <c>[min, max]</c> with max = AttackRange / building reach).
        /// </summary>
        public const float SuperMinAttackRange = 5f;

        public static float GetMinAttackRange(UnitRole attackerRole) =>
            attackerRole == UnitRole.Super ? SuperMinAttackRange : 0f;

        /// <summary>True when distance is inside the unit's fireable band (min..max).</summary>
        public static bool IsWithinAttackBand(
            float distance,
            float attackRange,
            UnitRole attackerRole,
            UnitRole targetRole)
        {
            var min = GetMinAttackRange(attackerRole);
            var max = GetUnitAttackReach(attackRange, targetRole);
            return distance >= min && distance <= max;
        }

        /// <summary>Race-aware fireable band (Faceless Super drops its dead-zone).</summary>
        public static bool IsWithinAttackBand(
            float distance,
            float attackRange,
            Data.UnitCombatIdentity identity,
            UnitRole targetRole)
        {
            var min = identity.GetMinAttackRange();
            var max = GetUnitAttackReach(attackRange, targetRole);
            return distance >= min && distance <= max;
        }

        /// <summary>Building attack band using surface distance vs building reach.</summary>
        public static bool IsWithinBuildingAttackBand(
            float surfaceDistance,
            float attackRange,
            UnitRole attackerRole)
        {
            var min = GetMinAttackRange(attackerRole);
            var max = GetBuildingAttackReach(attackRange);
            return surfaceDistance >= min && surfaceDistance <= max;
        }

        /// <summary>Building attack band for a race-aware identity (Faceless Super drops its dead-zone).</summary>
        public static bool IsWithinBuildingAttackBand(
            float surfaceDistance,
            float attackRange,
            Data.UnitCombatIdentity identity)
        {
            var min = identity.GetMinAttackRange();
            var max = GetBuildingAttackReach(attackRange);
            return surfaceDistance >= min && surfaceDistance <= max;
        }
    }
}
