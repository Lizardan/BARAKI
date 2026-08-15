using System;
using Game.Gameplay.Combat;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Hero leveling: XP curve, ability unlock levels and per-level stat growth.
    /// GDD baseline: HERO_LEVELING (XpToNext = level * 100, max 10), HERO_ABILITY_UNLOCK_LEVELS (1/4/7/10),
    /// HERO_LEVEL_STATS (HP +40, damage +3, armor +0.5 per level). Progress is per-match only.
    /// </summary>
    public static class HeroLevelRules
    {
        public const int MaxLevel = 10;
        public const int StartingLevel = 1;

        /// <summary>XP awarded to every living hero of the owner for each enemy building destroyed by owner's units.</summary>
        public const int XpForBuildingKill = 60;

        // Ability unlock levels.
        public const int StrikeUnlockLevel = 1;
        public const int HealUnlockLevel = 4;
        public const int AuraUnlockLevel = 7;
        public const int UltimateUnlockLevel = 10;

        // Per-level stat growth applied as base + (level - 1) * perLevel.
        public const float MaxHpPerLevel = 40f;
        public const float ArmorPerLevel = 0.5f;
        public const float DamageMinPerLevel = 3f;
        public const float DamageMaxPerLevel = 3f;

        public static int XpToNext(int level) => Math.Max(0, level) * 100;

        /// <summary>XP granted for a unit kill equals the victim's gold bounty.</summary>
        public static int GetKillXp(int goldBounty) => Math.Max(0, goldBounty);

        public static int GetBuildingKillXp() => XpForBuildingKill;

        public static bool IsMaxLevel(int level) => level >= MaxLevel;

        public static bool CanLevelUp(int level, int xp) =>
            level < MaxLevel && xp >= XpToNext(level);

        public static UnitCombatStats ApplyLevelGrowth(UnitCombatStats baseStats, int level)
        {
            var effectiveLevel = Math.Max(1, level);
            var growthSteps = effectiveLevel - 1;
            return new UnitCombatStats(
                baseStats.Role,
                baseStats.MaxHp + growthSteps * MaxHpPerLevel,
                baseStats.Armor + growthSteps * ArmorPerLevel,
                baseStats.DamageMin + growthSteps * DamageMinPerLevel,
                baseStats.DamageMax + growthSteps * DamageMaxPerLevel,
                baseStats.AttackSpeed,
                baseStats.AttackRange,
                baseStats.MoveSpeed,
                baseStats.GoldBounty,
                baseStats.MaxMana);
        }
    }
}
