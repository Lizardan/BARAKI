using System;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Pure rules for the Human bonuses: unit slots 1–6 (PRE-006a), champion veterans 7–10
    /// and race uniques 11–12 (PRE-006b): slot mapping and combat-mechanic tuning constants.
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
        public const float CatapultRangeBonus = 2f; // base Super 10 → BONUS 12
        public const float CatapultAoeRadius = 3f;
        public const float CatapultAoeDamagePercent = 0.5f;
        public const float CatapultSplashDiscSeconds = 1f;
        public const int RegenAuraAbilityId = 50;
        public const float RegenAuraFlatBonus = 1f;

        // --- PRE-006b: champion bonus slots 7–10 and race uniques 11–12 ---
        /// <summary>First hero veteran bonus slot (King, hero slot 1).</summary>
        public const int Hero1BonusSlot = 7;
        /// <summary>Last hero veteran bonus slot (Priest, hero slot 3).</summary>
        public const int Hero3BonusSlot = 9;
        /// <summary>Titan veteran bonus slot.</summary>
        public const int TitanBonusSlot = 10;
        /// <summary>Race-unique bonus slot #1 (March Discipline for Humans).</summary>
        public const int RaceUnique1Slot = 11;
        /// <summary>Race-unique bonus slot #2 (Stone Masonry for Humans).</summary>
        public const int RaceUnique2Slot = 12;

        /// <summary>Hero slot N is enhanced when the player picked bonus slot 6 + N (7..9).</summary>
        public const int HeroBonusSlotOffset = 6;

        /// <summary>Veteran champion stat multipliers vs the base hero/titan (PRE-006b).</summary>
        public const float VeteranHpMultiplier = 1.4f;
        public const float VeteranDamageMultiplier = 1.35f;
        public const float VeteranArmorBonus = 2f;

        /// <summary>March Discipline (slot 11): move speed multiplier for all owner troops.</summary>
        public const float MarchDisciplineMultiplier = 1.1f;
        /// <summary>Stone Masonry (slot 12): max HP multiplier for all owner buildings.</summary>
        public const float StoneMasonryHpMultiplier = 1.2f;

        /// <summary>
        /// Races that currently have no bonus kit (GATE: Faceless Fase 1). Their bonus pick is
        /// neutralized so they never inherit Human veteran multipliers or enhanced units.
        /// </summary>
        public static readonly string[] NoBonusKitRaceIds = { GameIds.Races.Faceless };

        /// <summary>True when a race has an authored bonus kit (unit 1–6 + veteran 7–10 + uniques 11–12).</summary>
        public static bool HasBonusKit(string raceId)
        {
            if (string.IsNullOrEmpty(raceId))
            {
                return true;
            }

            for (var i = 0; i < NoBonusKitRaceIds.Length; i++)
            {
                if (NoBonusKitRaceIds[i] == raceId)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsBonusSlot(int slot) => slot >= MinBonusSlot && slot <= MaxBonusSlot;

        /// <summary>Slots 7–9: veteran heroes; the value maps to hero slot via <see cref="HeroSlotForBonusSlot"/>.</summary>
        public static bool IsHeroBonusSlot(int slot) =>
            slot >= Hero1BonusSlot && slot <= Hero3BonusSlot;

        /// <summary>Slot 10: veteran titan.</summary>
        public static bool IsTitanBonusSlot(int slot) => slot == TitanBonusSlot;

        /// <summary>Slots 7–10: veteran champions (heroes + titan).</summary>
        public static bool IsChampionBonusSlot(int slot) =>
            IsHeroBonusSlot(slot) || IsTitanBonusSlot(slot);

        /// <summary>Slots 11–12: race uniques (player-level modifiers, no unit replacement).</summary>
        public static bool IsRaceUniqueSlot(int slot) =>
            slot >= RaceUnique1Slot && slot <= RaceUnique2Slot;

        public static int HeroSlotForBonusSlot(int slot) => slot - HeroBonusSlotOffset;

        public static int BonusSlotForHeroSlot(int heroSlot) => HeroBonusSlotOffset + heroSlot;

        /// <summary>Effective bonus slot for a hero spawn: pick must match this hero's slot, else 0.</summary>
        public static int EffectiveBonusSlotForHero(int playerBonusPickSlot, int heroSlot) =>
            EffectiveBonusSlotForHero(null, playerBonusPickSlot, heroSlot);

        /// <summary>Race-aware hero bonus slot; races without a bonus kit always resolve to 0.</summary>
        public static int EffectiveBonusSlotForHero(string raceId, int playerBonusPickSlot, int heroSlot) =>
            !HasBonusKit(raceId) ? 0 :
            playerBonusPickSlot == BonusSlotForHeroSlot(heroSlot) ? playerBonusPickSlot : 0;

        /// <summary>Effective bonus slot for a titan spawn: pick must be the titan slot, else 0.</summary>
        public static int EffectiveBonusSlotForTitan(int playerBonusPickSlot) =>
            EffectiveBonusSlotForTitan(null, playerBonusPickSlot);

        /// <summary>Race-aware titan bonus slot; races without a bonus kit always resolve to 0.</summary>
        public static int EffectiveBonusSlotForTitan(string raceId, int playerBonusPickSlot) =>
            !HasBonusKit(raceId) ? 0 :
            playerBonusPickSlot == TitanBonusSlot ? playerBonusPickSlot : 0;

        /// <summary>
        /// True when <paramref name="bonusSlot"/> marks this unit as an enhanced variant of its
        /// role/hero slot (unit bonuses 1–6 and champion veterans 7–10).
        /// </summary>
        public static bool MatchesUnit(int bonusSlot, UnitRole role, int heroSlot)
        {
            if (bonusSlot <= 0)
            {
                return false;
            }

            if (IsBonusSlot(bonusSlot))
            {
                return RoleForBonusSlot(bonusSlot) == role;
            }

            if (role == UnitRole.Hero && IsHeroBonusSlot(bonusSlot))
            {
                return HeroSlotForBonusSlot(bonusSlot) == heroSlot;
            }

            return role == UnitRole.Titan && IsTitanBonusSlot(bonusSlot);
        }

        /// <summary>Applies veteran champion multipliers to base hero/titan stats (fallback path).</summary>
        public static UnitCombatStats ApplyVeteranMultipliers(UnitCombatStats stats) =>
            new(
                stats.Role,
                stats.MaxHp * VeteranHpMultiplier,
                stats.Armor + VeteranArmorBonus,
                stats.DamageMin * VeteranDamageMultiplier,
                stats.DamageMax * VeteranDamageMultiplier,
                stats.AttackSpeed,
                stats.AttackRange,
                stats.MoveSpeed,
                stats.GoldBounty,
                stats.MaxMana);

        /// <summary>March Discipline speed for any owner troop (units, heroes, titan).</summary>
        public static float ApplyMarchDiscipline(MatchPlayerState player, float speed)
        {
            if (player == null || player.BonusPickSlot != RaceUnique1Slot || speed <= 0f)
            {
                return speed;
            }

            return speed * MarchDisciplineMultiplier;
        }

        /// <summary>
        /// Player pick applies only to the matching role (manual call / wave). Otherwise 0 = base unit.
        /// </summary>
        public static int EffectiveBonusSlotForRole(int playerBonusPickSlot, UnitRole role) =>
            EffectiveBonusSlotForRole(null, playerBonusPickSlot, role);

        /// <summary>Race-aware role bonus slot; races without a bonus kit always resolve to 0.</summary>
        public static int EffectiveBonusSlotForRole(string raceId, int playerBonusPickSlot, UnitRole role) =>
            !HasBonusKit(raceId) ? 0 :
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
