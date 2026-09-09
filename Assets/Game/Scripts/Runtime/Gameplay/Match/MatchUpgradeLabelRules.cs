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

        public static string FormatBuildingAbilityButton(
            int abilityId,
            float mana,
            float manaMax,
            float cooldown,
            string raceId = null) =>
            cooldown > 0.05f
                ? $"{BuildingAbilityRules.GetDisplayName(abilityId, raceId)}\n{cooldown:0}с"
                : $"{BuildingAbilityRules.GetDisplayName(abilityId, raceId)}\n{mana:0}/{manaMax:0}";

        public static string FormatBuildingAbilityLockedButton(
            int abilityId,
            int requiredMainLevel,
            string raceId = null) =>
            $"{BuildingAbilityRules.GetDisplayName(abilityId, raceId)}\nУр. {requiredMainLevel}";

        public static string FormatBuildingAbilityTooltip(int abilityId, string raceId = null) =>
            BuildingAbilityRules.GetTooltip(abilityId, raceId);

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

        public static string GetTowerTrackTitle(int trackIndex, string raceId = GameIds.Races.Human) => raceId switch
        {
            GameIds.Races.Faceless => trackIndex switch
            {
                0 => "Chitinous Hide",
                1 => "Hollow Barbs",
                2 => "Vacuum Collapse",
                3 => "Ritual of the Deep",
                4 => "Swarm at Death Site",
                5 => "Frenzy of the Deep",
                6 => "Feast on Heroes",
                7 => "Hollow Bones",
                8 => "Void Hardening",
                _ => "Трек башни",
            },
            _ => trackIndex switch
            {
                0 => "Flaming Arrows",
                1 => "Bulwark",
                2 => "Bloodrage",
                3 => "Battering Rams",
                4 => "Arcane Focus",
                5 => "Skirmishers",
                6 => "Forced March",
                7 => "Field Medics",
                8 => "Last Stand",
                _ => "Трек башни",
            },
        };

        public static string GetTowerTrackEffect(int trackIndex, string raceId = GameIds.Races.Human)
        {
            if (raceId == GameIds.Races.Faceless)
            {
                return trackIndex switch
                {
                    0 => "Мили и супер: +1/+2/+3 брони; на ур. 3 +15% макс. ХП",
                    1 => "Стрелки и летуны: игнор +1/+2/+3 брони цели",
                    2 => "Мили и летуны: при смерти замедление 15%/25%/35% на 3 с в радиусе 3",
                    3 => "Кастеры: живой кастер в радиусе 8 снижает входящий урон на 6%/10%/15%",
                    4 => "При смерти обычного юнита: шанс 5%/10%/15% — поднять слугу на месте гибели",
                    5 => "Мили и осада: +10%/+15%/+20% скорости атаки",
                    6 => "Убийство вражеского героя/титана: слуги в радиусе 8 — +30% атаки и +30% макс. ХП на 10 с (декей 1/3 ХП)",
                    7 => "Осада и летуны: +8%/+16%/+24% скорости движения",
                    8 => "Все юниты: −10%/−15%/−20% урона от атак башен/зданий",
                    _ => string.Empty,
                };
            }

            return trackIndex switch
            {
                0 => "Стрелки и летуны поджигают: 2/4/6 dmg/с за уровень в течение 2 с; выстрелы живых башен тоже горят",
                1 => "Мили и осада: +1/+2/+3 брони; на ур. 3 блок −20% получаемого урона в ближнем бою",
                2 => "Мили и летуны: после убийства +15%/+25%/+40% скорости атаки на 3 с",
                3 => "Осада и супер: +25%/+50%/+75% урона по зданиям; на ур. 3 радиус разлёта +1",
                4 => "Кастеры: перезарядка умений ×0.88/×0.76/×0.64",
                5 => "Стрелки и кастеры: +0.5/+1.0/+1.5 дальности атаки",
                6 => "Мили, осада и кастеры: +8%/+16%/+24% скорости движения",
                7 => "Все юниты: +1/+2/+3 HP/с регенерации",
                8 => "Все юниты: при HP < 30% урон +20%/+30%/+40%",
                _ => string.Empty,
            };
        }

        public static string FormatTowerTrackButton(int trackIndex, int nextLevel, int cost, string raceId = GameIds.Races.Human) =>
            $"{GetTowerTrackTitle(trackIndex, raceId)}\nУр. {nextLevel}\n{cost}g";

        public static string FormatTowerTrackTooltip(int trackIndex, int nextLevel, int cost, float seconds, string raceId = GameIds.Races.Human) =>
            $"{GetTowerTrackTitle(trackIndex, raceId)} — уровень {nextLevel}\n{cost}g · {seconds:0}с\n{GetTowerTrackEffect(trackIndex, raceId)}";

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

            if (TowerTrackRules.TryGetTrackIndex(upgradeId, out var towerTrackIndex))
            {
                return $"Баш {towerTrackIndex + 1} {displayLevel}";
            }

            return "?";
        }
    }
}
