using Game.Gameplay.Combat;
using Game.Gameplay.Data;

namespace Game.Gameplay.Match
{
    public enum TitanLifecycleState
    {
        Locked = 0,
        IdleAtBase = 1,
        Deployed = 2,
        Dead = 3,
    }

    /// <summary>
    /// Titan unlock/deploy rules (GDD: <c>TITAN_SUMMON</c>).
    /// Passive 180s bar above Main gated on Main lvl 3 + all 3 heroes idle at base.
    /// Progress freezes while any hero is deployed/dead (no reset). On complete the titan
    /// appears parked at base. Deploy costs 2500g from an intact barracks; death starts a
    /// 300s cooldown per barracks (no re-research). Titan XP/levels follow hero leveling.
    /// </summary>
    public static class TitanRules
    {
        public const float ResearchSeconds = 180f;
        public const int DeployGold = 2500;
        public const float DeathCooldownSeconds = 300f;
        public const int RequiredMainLevel = 3;
        public const int RequiredHeroesHired = 3;
        public const int RequiredHeroesIdleAtBase = 3;

        /// <summary>Titan combat stats = hero base × this multiplier (HP/armor/damage), before level growth.</summary>
        public const float BaseStatMultiplier = 3f;

        public static bool AreResearchGatesMet(MatchPlayerState player, HeroRosterState roster)
        {
            if (player == null || roster == null || player.MainLevel < RequiredMainLevel)
            {
                return false;
            }

            if (roster.CountHired() < RequiredHeroesHired)
            {
                return false;
            }

            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                if (roster.Get(slot).State != HeroLifecycleState.IdleAtBase)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ShouldShowResearchBar(TitanLifecycleState state, float progressSeconds, bool gatesMet) =>
            state == TitanLifecycleState.Locked && (gatesMet || progressSeconds > 0f);

        public static bool CanDeploy(
            TitanLifecycleState state,
            float barracksDeathCooldownRemaining,
            int gold,
            bool barracksIntact) =>
            barracksIntact
            && gold >= DeployGold
            && state is TitanLifecycleState.IdleAtBase or TitanLifecycleState.Dead
            && barracksDeathCooldownRemaining <= 0f;

        /// <summary>Barracks deploy button is visible while the titan is idle at base or dead (CD).</summary>
        public static bool ShouldShowDeploy(TitanLifecycleState state) =>
            state is TitanLifecycleState.IdleAtBase or TitanLifecycleState.Dead;

        public static UnitCombatStats ScaleForTitan(UnitCombatStats baseStats)
        {
            return new UnitCombatStats(
                UnitRole.Titan,
                baseStats.MaxHp * BaseStatMultiplier,
                baseStats.Armor * BaseStatMultiplier,
                baseStats.DamageMin * BaseStatMultiplier,
                baseStats.DamageMax * BaseStatMultiplier,
                baseStats.AttackSpeed,
                baseStats.AttackRange,
                baseStats.MoveSpeed,
                baseStats.GoldBounty,
                baseStats.MaxMana);
        }
    }
}
