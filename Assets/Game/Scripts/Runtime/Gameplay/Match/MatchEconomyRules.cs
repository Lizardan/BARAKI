using System;
using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>Pure economy formulas from Economy.md / Buildings.md.</summary>
    public static class MatchEconomyRules
    {
        public static readonly int[] BarracksLevelCosts = { 1000, 1500, 2500 };
        public static readonly float[] BarracksLevelDurationsSeconds = { 3f, 3f, 3f };
        public static readonly int[] MainLevelCosts = { 2000, 3000 };
        public static readonly float[] MainLevelDurationsSeconds = { 120f, 180f };

        public static readonly int[] MeleeDamageTrackCosts = { 75, 100, 125, 150, 175, 200, 225, 250, 275 };
        public static readonly float[] MeleeDamageTrackDurationsSeconds = { 8f, 10f, 12f, 14f, 16f, 18f, 20f, 22f, 24f };
        public static readonly int[] RangedDamageTrackCosts = { 75, 100, 125, 150, 175, 200, 225, 250, 275 };
        public static readonly float[] RangedDamageTrackDurationsSeconds = { 8f, 10f, 12f, 14f, 16f, 18f, 20f, 22f, 24f };
        public static readonly int[] HpArmorTrackCosts = { 60, 80, 100, 120, 140, 160, 180, 200, 220 };
        public static readonly float[] HpArmorTrackDurationsSeconds = { 6f, 8f, 10f, 12f, 14f, 16f, 18f, 20f, 22f };

        public const int PassiveGoldUpgradeCost = 200;
        public const float PassiveGoldUpgradeSeconds = 25f;
        public const float PassiveGoldTickIntervalSeconds = 30f;
        public const int PassiveGoldPerLevelPerTick = 25;
        public const int MaxPassiveGoldLevel = 9;
        public const int MaxBarracksLevel = 4;
        public const int MaxMainLevel = 3;
        public const int MaxStatTrackLevel = 9;
        public const int MaxMagicLevel = 3;
        public const float MeleeDamagePercentPerLevel = 0.03f;
        public const float RangedDamagePercentPerLevel = 0.03f;
        public const float UpgradeHpPerLevel = 25f;
        public const float UpgradeArmorPerLevel = 2f;
        public const float MagicDamagePerLevel = 3f;
        public const int DefaultMainLevel = 1;

        public static readonly int[] MagicSlotCosts = { 800, 1500, 2500 };
        public static readonly float[] MagicSlotDurationsSeconds = { 60f, 90f, 135f };

        public static bool TrySpendGold(int currentGold, int cost, out int remainingGold)
        {
            remainingGold = currentGold;
            if (cost < 0 || currentGold < cost)
            {
                return false;
            }

            remainingGold = currentGold - cost;
            return true;
        }

        public static bool TryGetBarracksLevelUpgrade(int currentLevel, out int cost, out float durationSeconds)
        {
            cost = 0;
            durationSeconds = 0f;
            if (currentLevel < 1 || currentLevel >= MaxBarracksLevel)
            {
                return false;
            }

            var index = currentLevel - 1;
            cost = BarracksLevelCosts[index];
            durationSeconds = BarracksLevelDurationsSeconds[index];
            return true;
        }

        public static bool TryGetMainLevelUpgrade(int currentLevel, out int cost, out float durationSeconds)
        {
            cost = 0;
            durationSeconds = 0f;
            if (currentLevel < 1 || currentLevel >= MaxMainLevel)
            {
                return false;
            }

            var index = currentLevel - 1;
            cost = MainLevelCosts[index];
            durationSeconds = MainLevelDurationsSeconds[index];
            return true;
        }

        public static int GetPassiveGoldCap(int mainLevel)
        {
            var clampedMain = Math.Clamp(mainLevel, 1, MaxMainLevel);
            return Math.Min(MaxPassiveGoldLevel, clampedMain * 3);
        }

        public static int GetPassiveGoldPerTick(int upgradeLevel) =>
            Math.Clamp(upgradeLevel, 0, MaxPassiveGoldLevel) * PassiveGoldPerLevelPerTick;

        public static bool CanPurchasePassiveGold(int currentLevel, int mainLevel)
        {
            if (currentLevel < 0 || currentLevel >= MaxPassiveGoldLevel)
            {
                return false;
            }

            return currentLevel < GetPassiveGoldCap(mainLevel);
        }

        public static int GetStatLevelCap(int mainLevel)
        {
            var clampedMain = Math.Clamp(mainLevel, 1, MaxMainLevel);
            return Math.Min(MaxStatTrackLevel, clampedMain * 3);
        }

        public static bool CanPurchaseStatTrack(string trackId, int currentLevel, int mainLevel)
        {
            if (currentLevel < 0 || currentLevel >= MaxStatTrackLevel)
            {
                return false;
            }

            return currentLevel < GetStatLevelCap(mainLevel);
        }

        public static bool TryGetStatTrackUpgrade(
            string trackId,
            int currentLevel,
            out int cost,
            out float durationSeconds)
        {
            cost = 0;
            durationSeconds = 0f;
            if (currentLevel < 0 || currentLevel >= MaxStatTrackLevel)
            {
                return false;
            }

            var index = currentLevel;
            if (trackId == GameIds.Upgrades.MeleeDamage)
            {
                cost = MeleeDamageTrackCosts[index];
                durationSeconds = MeleeDamageTrackDurationsSeconds[index];
                return true;
            }

            if (trackId == GameIds.Upgrades.RangedDamage)
            {
                cost = RangedDamageTrackCosts[index];
                durationSeconds = RangedDamageTrackDurationsSeconds[index];
                return true;
            }

            if (trackId == GameIds.Upgrades.Armor)
            {
                cost = HpArmorTrackCosts[index];
                durationSeconds = HpArmorTrackDurationsSeconds[index];
                return true;
            }

            return false;
        }

        public static bool CanPurchaseMagic(int currentMagicLevel, int mainLevel)
        {
            return currentMagicLevel >= 0
                && currentMagicLevel < mainLevel
                && currentMagicLevel < MaxMagicLevel;
        }

        public static bool TryGetMagicUpgrade(int currentMagicLevel, out int cost, out float durationSeconds)
        {
            cost = 0;
            durationSeconds = 0f;
            if (currentMagicLevel < 0 || currentMagicLevel >= MaxMagicLevel)
            {
                return false;
            }

            var index = currentMagicLevel;
            cost = MagicSlotCosts[index];
            durationSeconds = MagicSlotDurationsSeconds[index];
            return true;
        }
    }
}
