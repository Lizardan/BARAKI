using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HeroAbilityRulesTests
    {
        [Test]
        public void GatherEnemiesInRadius_ReturnsOnlyOpponentsWithinRange()
        {
            var hero = MakeUnit(ownerSlot: 0, UnitRole.Hero, new Vector3(0f, 0f, 0f));
            var nearbyEnemy = MakeUnit(1, UnitRole.Melee, new Vector3(3f, 0f, 0f));
            var farEnemy = MakeUnit(1, UnitRole.Melee, new Vector3(20f, 0f, 0f));
            var ally = MakeUnit(0, UnitRole.Melee, new Vector3(2f, 0f, 0f));
            var units = new List<MatchUnitState> { hero, nearbyEnemy, farEnemy, ally };

            var enemies = HeroAbilityRules.GatherEnemiesInRadius(hero, units, 4f);

            Assert.AreEqual(1, enemies.Count);
            Assert.AreEqual(nearbyEnemy.UnitId, enemies[0].UnitId);
        }

        [Test]
        public void GatherAlliesInRadius_IncludesSelfAndAllies()
        {
            var hero = MakeUnit(0, UnitRole.Hero, new Vector3(0f, 0f, 0f));
            var ally = MakeUnit(0, UnitRole.Melee, new Vector3(3f, 0f, 0f));
            var enemy = MakeUnit(1, UnitRole.Melee, new Vector3(2f, 0f, 0f));
            var units = new List<MatchUnitState> { hero, ally, enemy };

            var allies = HeroAbilityRules.GatherAlliesInRadius(hero, units, 6f);

            Assert.AreEqual(2, allies.Count);
            Assert.IsTrue(allies.Contains(hero));
            Assert.IsTrue(allies.Contains(ally));
        }

        [Test]
        public void ApplyHeal_ClampsToMaxHp()
        {
            var full = HeroAbilityRules.ApplyHeal(600f, 600f);
            Assert.AreEqual(600f, full);

            var partial = HeroAbilityRules.ApplyHeal(500f, 600f);
            Assert.AreEqual(600f, partial);

            var low = HeroAbilityRules.ApplyHeal(10f, 600f);
            Assert.AreEqual(10f + HeroAbilityRules.HealAmount, low);
        }

        static MatchUnitState MakeUnit(int ownerSlot, UnitRole role, Vector3 position)
        {
            var stats = new UnitCombatStats(role, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);
            var unit = new MatchUnitState(
                0,
                ownerSlot,
                GameIds.Lanes.Center,
                role,
                stats,
                stats.MaxHp,
                position);
            return unit;
        }
    }
}
