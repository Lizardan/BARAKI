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

        public static string FormatMainLevelButton(int nextLevel, int cost) =>
            $"Ур. здания\n→ {nextLevel}\n{cost}g";

        public static string FormatMainLevelTooltip(int nextLevel, int cost, float seconds) =>
            $"Уровень главного здания → {nextLevel}\n{cost}g · {seconds:0}с\nОткрывает грейды до ур. {nextLevel * 3}";

        public static string FormatMagicButton(int nextLevel, int cost) =>
            $"Магия\nУр. {nextLevel}\n{cost}g";

        public static string FormatMagicTooltip(int nextLevel, int cost, float seconds) =>
            $"Магия — уровень {nextLevel}\n{cost}g · {seconds:0}с\n+{MatchEconomyRules.MagicDamagePerLevel:0} урона магам и новое заклинание";

        public static string FormatHeroHireButton(int heroSlot, int cost) =>
            $"Герой {heroSlot}\n{cost}g";

        public static string FormatHeroHireTooltip(int heroSlot, int cost, float seconds) =>
            $"Найм героя {heroSlot}\n{cost}g · {seconds:0}с исследования";

        public static string FormatTitanDeployButton(int cost) =>
            $"Титан\n{cost}g";

        public static string FormatTitanDeployTooltip(int cost) =>
            $"Выпуск титана\n{cost}g · мгновенно";

        public static string GetStatTrackTitle(string trackId) => trackId switch
        {
            GameIds.Upgrades.MeleeDamage => "Урон мили",
            GameIds.Upgrades.RangedDamage => "Урон дальн.",
            GameIds.Upgrades.Armor => "ХП/Броня",
            _ => "Трек",
        };

        public static string GetStatTrackEffect(string trackId) => trackId switch
        {
            GameIds.Upgrades.MeleeDamage => "+3% урон ближнего боя за уровень",
            GameIds.Upgrades.RangedDamage => "+3% урон дальнего боя за уровень",
            GameIds.Upgrades.Armor => "+25 ХП и +2 брони всем юнитам за уровень",
            _ => string.Empty,
        };

        public static string FormatStatTrackButton(string trackId, int nextLevel, int cost) =>
            $"{GetStatTrackTitle(trackId)}\nУр. {nextLevel}\n{cost}g";

        public static string FormatStatTrackTooltip(string trackId, int nextLevel, int cost, float seconds) =>
            $"{GetStatTrackTitle(trackId)} — уровень {nextLevel}\n{cost}g · {seconds:0}с\n{GetStatTrackEffect(trackId)}";

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

            if (upgradeId == GameIds.Upgrades.MainBuildingLevel)
            {
                return $"Глав {displayLevel}";
            }

            if (upgradeId == GameIds.Upgrades.MeleeDamage)
            {
                return $"Мил {displayLevel}";
            }

            if (upgradeId == GameIds.Upgrades.RangedDamage)
            {
                return $"Дал {displayLevel}";
            }

            if (upgradeId == GameIds.Upgrades.Armor)
            {
                return $"ХП {displayLevel}";
            }

            if (upgradeId == GameIds.Upgrades.MainMagic)
            {
                return $"Магия {displayLevel}";
            }

            if (HeroRules.TryParseHireUpgradeId(upgradeId, out var heroSlot))
            {
                return $"Герой {heroSlot}";
            }

            return "?";
        }
    }
}
