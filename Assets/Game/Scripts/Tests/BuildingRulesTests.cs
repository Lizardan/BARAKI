using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

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
        public void GetEngageRadius_UsesFootprintHalfDiameter()
        {
            Assert.Greater(BuildingRules.GetEngageRadius(GameIds.Buildings.Main), 1f);
            Assert.Greater(
                BuildingRules.GetEngageRadius(GameIds.Buildings.BarracksCenter),
                BuildingRules.GetEngageRadius(GameIds.Buildings.TowerNw));
        }

        [Test]
        public void CanLaneAttackBuilding_AllowsAnyNonOwnedIntactBuilding()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var laneOpponentBarracks = new BuildingState(
                1,
                ownerSlot: 3,
                GameIds.Buildings.BarracksRight,
                worldPosition: Vector3.zero,
                maxHp: 800f,
                armor: 2f);
            var nextLivingBarracks = new BuildingState(
                2,
                ownerSlot: 1,
                GameIds.Buildings.BarracksLeft,
                worldPosition: Vector3.zero,
                maxHp: 800f,
                armor: 2f);

            Assert.IsTrue(BuildingRules.CanLaneAttackBuilding(
                attackerOwnerSlot: 0,
                GameIds.Lanes.Left,
                laneOpponentBarracks,
                graph));
            Assert.IsTrue(BuildingRules.CanLaneAttackBuilding(
                attackerOwnerSlot: 0,
                GameIds.Lanes.Left,
                nextLivingBarracks,
                graph));

            var friendly = new BuildingState(
                3,
                ownerSlot: 0,
                GameIds.Buildings.BarracksLeft,
                worldPosition: Vector3.zero,
                maxHp: 800f,
                armor: 2f);
            Assert.IsFalse(BuildingRules.CanLaneAttackBuilding(
                0,
                GameIds.Lanes.Left,
                friendly,
                graph));
        }

        [Test]
        public void GetSurfaceDistance_SubtractsEngageRadius()
        {
            var radius = BuildingRules.GetEngageRadius(GameIds.Buildings.Main);
            Assert.AreEqual(0f, BuildingRules.GetSurfaceDistance(radius * 0.5f, GameIds.Buildings.Main), 0.001f);
            Assert.AreEqual(5f, BuildingRules.GetSurfaceDistance(radius + 5f, GameIds.Buildings.Main), 0.001f);
        }
    }
}
