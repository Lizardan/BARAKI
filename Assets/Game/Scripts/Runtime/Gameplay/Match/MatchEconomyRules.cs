using System;
using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>Pure economy formulas from Economy.md / Buildings.md.</summary>
    public static class MatchEconomyRules
    {
        public static readonly int[] BarracksLevelCosts = { 1000, 1500, 2500 };
        public static readonly float[] BarracksLevelDurationsSeconds = { 3f, 3f, 3f };

        public const int PassiveGoldUpgradeCost = 200;
        public const float PassiveGoldUpgradeSeconds = 25f;
        public const float PassiveGoldTickIntervalSeconds = 30f;
        public const int PassiveGoldPerLevelPerTick = 25;
        public const int MaxPassiveGoldLevel = 9;
        public const int MaxBarracksLevel = 4;
        public const int DefaultMainLevel = 1;

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

        public static int GetPassiveGoldCap(int mainLevel)
        {
            var clampedMain = Math.Clamp(mainLevel, 1, 3);
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
    }
}
