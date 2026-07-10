using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BuildingRulesTests
    {
        [Test]
        public void GetMaxHp_MatchesGddBaseline()
        {
            Assert.AreEqual(2000f, BuildingRules.GetMaxHp(GameIds.Buildings.Main));
            Assert.AreEqual(800f, BuildingRules.GetMaxHp(GameIds.Buildings.BarracksCenter));
            Assert.AreEqual(600f, BuildingRules.GetMaxHp(GameIds.Buildings.TowerNw));
        }

        [Test]
        public void CanSiegeTarget_RespectsLaneBinding()
        {
            Assert.IsTrue(BuildingRules.CanSiegeTarget(
                GameIds.Lanes.Left,
                GameIds.Buildings.BarracksLeft));
            Assert.IsFalse(BuildingRules.CanSiegeTarget(
                GameIds.Lanes.Left,
                GameIds.Buildings.BarracksCenter));
            Assert.IsTrue(BuildingRules.CanSiegeTarget(
                GameIds.Lanes.Center,
                GameIds.Buildings.Main));
            Assert.IsFalse(BuildingRules.CanSiegeTarget(
                GameIds.Lanes.Left,
                GameIds.Buildings.Main));
        }
    }
}
