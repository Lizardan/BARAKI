using Game.Core;

namespace Game.Gameplay.Match
{
    public static class MatchUpgradeLabelRules
    {
        public static int GetNextLevel(int currentLevel, int queuedSameUpgradeCount) =>
            currentLevel + 1 + queuedSameUpgradeCount;

        public static string FormatPassiveGoldButton(int nextLevel, int cost) =>
            $"Passive Gold\nУр. {nextLevel}\n{cost}g";

        public static string FormatPassiveGoldTooltip(int nextLevel, int cost, float seconds, float tickInterval) =>
            $"Passive Gold — уровень {nextLevel}\n{cost}g · {seconds:0}с\n+доход каждые {tickInterval:0}с";

        public static string FormatBarracksLevelButton(int nextLevel, int cost) =>
            $"Ур. казарм\n→ {nextLevel}\n{cost}g";

        public static string FormatBarracksLevelTooltip(int nextLevel, int cost, float seconds) =>
            $"Уровень казарм → {nextLevel}\n{cost}g · {seconds:0}с";

        public static string FormatHeroHireButton(int heroSlot, int cost) =>
            $"Герой {heroSlot}\n{cost}g";

        public static string FormatHeroHireTooltip(int heroSlot, int cost, float seconds) =>
            $"Найм героя {heroSlot}\n{cost}g · {seconds:0}с исследования";

        public static string FormatQueueSlotShort(string upgradeId, int displayLevel)
        {
            if (upgradeId == GameIds.Upgrades.MainPassiveGold)
            {
                return $"PG {displayLevel}";
            }

            if (upgradeId == GameIds.Upgrades.BarracksLevel)
            {
                return $"Бар {displayLevel}";
            }

            if (HeroRules.TryParseHireUpgradeId(upgradeId, out var heroSlot))
            {
                return $"Герой {heroSlot}";
            }

            return "?";
        }
    }
}
