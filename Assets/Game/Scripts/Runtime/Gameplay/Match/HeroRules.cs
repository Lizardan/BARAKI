using System;
using Game.Core;

namespace Game.Gameplay.Match
{
    public enum HeroLifecycleState
    {
        None = 0,
        IdleAtBase = 1,
        Deployed = 2,
        Dead = 3,
    }

    /// <summary>Hire / deploy economy and slot caps from Heroes.md.</summary>
    public static class HeroRules
    {
        public const int HireGold = 500;
        public const int DeployGold = 1000;
        public const float HireResearchSeconds = 25f;
        public const float DeathCooldownSeconds = 300f;
        public const int MaxHeroSlots = 3;

        public static int GetMaxHiredHeroes(int mainLevel) =>
            mainLevel < 1 ? 0 : (mainLevel > MaxHeroSlots ? MaxHeroSlots : mainLevel);

        public static bool IsValidHeroSlot(int heroSlot) =>
            heroSlot >= 1 && heroSlot <= MaxHeroSlots;

        public static string BuildHireUpgradeId(int heroSlot) =>
            $"{GameIds.Upgrades.HeroHire}:{heroSlot}";

        public static bool TryParseHireUpgradeId(string upgradeId, out int heroSlot)
        {
            heroSlot = -1;
            if (string.IsNullOrEmpty(upgradeId))
            {
                return false;
            }

            var prefix = GameIds.Upgrades.HeroHire + ":";
            if (!upgradeId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(upgradeId.Substring(prefix.Length), out heroSlot)
                   && IsValidHeroSlot(heroSlot);
        }

        public static bool CanHire(
            HeroLifecycleState state,
            int heroSlot,
            int mainLevel,
            int gold) =>
            state == HeroLifecycleState.None
            && IsValidHeroSlot(heroSlot)
            && heroSlot <= GetMaxHiredHeroes(mainLevel)
            && gold >= HireGold;

        public static bool CanDeploy(
            HeroLifecycleState state,
            float deathCooldownRemaining,
            int gold,
            bool barracksIntact) =>
            barracksIntact
            && gold >= DeployGold
            && state is HeroLifecycleState.IdleAtBase
                or HeroLifecycleState.Dead
            && deathCooldownRemaining <= 0f
            && state != HeroLifecycleState.Deployed;
    }
}
