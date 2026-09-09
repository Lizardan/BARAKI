using System;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Human-scoped bonus tuning: unit bonuses 1–6 (PRE-006a), champion/veteran/unique
    /// mechanic constants. The race-agnostic slot layout (slots 1–12, veteran multipliers,
    /// effective-slot resolvers, <c>HasBonusKit</c>) lives in <see cref="BonusKitRules"/>.
    /// </summary>
    public static class HumanBonusUnitRules
    {
        public const float OnHitProcChance = 0.15f;
        public const float MeleeAoeRadius = 2f;
        public const float RangedCritMultiplier = 2f;
        public const float HybridMeleeRange = 2f;
        public const float HybridMeleeDamageMin = 8f;
        public const float HybridMeleeDamageMax = 10f;
        public const float OnDeathSpawnChance = 0.25f;
        public const float CatapultRangeBonus = 2f; // base Super 10 → BONUS 12
        public const float CatapultAoeRadius = 3f;
        public const float CatapultAoeDamagePercent = 0.5f;
        public const float CatapultSplashDiscSeconds = 1f;
        public const int RegenAuraAbilityId = 50;
        public const float RegenAuraFlatBonus = 1f;

        /// <summary>March Discipline (slot 11): move speed multiplier for all owner troops.</summary>
        public const float MarchDisciplineMultiplier = 1.1f;
        /// <summary>Stone Masonry (slot 12): max HP multiplier for all owner buildings.</summary>
        public const float StoneMasonryHpMultiplier = 1.2f;

        /// <summary>
        /// March Discipline speed for any owner troop (units, heroes, titan).
        /// Human-only: other races picking slot 11 (e.g. Faceless 11) must not inherit it.
        /// </summary>
        public static float ApplyMarchDiscipline(MatchPlayerState player, float speed)
        {
            if (player == null
                || player.RaceId != GameIds.Races.Human
                || !player.HasBonusEffective(BonusKitRules.RaceUnique1Slot)
                || speed <= 0f)
            {
                return speed;
            }

            return speed * MarchDisciplineMultiplier;
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
            unit != null && unit.BonusSlot == BonusKitRules.BonusSlotForRole(UnitRole.Caster);

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
            bonusSlot == BonusKitRules.BonusSlotForRole(UnitRole.Super);
    }
}