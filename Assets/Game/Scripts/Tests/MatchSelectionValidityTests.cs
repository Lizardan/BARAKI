using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchSelectionValidityTests
    {
        [Test]
        public void IsUnitTargetAlive_ReturnsFalse_WhenUnitMissing()
        {
            var units = new List<MatchUnitState>();

            Assert.IsFalse(MatchSelectionTargetRules.IsUnitTargetAlive(units, 7));
        }

        [Test]
        public void IsUnitTargetAlive_ReturnsFalse_WhenUnitDead()
        {
            var units = new List<MatchUnitState>
            {
                CreateUnit(7, 0f),
            };

            Assert.IsFalse(MatchSelectionTargetRules.IsUnitTargetAlive(units, 7));
        }

        [Test]
        public void IsUnitTargetAlive_ReturnsTrue_WhenUnitAlive()
        {
            var units = new List<MatchUnitState>
            {
                CreateUnit(7, 50f),
            };

            Assert.IsTrue(MatchSelectionTargetRules.IsUnitTargetAlive(units, 7));
        }

        static MatchUnitState CreateUnit(int unitId, float currentHp)
        {
            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 2f,
                attackSpeed: 1f,
                attackRange: 1f,
                moveSpeed: 1f,
                goldBounty: 1);

            return new MatchUnitState(
                unitId,
                ownerSlot: 0,
                laneId: "center",
                role: UnitRole.Melee,
                stats,
                currentHp,
                Vector3.zero);
        }
    }
}
