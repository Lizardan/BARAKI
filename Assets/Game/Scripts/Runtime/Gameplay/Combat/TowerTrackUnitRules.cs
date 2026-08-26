using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Applies race tower upgrade tracks (PRE-007) to a regular unit's spawn-time stats.
    /// Never applies to champions (<see cref="UnitRole.Hero"/> / <see cref="UnitRole.Titan"/>).
    /// Runtime-only mechanics (burn, bloodrage, last stand, vs-building damage,
    /// caster cooldowns, regen) are handled by the combat system instead.
    /// </summary>
    public static class TowerTrackUnitRules
    {
        public static UnitCombatStats Apply(UnitCombatStats stats, MatchPlayerState player)
        {
            if (player == null || stats.Role is UnitRole.Hero or UnitRole.Titan)
            {
                return stats;
            }

            var armorBonus = 0f;
            if (TowerTrackRules.RoleMatches(1, stats.Role))
            {
                armorBonus += player.GetTowerTrackLevel(1) * TowerTrackRules.BulwarkArmorPerLevel;
            }

            var rangeBonus = 0f;
            if (TowerTrackRules.RoleMatches(5, stats.Role))
            {
                rangeBonus += player.GetTowerTrackLevel(5) * TowerTrackRules.AttackRangeBonusPerLevel;
            }

            var moveMultiplier = 1f;
            if (TowerTrackRules.RoleMatches(6, stats.Role))
            {
                moveMultiplier += player.GetTowerTrackLevel(6) * TowerTrackRules.MoveSpeedPercentPerLevel;
            }

            return new UnitCombatStats(
                stats.Role,
                stats.MaxHp,
                stats.Armor + armorBonus,
                stats.DamageMin,
                stats.DamageMax,
                stats.AttackSpeed,
                stats.AttackRange + rangeBonus,
                stats.MoveSpeed * moveMultiplier,
                stats.GoldBounty,
                stats.MaxMana);
        }
    }
}
