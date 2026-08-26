using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TowerTrackResearchTests
    {
        [Test]
        public void StartTowerTrack_DeductsGoldAndEnqueues()
        {
            var controller = CreateEarlyMatch();
            GiveGold(controller);
            var tower = FindBuilding(controller, 0, GameIds.Buildings.TowerNw);
            var goldBefore = controller.Players[0].Gold;

            Assert.IsTrue(controller.TryStartResearch(
                0,
                tower.InstanceId,
                GameIds.Upgrades.TowerHumanFlamingArrows));
            Assert.AreEqual(goldBefore - 500, controller.Players[0].Gold);
            Assert.IsTrue(controller.Research.TryGetActive(tower.InstanceId, out var research));
            Assert.AreEqual(GameIds.Upgrades.TowerHumanFlamingArrows, research.UpgradeId);
            Assert.AreEqual(500, research.CostPaid);
            Assert.AreEqual(45f, research.DurationSeconds);
        }

        [Test]
        public void TowerQueue_HoldsSingleJob()
        {
            var controller = CreateEarlyMatch();
            GiveGold(controller);
            var tower = FindBuilding(controller, 0, GameIds.Buildings.TowerNw);

            Assert.IsTrue(controller.TryStartResearch(
                0,
                tower.InstanceId,
                GameIds.Upgrades.TowerHumanFlamingArrows));
            Assert.IsFalse(controller.TryStartResearch(
                0,
                tower.InstanceId,
                GameIds.Upgrades.TowerHumanBulwark));
            Assert.AreEqual(1, controller.GetResearchQueueLimit(GameIds.Buildings.TowerNw));
        }

        [Test]
        public void DifferentTowers_ResearchDifferentTracksInParallel()
        {
            var controller = CreateEarlyMatch();
            GiveGold(controller);
            var nw = FindBuilding(controller, 0, GameIds.Buildings.TowerNw);
            var ne = FindBuilding(controller, 0, GameIds.Buildings.TowerNe);

            Assert.IsTrue(controller.TryStartResearch(0, nw.InstanceId, GameIds.Upgrades.TowerHumanFlamingArrows));
            Assert.IsTrue(controller.TryStartResearch(0, ne.InstanceId, GameIds.Upgrades.TowerHumanBulwark));
            Assert.IsTrue(controller.Research.HasActive(nw.InstanceId));
            Assert.IsTrue(controller.Research.HasActive(ne.InstanceId));
        }

        [Test]
        public void RuinsTower_RejectsResearch()
        {
            var controller = CreateEarlyMatch();
            var tower = FindBuilding(controller, 0, GameIds.Buildings.TowerSw);
            tower.ApplyDamage(10_000f);
            Assert.IsTrue(tower.IsRuins);

            Assert.IsFalse(controller.TryStartResearch(
                0,
                tower.InstanceId,
                GameIds.Upgrades.TowerHumanFlamingArrows));
        }

        [Test]
        public void NonTowerBuilding_RejectsTowerTrack()
        {
            var controller = CreateEarlyMatch();
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);

            Assert.IsFalse(controller.TryStartResearch(
                0,
                main.InstanceId,
                GameIds.Upgrades.TowerHumanFieldMedics));
        }

        [Test]
        public void Completion_IncrementsTrackLevel_Sequentially()
        {
            var controller = CreateEarlyMatch();
            var tower = FindBuilding(controller, 0, GameIds.Buildings.TowerSe);
            controller.Players[0].Gold = 10_000;

            Assert.IsTrue(controller.TryStartResearch(0, tower.InstanceId, GameIds.Upgrades.TowerHumanLastStand));
            TickUntilQueueEmpty(controller, tower.InstanceId);
            Assert.AreEqual(1, controller.Players[0].GetTowerTrackLevel(GameIds.Upgrades.TowerHumanLastStand));

            // L2 requires L1 — now allowed.
            Assert.IsTrue(controller.TryStartResearch(0, tower.InstanceId, GameIds.Upgrades.TowerHumanLastStand));
            TickUntilQueueEmpty(controller, tower.InstanceId);
            Assert.AreEqual(2, controller.Players[0].GetTowerTrackLevel(GameIds.Upgrades.TowerHumanLastStand));

            // Cap at L3.
            Assert.IsTrue(controller.TryStartResearch(0, tower.InstanceId, GameIds.Upgrades.TowerHumanLastStand));
            TickUntilQueueEmpty(controller, tower.InstanceId);
            Assert.AreEqual(3, controller.Players[0].GetTowerTrackLevel(GameIds.Upgrades.TowerHumanLastStand));
            Assert.IsFalse(controller.TryStartResearch(0, tower.InstanceId, GameIds.Upgrades.TowerHumanLastStand));
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
        }

        static void GiveGold(MatchController controller) => controller.Players[0].Gold = 10_000;

        static void TickUntilQueueEmpty(MatchController controller, int buildingInstanceId)
        {
            for (var i = 0; i < 200 && controller.Research.HasActive(buildingInstanceId); i++)
            {
                controller.Tick(1f);
            }

            Assert.IsFalse(
                controller.Research.HasActive(buildingInstanceId),
                "Research queue did not drain");
        }

        static BuildingState FindBuilding(MatchController controller, int ownerSlot, string buildingId)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.BuildingId == buildingId)
                {
                    return building;
                }
            }

            Assert.Fail($"Building {buildingId} for slot {ownerSlot} not found.");
            return null;
        }
    }
}
