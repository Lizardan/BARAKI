using System;
using Game.Gameplay.Data;
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
                or UnitRole.Hero;
        }

        public static float GetAttackIntervalSeconds(float attackSpeed)
        {
            return attackSpeed > 0f ? 1f / attackSpeed : float.MaxValue;
        }

        public static float GetAggroRadius(UnitCombatStats stats)
        {
            return Mathf.Max(stats.AttackRange * 2.5f, 8f);
        }
    }
}
