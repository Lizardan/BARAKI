using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HeroMatchControllerTests
    {
        [Test]
        public void TryHireHero_StartsResearchAndCompletesToIdle()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 500;
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);

            Assert.IsTrue(controller.TryHireHero(0, 1));
            Assert.AreEqual(0, controller.Players[0].Gold);
            Assert.AreEqual(HeroLifecycleState.None, controller.GetHeroRoster(0).Get(1).State);
            Assert.IsTrue(controller.TryGetResearch(main.InstanceId, out var research));
            Assert.AreEqual(HeroRules.BuildHireUpgradeId(1), research.UpgradeId);

            controller.Tick(HeroRules.HireResearchSeconds);

            Assert.IsFalse(controller.TryGetResearch(main.InstanceId, out _));
            Assert.AreEqual(HeroLifecycleState.IdleAtBase, controller.GetHeroRoster(0).Get(1).State);
            Assert.AreEqual(1, controller.Combat.Units.Count);
            Assert.IsTrue(controller.Combat.Units[0].IsParkedAtBase);
            Assert.AreEqual(controller.Combat.Units[0].UnitId, controller.GetHeroRoster(0).Get(1).DeployedUnitId);
        }

        [Test]
        public void TryStartResearch_HeroHire_EnqueuesWhileMainHasPassiveResearch()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 1000;
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);

            Assert.IsTrue(controller.TryStartResearch(
                0,
                main.InstanceId,
                GameIds.Upgrades.MainPassiveGold));
            Assert.IsTrue(controller.TryHireHero(0, 1));
            Assert.AreEqual(HeroLifecycleState.None, controller.GetHeroRoster(0).Get(1).State);
            Assert.IsTrue(controller.TryGetResearchQueue(main.InstanceId, out var queue));
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(HeroRules.BuildHireUpgradeId(1), queue[1].UpgradeId);
        }

        [Test]
        public void TryDeployHero_SpawnsHeroUnit()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 1500;
            Assert.IsTrue(controller.TryHireHero(0, 1));
            controller.Tick(HeroRules.HireResearchSeconds);

            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            Assert.AreEqual(1, controller.Combat.Units.Count);
            Assert.IsTrue(controller.Combat.Units[0].IsParkedAtBase);

            Assert.IsTrue(controller.TryDeployHero(0, barracks.InstanceId, 1));
            Assert.AreEqual(0, controller.Players[0].Gold);
            Assert.AreEqual(HeroLifecycleState.Deployed, controller.GetHeroRoster(0).Get(1).State);
            Assert.AreEqual(1, controller.Combat.Units.Count);
            Assert.AreEqual(UnitRole.Hero, controller.Combat.Units[0].Role);
            Assert.IsFalse(controller.Combat.Units[0].IsParkedAtBase);
            Assert.AreEqual(GameIds.Lanes.Left, controller.Combat.Units[0].LaneId);
        }

        [Test]
        public void TrySetTowerTarget_AcceptsEnemyInPlay()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 1500;
            controller.TryHireHero(0, 1);
            controller.Tick(HeroRules.HireResearchSeconds);

            var barracks = FindBuilding(controller, 1, GameIds.Buildings.BarracksLeft);
            controller.Players[1].Gold = 1500;
            controller.TryHireHero(1, 1);
            controller.Tick(HeroRules.HireResearchSeconds);
            controller.TryDeployHero(1, barracks.InstanceId, 1);

            var tower = FindBuilding(controller, 0, GameIds.Buildings.TowerNw);
            MatchUnitState enemy = null;
            foreach (var unit in controller.Combat.Units)
            {
                if (unit.OwnerSlot == 1)
                {
                    enemy = unit;
                    break;
                }
            }

            Assert.IsNotNull(enemy);
            // Place enemy in tower range for sticky target validation.
            enemy.WorldPosition = tower.WorldPosition + new UnityEngine.Vector3(TowerCombatRules.Range * 0.5f, 0f, 0f);
            Assert.IsTrue(controller.TrySetTowerTarget(0, tower.InstanceId, enemy.UnitId));
            Assert.AreEqual(enemy.UnitId, controller.Towers.GetManualTarget(tower.InstanceId));
        }

        [Test]
        public void TryEliminateForDisconnect_MarksPlayerAndEndsWhenLastStanding()
        {
            var controller = CreateEarlyMatch();
            Assert.IsTrue(controller.TryEliminateForDisconnect(1));
            Assert.IsTrue(controller.Players[1].IsEliminated);
            Assert.AreEqual(MatchPhase.End, controller.Phase);
            Assert.AreEqual(0, controller.WinnerSlot);
        }

        [Test]
        public void DebugCompleteResearchForOwner_FinishesHeroHireImmediately()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 500;
            Assert.IsTrue(controller.TryHireHero(0, 1));

            controller.DebugCompleteResearchForOwner(0);

            Assert.AreEqual(HeroLifecycleState.IdleAtBase, controller.GetHeroRoster(0).Get(1).State);
        }

        [Test]
        public void DebugBumpPassiveGold_IncrementsWithinCap()
        {
            var controller = CreateEarlyMatch();
            Assert.IsTrue(controller.DebugBumpPassiveGold(0));
            Assert.AreEqual(1, controller.Players[0].PassiveGoldLevel);
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
