using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TowerCombatRulesTests
    {
        [Test]
        public void IsInRange_UsesHorizontalDistance()
        {
            var tower = new Vector3(0f, 2f, 0f);
            Assert.IsTrue(TowerCombatRules.IsInRange(tower, new Vector3(12f, 0f, 0f)));
            Assert.IsFalse(TowerCombatRules.IsInRange(tower, new Vector3(12.1f, 0f, 0f)));
        }

        [Test]
        public void CanTargetUnit_RejectsAlliesAndDead()
        {
            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 2f, 1f, 1f, 3f, 10);
            var enemy = new MatchUnitState(1, 1, "LANE_CENTER", UnitRole.Melee, stats, 50f, Vector3.zero);
            var ally = new MatchUnitState(2, 0, "LANE_CENTER", UnitRole.Melee, stats, 50f, Vector3.zero);
            var dead = new MatchUnitState(3, 1, "LANE_CENTER", UnitRole.Melee, stats, 0f, Vector3.zero);

            Assert.IsTrue(TowerCombatRules.CanTargetUnit(0, enemy));
            Assert.IsFalse(TowerCombatRules.CanTargetUnit(0, ally));
            Assert.IsFalse(TowerCombatRules.CanTargetUnit(0, dead));
        }
    }
}
