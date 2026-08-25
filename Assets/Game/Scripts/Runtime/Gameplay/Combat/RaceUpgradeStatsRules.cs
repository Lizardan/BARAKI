using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Applies race-wide main-building upgrades to a combat unit's stats at spawn time
    /// (UPG_MELEE_DMG / UPG_RANGED_DMG / UPG_ARMOR). HP/armor apply to every unit
    /// including heroes; damage % only to the matching role. Caster units instead
    /// gain flat damage per Magic level.
    /// </summary>
    public static class RaceUpgradeStatsRules
    {
        public static UnitCombatStats Apply(UnitCombatStats stats, MatchPlayerState player)
        {
            if (player == null)
            {
                return stats;
            }

            var hpBonus = player.HpArmorLevel * MatchEconomyRules.UpgradeHpPerLevel;
            var armorBonus = player.HpArmorLevel * MatchEconomyRules.UpgradeArmorPerLevel;

            var damageMultiplier = 1f;
            var flatDamageBonus = 0f;
            if (stats.Role == UnitRole.Melee)
            {
                damageMultiplier += player.MeleeDamageLevel * MatchEconomyRules.MeleeDamagePercentPerLevel;
            }
            else if (stats.Role == UnitRole.Ranged)
            {
                damageMultiplier += player.RangedDamageLevel * MatchEconomyRules.RangedDamagePercentPerLevel;
            }
            else if (stats.Role == UnitRole.Caster)
            {
                flatDamageBonus = player.MagicLevel * MatchEconomyRules.MagicDamagePerLevel;
            }

            return new UnitCombatStats(
                stats.Role,
                stats.MaxHp + hpBonus,
                stats.Armor + armorBonus,
                stats.DamageMin * damageMultiplier + flatDamageBonus,
                stats.DamageMax * damageMultiplier + flatDamageBonus,
                stats.AttackSpeed,
                stats.AttackRange,
                HumanBonusUnitRules.ApplyMarchDiscipline(player, stats.MoveSpeed),
                stats.GoldBounty,
                stats.MaxMana);
        }
    }
}
