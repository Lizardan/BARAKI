using System;
using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Race-agnostic bonus-slot layout shared by every race (Bonuses.md: 12 slots).
    /// Slots 1–6 = unit bonuses, 7–9 = veteran heroes, 10 = veteran titan, 11–12 = race uniques.
    /// Veteran stat multipliers mirror Human PRE-006b (shared by all races with a veteran kit).
    /// Race-specific effect tuning lives in each race's own rules class
    /// (<c>HumanBonusUnitRules</c> / <c>FacelessBonusUnitRules</c>).
    /// </summary>
    public static class BonusKitRules
    {
        /// <summary>First unit bonus slot (Melee).</summary>
        public const int MinBonusSlot = 1;

        /// <summary>Last unit bonus slot (Super).</summary>
        public const int MaxBonusSlot = 6;

        // --- Champion veteran slots 7–10 and race uniques 11–12 ---
        /// <summary>First hero veteran bonus slot (hero slot 1).</summary>
        public const int Hero1BonusSlot = 7;
        /// <summary>Second hero veteran bonus slot (hero slot 2).</summary>
        public const int Hero2BonusSlot = 8;
        /// <summary>Last hero veteran bonus slot (hero slot 3).</summary>
        public const int Hero3BonusSlot = 9;
        /// <summary>Titan veteran bonus slot.</summary>
        public const int TitanBonusSlot = 10;
        /// <summary>Race-unique bonus slot #1.</summary>
        public const int RaceUnique1Slot = 11;
        /// <summary>Race-unique bonus slot #2.</summary>
        public const int RaceUnique2Slot = 12;

        /// <summary>
        /// Summon marker (NOT an authored game slot — outside the 1–12 range on purpose). Wire v22 ships
        /// bonusSlot as byte 0–255, so 13 is safe. <see cref="MatchesUnit"/>/<see cref="RoleForBonusSlot"/>
        /// and <see cref="EffectiveBonusSlotForRole(string, int, UnitRole)"/> must keep ignoring it.
        /// </summary>
        public const int SummonBonusSlot = 13;

        /// <summary>Hero slot N is enhanced when the player picked bonus slot 6 + N (7..9).</summary>
        public const int HeroBonusSlotOffset = 6;

        /// <summary>Veteran champion stat multipliers vs the base hero/titan (PRE-006b).</summary>
        public const float VeteranHpMultiplier = 1.4f;
        public const float VeteranDamageMultiplier = 1.35f;
        public const float VeteranArmorBonus = 2f;

        /// <summary>
        /// Races that currently have no bonus kit (GATE). Their bonus pick is
        /// neutralized so they never inherit veteran multipliers or enhanced units.
        /// Add a race id here while its kit is still work-in-progress and remove
        /// it once the kit + visual ship (FACELESS-014 flipped the Faceless gate).
        /// </summary>
        public static readonly string[] NoBonusKitRaceIds = { };

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

        /// <summary>Slots 7–10: champion veterans (heroes + titan).</summary>
        public static bool IsChampionBonusSlot(int slot) =>
            IsHeroBonusSlot(slot) || IsTitanBonusSlot(slot);

        /// <summary>Slots 11–12: race uniques (player-level modifiers, no unit replacement).</summary>
        public static bool IsRaceUniqueSlot(int slot) =>
            slot >= RaceUnique1Slot && slot <= RaceUnique2Slot;

        public static int HeroSlotForBonusSlot(int slot) => slot - HeroBonusSlotOffset;

        public static int BonusSlotForHeroSlot(int heroSlot) => HeroBonusSlotOffset + heroSlot;

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

        /// <summary>Player pick applies only to the matching role (manual call / wave). Otherwise 0 = base unit.</summary>
        public static int EffectiveBonusSlotForRole(int playerBonusPickSlot, UnitRole role) =>
            EffectiveBonusSlotForRole(null, playerBonusPickSlot, BonusPickRules.NoneSlot, role);

        /// <summary>Race-aware role bonus slot (single-pick path); races without a bonus kit always resolve to 0.</summary>
        public static int EffectiveBonusSlotForRole(string raceId, int playerBonusPickSlot, UnitRole role) =>
            EffectiveBonusSlotForRole(raceId, playerBonusPickSlot, BonusPickRules.NoneSlot, role);

        /// <summary>Player picks apply only to the matching role (manual call / wave). Otherwise 0 = base unit.</summary>
        public static int EffectiveBonusSlotForRole(int pick1, int pick2, UnitRole role) =>
            EffectiveBonusSlotForRole(null, pick1, pick2, role);

        /// <summary>Race-aware role bonus slot from either pick (auto + chosen stack); races without a kit resolve to 0.</summary>
        public static int EffectiveBonusSlotForRole(string raceId, int pick1, int pick2, UnitRole role)
        {
            if (!HasBonusKit(raceId))
            {
                return 0;
            }

            if (IsBonusSlot(pick1) && RoleForBonusSlot(pick1) == role)
            {
                return pick1;
            }

            if (IsBonusSlot(pick2) && RoleForBonusSlot(pick2) == role)
            {
                return pick2;
            }

            return 0;
        }

        /// <summary>Effective bonus slot for a hero spawn: a pick must match this hero's slot, else 0.</summary>
        public static int EffectiveBonusSlotForHero(int playerBonusPickSlot, int heroSlot) =>
            EffectiveBonusSlotForHero(null, playerBonusPickSlot, BonusPickRules.NoneSlot, heroSlot);

        /// <summary>Race-aware hero bonus slot (single-pick path); races without a bonus kit always resolve to 0.</summary>
        public static int EffectiveBonusSlotForHero(string raceId, int playerBonusPickSlot, int heroSlot) =>
            EffectiveBonusSlotForHero(raceId, playerBonusPickSlot, BonusPickRules.NoneSlot, heroSlot);

        /// <summary>Effective bonus slot for a hero spawn: either pick may match this hero's slot.</summary>
        public static int EffectiveBonusSlotForHero(int pick1, int pick2, int heroSlot) =>
            EffectiveBonusSlotForHero(null, pick1, pick2, heroSlot);

        /// <summary>Race-aware hero bonus slot from either pick (auto + chosen stack).</summary>
        public static int EffectiveBonusSlotForHero(string raceId, int pick1, int pick2, int heroSlot)
        {
            if (!HasBonusKit(raceId))
            {
                return 0;
            }

            var slot = BonusSlotForHeroSlot(heroSlot);
            if (pick1 == slot || pick2 == slot)
            {
                return slot;
            }

            return 0;
        }

        /// <summary>Effective bonus slot for a titan spawn: a pick must be the titan slot, else 0.</summary>
        public static int EffectiveBonusSlotForTitan(int playerBonusPickSlot) =>
            EffectiveBonusSlotForTitan(null, playerBonusPickSlot, BonusPickRules.NoneSlot);

        /// <summary>Race-aware titan bonus slot (single-pick path); races without a bonus kit always resolve to 0.</summary>
        public static int EffectiveBonusSlotForTitan(string raceId, int playerBonusPickSlot) =>
            EffectiveBonusSlotForTitan(raceId, playerBonusPickSlot, BonusPickRules.NoneSlot);

        /// <summary>Effective bonus slot for a titan spawn: either pick may be the titan slot.</summary>
        public static int EffectiveBonusSlotForTitan(int pick1, int pick2) =>
            EffectiveBonusSlotForTitan(null, pick1, pick2);

        /// <summary>Race-aware titan bonus slot from either pick (auto + chosen stack).</summary>
        public static int EffectiveBonusSlotForTitan(string raceId, int pick1, int pick2)
        {
            if (!HasBonusKit(raceId))
            {
                return 0;
            }

            return pick1 == TitanBonusSlot || pick2 == TitanBonusSlot ? TitanBonusSlot : 0;
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
    }
}