using System;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Pure rules for the six Human unit bonuses (PRE-006a): bonus slot ↔ role mapping
    /// and combat-mechanic tuning constants.
    /// </summary>
    public static class HumanBonusUnitRules
    {
        /// <summary>First unit bonus slot (Melee).</summary>
        public const int MinBonusSlot = 1;

        /// <summary>Last unit bonus slot (Super).</summary>
        public const int MaxBonusSlot = 6;

        public const float OnHitProcChance = 0.15f;
        public const float MeleeAoeRadius = 2f;
        public const float RangedCritMultiplier = 2f;
        public const float HybridMeleeRange = 2f;
        public const float HybridMeleeDamageMin = 8f;
        public const float HybridMeleeDamageMax = 10f;
        public const float OnDeathSpawnChance = 0.25f;
        public const float CatapultRangeBonus = 2f;
        public const float CatapultAoeRadius = 3f;
        public const float CatapultAoeDamagePercent = 0.5f;
        public const int RegenAuraAbilityId = 50;
        public const float RegenAuraFlatBonus = 1f;

        public static bool IsBonusSlot(int slot) => slot >= MinBonusSlot && slot <= MaxBonusSlot;

        /// <summary>
        /// Player pick applies only to the matching role (manual call / wave). Otherwise 0 = base unit.
        /// </summary>
        public static int EffectiveBonusSlotForRole(int playerBonusPickSlot, UnitRole role) =>
            IsBonusSlot(playerBonusPickSlot) && RoleForBonusSlot(playerBonusPickSlot) == role
                ? playerBonusPickSlot
                : 0;

        public static int BonusSlotForRole(UnitRole role) => role switch
        {
            UnitRole.Melee => 1,
            UnitRole.Ranged => 2,
            UnitRole.Caster => 3,
            UnitRole.Siege => 4,
            UnitRole.Flying => 5,
            UnitRole.Super => 6,
            _ => 0,
        };

        public static UnitRole RoleForBonusSlot(int slot) => slot switch
        {
            1 => UnitRole.Melee,
            2 => UnitRole.Ranged,
            3 => UnitRole.Caster,
            4 => UnitRole.Siege,
            5 => UnitRole.Flying,
            6 => UnitRole.Super,
            _ => UnitRole.Melee,
        };

        public static bool RollProc(System.Random random, float chance)
        {
            if (random == null || chance <= 0f)
            {
                return false;
            }

            if (chance >= 1f)
            {
                return true;
            }

            return random.NextDouble() < chance;
        }

        /// <summary>Hybrid melee auto-attack damage for bonus caster, scaled by MeleeDamageLevel.</summary>
        public static float RollHybridMeleeDamage(MatchPlayerState player, System.Random random)
        {
            var raw = CombatRules.RollDamage(HybridMeleeDamageMin, HybridMeleeDamageMax, random);
            if (player == null)
            {
                return raw;
            }

            var multiplier = 1f + player.MeleeDamageLevel * MatchEconomyRules.MeleeDamagePercentPerLevel;
            return raw * multiplier;
        }

        public static bool IsHybridMeleeRange(float horizontalDistance) =>
            horizontalDistance < HybridMeleeRange;

        /// <summary>True when this unit is the Human caster bonus and should resolve Battlemace.</summary>
        public static bool IsCasterBonus(MatchUnitState unit) =>
            unit != null && unit.BonusSlot == BonusSlotForRole(UnitRole.Caster);

        /// <summary>
        /// Hybrid melee right now: caster BONUS with a living unit target closer than
        /// <see cref="HybridMeleeRange"/> (buildings stay ranged).
        /// </summary>
        public static bool IsHybridMeleeNow(
            MatchUnitState attacker,
            Vector3 attackerPosition,
            Vector3 targetPosition) =>
            IsCasterBonus(attacker)
            && IsHybridMeleeRange(HorizontalDistance(attackerPosition, targetPosition));

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static bool UsesCatapultSplash(int bonusSlot) =>
            bonusSlot == BonusSlotForRole(UnitRole.Super);
    }
}
