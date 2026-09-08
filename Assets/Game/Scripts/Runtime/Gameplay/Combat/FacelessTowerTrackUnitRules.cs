using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Applies Faceless tower upgrade tracks (FACELESS-017) to a regular unit's spawn-time stats.
    /// Never applies to champions (<see cref="UnitRole.Hero"/> / <see cref="UnitRole.Titan"/>).
    /// </summary>
    public static class FacelessTowerTrackUnitRules
    {
        public static UnitCombatStats Apply(UnitCombatStats stats, MatchPlayerState player)
        {
            if (player == null || stats.Role is UnitRole.Hero or UnitRole.Titan)
            {
                return stats;
            }

            var armorBonus = 0f;
            if (FacelessTowerTrackRules.RoleMatches(0, stats.Role))
            {
                armorBonus += player.GetTowerTrackLevel(0) * FacelessTowerTrackRules.ChitinousHideArmorPerLevel;
            }

            var maxHpMultiplier = 1f;
            if (FacelessTowerTrackRules.RoleMatches(0, stats.Role))
            {
                var level = player.GetTowerTrackLevel(0);
                if (level >= MatchEconomyRules.MaxTowerTrackLevel)
                {
                    maxHpMultiplier += FacelessTowerTrackRules.ChitinousHideMaxHpPercentAtMaxLevel;
                }
            }

            var attackSpeedMultiplier = 1f;
            if (FacelessTowerTrackRules.RoleMatches(5, stats.Role))
            {
                var level = player.GetTowerTrackLevel(5);
                if (level > 0)
                {
                    attackSpeedMultiplier += FacelessTowerTrackRules.FrenzyAttackSpeedPercentByLevel[level - 1];
                }
            }

            var moveMultiplier = 1f;
            if (FacelessTowerTrackRules.RoleMatches(7, stats.Role))
            {
                moveMultiplier += player.GetTowerTrackLevel(7) * FacelessTowerTrackRules.HollowBonesMoveSpeedPercentPerLevel;
            }

            return new UnitCombatStats(
                stats.Role,
                stats.MaxHp * maxHpMultiplier,
                stats.Armor + armorBonus,
                stats.DamageMin,
                stats.DamageMax,
                stats.AttackSpeed * attackSpeedMultiplier,
                stats.AttackRange,
                stats.MoveSpeed * moveMultiplier,
                stats.GoldBounty,
                stats.MaxMana);
        }
    }
}
