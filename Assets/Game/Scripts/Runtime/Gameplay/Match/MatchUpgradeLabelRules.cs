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

        public static string FormatDivineBlessingButton(int cost) =>
            $"Благословение\n{cost}g";

        public static string FormatDivineBlessingLockedButton(int requiredMainLevel) =>
            $"Благословение\nУр. {requiredMainLevel}";

        public static string FormatDivineBlessingTooltip(int cost, float seconds, int mainLevel = 99) =>
            mainLevel < MatchEconomyRules.DivineBlessingRequiredMainLevel
                ? $"Божественное благословение\nНужен ур. главного здания {MatchEconomyRules.DivineBlessingRequiredMainLevel}\n{cost}g · {seconds:0}с\nСнимает туман войны; открывает выбор способности main"
                : $"Божественное благословение\n{cost}g · {seconds:0}с\nСнимает туман войны; открывает выбор способности main";

        public static string FormatExtraAbilityMenuButton() => "Способность";

        public static string FormatExtraAbilityMenuTooltip() =>
            "Выбрать одну доп. способность main на весь матч\nМеню можно закрыть и открыть снова";

        public static string FormatExtraAbilityCastButton(int abilityId, float mana, float manaMax, float cooldown) =>
            cooldown > 0.05f
                ? $"{MainExtraAbilityRules.GetDisplayName(abilityId)}\n{cooldown:0}с"
                : $"{MainExtraAbilityRules.GetDisplayName(abilityId)}\n{mana:0}/{manaMax:0}";

        public static string FormatExtraAbilityCastTooltip(int abilityId, float mana, float manaMax, float cooldown)
        {
            var effect = MainExtraAbilityRules.GetEffectDescription(abilityId);
            if (cooldown > 0.05f)
            {
                return $"{MainExtraAbilityRules.GetDisplayName(abilityId)}\n{effect}\nПерезарядка: {cooldown:0}с\nМана: {mana:0}/{manaMax:0}";
            }

            return $"{MainExtraAbilityRules.GetDisplayName(abilityId)}\n{effect}\nМана: {mana:0}/{manaMax:0}\nКлик → выбрать цель";
        }

        public static string FormatExtraAbilityPickedButton(int abilityId) =>
            MainExtraAbilityRules.GetDisplayName(abilityId);

        public static string FormatExtraAbilityPickedTooltip(int abilityId) =>
            MainExtraAbilityRules.GetMenuTooltip(abilityId);

        public static string FormatHeroHireButton(int heroSlot, int cost) =>
            $"Герой {heroSlot}\n{cost}g";

        public static string FormatHeroHireTooltip(int heroSlot, int cost, float seconds) =>
            $"Найм героя {heroSlot}\n{cost}g · {seconds:0}с исследования";

        public static string FormatHeroDeployButton(int heroSlot, int cost) =>
            $"Герой {heroSlot}\n{cost}g";

        public static string FormatHeroDeployCooldownButton(int heroSlot, int remainingSeconds) =>
            $"Герой {heroSlot}\n{remainingSeconds}с";

        public static string FormatHeroDeployTooltip(string heroName, int cost) =>
            $"Выпуск {heroName}\n{cost}g · мгновенно";

        public static string FormatHeroDeployCooldownTooltip(string heroName, int remainingSeconds) =>
            $"{heroName} — перезарядка казарм\n{remainingSeconds}с";

        public static string FormatTitanDeployButton(int cost) =>
            $"Титан\n{cost}g";

        public static string FormatTitanDeployTooltip(int cost) =>
            $"Выпуск титана\n{cost}g · мгновенно";

        public static string FormatTitanDeployCooldownButton(int remainingSeconds) =>
            $"Титан\n{remainingSeconds}с";

        public static string FormatTitanDeployCooldownTooltip(int remainingSeconds) =>
            $"Титан — перезарядка казарм\n{remainingSeconds}с";

        public static int CeilRemainingSeconds(float remaining) =>
            remaining <= 0f ? 0 : (int)System.Math.Ceiling(remaining);

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

            if (upgradeId == GameIds.Upgrades.DivineBlessing)
            {
                return "Благосл.";
            }

            if (HeroRules.TryParseHireUpgradeId(upgradeId, out var heroSlot))
            {
                return $"Герой {heroSlot}";
            }

            return "?";
        }
    }
}
