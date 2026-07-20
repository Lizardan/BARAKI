using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchEconomyControllerTests
    {
        [Test]
        public void TryStartBarracksLevelResearch_SpendsGoldAndCompletes()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.Gold = 1000;
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);

            Assert.IsTrue(controller.TryStartResearch(
                player.SlotIndex,
                barracks.InstanceId,
                GameIds.Upgrades.BarracksLevel));

            Assert.AreEqual(0, player.Gold);
            Assert.IsTrue(controller.TryGetResearch(barracks.InstanceId, out var research));
            Assert.AreEqual(GameIds.Upgrades.BarracksLevel, research.UpgradeId);

            controller.Tick(5f);

            Assert.IsFalse(controller.TryGetResearch(barracks.InstanceId, out _));
            Assert.AreEqual(2, controller.WaveScheduler.GetBarracks(0, GameIds.Buildings.BarracksLeft).Level);
        }

        [Test]
        public void TryStartBarracksLevelResearch_RejectsInsufficientGold()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 999;
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);

            Assert.IsFalse(controller.TryStartResearch(
                0,
                barracks.InstanceId,
                GameIds.Upgrades.BarracksLevel));
            Assert.AreEqual(999, controller.Players[0].Gold);
        }

        [Test]
        public void TryStartPassiveGoldResearch_CompletesAndGrantsTickIncome()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.Gold = 200;
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);

            Assert.IsTrue(controller.TryStartResearch(
                0,
                main.InstanceId,
                GameIds.Upgrades.MainPassiveGold));

            controller.Tick(25f);
            Assert.AreEqual(1, player.PassiveGoldLevel);
            Assert.AreEqual(0, player.Gold);

            controller.Tick(30f);
            Assert.AreEqual(25, player.Gold);
        }

        [Test]
        public void TryStartResearch_EnqueuesSecondAndThirdPassiveGold()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 800;
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);

            Assert.IsTrue(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.MainPassiveGold));
            Assert.IsTrue(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.MainPassiveGold));
            Assert.IsTrue(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.MainPassiveGold));
            Assert.IsFalse(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.MainPassiveGold));
            Assert.AreEqual(200, controller.Players[0].Gold);
            Assert.IsTrue(controller.TryGetResearchQueue(main.InstanceId, out var queue));
            Assert.AreEqual(3, queue.Count);
        }

        [Test]
        public void TryStartResearch_PassiveGoldQueueCompletesToLevelTwo()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 400;
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);

            Assert.IsTrue(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.MainPassiveGold));
            Assert.IsTrue(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.MainPassiveGold));

            controller.Tick(MatchEconomyRules.PassiveGoldUpgradeSeconds);
            Assert.AreEqual(1, controller.Players[0].PassiveGoldLevel);
            Assert.IsTrue(controller.TryGetResearch(main.InstanceId, out _));

            controller.Tick(MatchEconomyRules.PassiveGoldUpgradeSeconds);
            Assert.AreEqual(2, controller.Players[0].PassiveGoldLevel);
            Assert.IsFalse(controller.TryGetResearch(main.InstanceId, out _));
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
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
