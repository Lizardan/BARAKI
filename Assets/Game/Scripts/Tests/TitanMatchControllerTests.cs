using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TitanMatchControllerTests
    {
        [Test]
        public void Research_CompletesToIdleAtBaseWithParkedUnit()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = TitanRules.RequiredMainLevel;
            HireAllHeroes(controller);

            var titan = controller.GetTitanState(0);
            Assert.AreEqual(TitanLifecycleState.Locked, titan.State);
            titan.ResearchProgressSeconds = 0f;

            controller.Tick(TitanRules.ResearchSeconds);

            Assert.AreEqual(TitanLifecycleState.IdleAtBase, titan.State);
            Assert.AreEqual(TitanRules.ResearchSeconds, titan.ResearchProgressSeconds, 0.01f);
            var parked = GetParkedTitan(controller, 0);
            Assert.IsTrue(parked.IsParkedAtBase);
            Assert.AreEqual(parked.UnitId, titan.DeployedUnitId);
        }

        [Test]
        public void Research_RequiresMainLevelThree()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = 2;
            player.Gold = 5000;
            for (var slot = 1; slot <= 2; slot++)
            {
                Assert.IsTrue(controller.TryHireHero(0, slot));
                controller.Tick(HeroRules.HireResearchSeconds);
            }

            var titan = controller.GetTitanState(0);
            controller.Tick(TitanRules.ResearchSeconds);

            Assert.AreEqual(0f, titan.ResearchProgressSeconds, 0.01f);
            Assert.AreEqual(TitanLifecycleState.Locked, titan.State);
        }

        [Test]
        public void Research_FreezesWhileHeroDeployed()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = TitanRules.RequiredMainLevel;
            HireAllHeroes(controller);

            var titan = controller.GetTitanState(0);
            titan.ResearchProgressSeconds = 0f;
            controller.Tick(60f);
            Assert.AreEqual(60f, titan.ResearchProgressSeconds, 0.01f);

            player.Gold = 5000;
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            Assert.IsTrue(controller.TryDeployHero(0, barracks.InstanceId, 1));

            controller.Tick(60f);
            Assert.AreEqual(60f, titan.ResearchProgressSeconds, 0.01f);
            Assert.AreEqual(TitanLifecycleState.Locked, titan.State);
        }

        [Test]
        public void TryDeployTitan_RequiresIdleAtBaseGoldAndBarracks()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = TitanRules.RequiredMainLevel;
            HireAllHeroes(controller);
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);

            player.Gold = 5000;
            Assert.IsFalse(controller.TryDeployTitan(0, barracks.InstanceId));

            controller.Tick(TitanRules.ResearchSeconds);
            Assert.IsNotNull(GetParkedTitan(controller, 0));

            player.Gold = TitanRules.DeployGold - 1;
            Assert.IsFalse(controller.TryDeployTitan(0, barracks.InstanceId));

            player.Gold = TitanRules.DeployGold;
            Assert.IsTrue(controller.TryDeployTitan(0, barracks.InstanceId));

            var titan = controller.GetTitanState(0);
            Assert.AreEqual(TitanLifecycleState.Deployed, titan.State);
            Assert.IsNotNull(titan.DeployedUnitId);
            Assert.AreEqual(0, player.Gold);
            var deployed = GetDeployedTitan(controller, 0);
            Assert.IsFalse(deployed.IsParkedAtBase);
            Assert.AreEqual(GameIds.Lanes.Left, deployed.LaneId);
            Assert.IsFalse(controller.TryDeployTitan(0, barracks.InstanceId));
        }

        [Test]
        public void TitanDeath_StartsPerBarracksCooldown_WithoutReResearch()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = TitanRules.RequiredMainLevel;
            HireAllHeroes(controller);
            controller.Tick(TitanRules.ResearchSeconds);

            player.Gold = 5000;
            var left = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            var right = FindBuilding(controller, 0, GameIds.Buildings.BarracksRight);
            Assert.IsTrue(controller.TryDeployTitan(0, left.InstanceId));

            var titanUnit = GetDeployedTitan(controller, 0);
            var killer = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee,
                new UnitCombatStats(UnitRole.Melee, 100f, 0f, 10f, 10f, 1f, 1f, 3.5f, 40),
                distanceAlongLane: 30f);
            controller.Combat.ResolveMeleeImpact(
                new CombatMeleeStrikeState(killer.UnitId, titanUnit.UnitId, 100000f, 0.1f));

            var titan = controller.GetTitanState(0);
            Assert.AreEqual(TitanLifecycleState.Dead, titan.State);
            Assert.IsNull(titan.DeployedUnitId);
            Assert.AreEqual(TitanRules.DeathCooldownSeconds, titan.GetDeathCooldown(left.InstanceId), 0.01f);
            Assert.AreEqual(TitanRules.ResearchSeconds, titan.ResearchProgressSeconds, 0.01f);

            Assert.IsFalse(controller.TryDeployTitan(0, left.InstanceId));

            player.Gold = TitanRules.DeployGold;
            Assert.IsTrue(controller.TryDeployTitan(0, right.InstanceId));
            Assert.AreEqual(TitanLifecycleState.Deployed, controller.GetTitanState(0).State);
        }

        [Test]
        public void TitanGainsXpOnUnitKill()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = TitanRules.RequiredMainLevel;
            HireAllHeroes(controller);
            controller.Tick(TitanRules.ResearchSeconds);

            player.Gold = 5000;
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            Assert.IsTrue(controller.TryDeployTitan(0, barracks.InstanceId));
            var titan = GetDeployedTitan(controller, 0);

            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee,
                new UnitCombatStats(UnitRole.Melee, 100f, 0f, 10f, 10f, 1f, 1f, 3.5f, 40),
                distanceAlongLane: 20f);

            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(titan.UnitId, enemy.UnitId, 500f, 0.1f));

            Assert.AreEqual(HeroLevelRules.GetKillXp(40), controller.GetTitanState(0).Xp);
        }

        [Test]
        public void TitanSpawnsWithTripleHeroStats()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = TitanRules.RequiredMainLevel;
            HireAllHeroes(controller);
            controller.Tick(TitanRules.ResearchSeconds);

            player.Gold = 5000;
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            Assert.IsTrue(controller.TryDeployTitan(0, barracks.InstanceId));
            var titan = GetDeployedTitan(controller, 0);

            Assert.AreEqual(UnitRole.Titan, titan.Stats.Role);
            Assert.AreEqual(600f * TitanRules.BaseStatMultiplier, titan.Stats.MaxHp, 0.01f);
            Assert.AreEqual(4f * TitanRules.BaseStatMultiplier, titan.Stats.Armor, 0.01f);
            Assert.AreEqual(35f * TitanRules.BaseStatMultiplier, titan.Stats.DamageMin, 0.01f);
            Assert.AreEqual(45f * TitanRules.BaseStatMultiplier, titan.Stats.DamageMax, 0.01f);
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
        }

        static void HireAllHeroes(MatchController controller)
        {
            var player = controller.Players[0];
            player.Gold = 5000;
            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                Assert.IsTrue(controller.TryHireHero(0, slot), $"Hire hero {slot} failed.");
                controller.Tick(HeroRules.HireResearchSeconds);
            }

            Assert.AreEqual(3, controller.GetHeroRoster(0).CountHired());
        }

        static MatchUnitState GetParkedTitan(MatchController controller, int ownerSlot)
        {
            foreach (var unit in controller.Combat.Units)
            {
                if (unit.Role == UnitRole.Titan && unit.OwnerSlot == ownerSlot && unit.IsParkedAtBase)
                {
                    return unit;
                }
            }

            Assert.Fail($"Parked titan for slot {ownerSlot} not found.");
            return null;
        }

        static MatchUnitState GetDeployedTitan(MatchController controller, int ownerSlot)
        {
            foreach (var unit in controller.Combat.Units)
            {
                if (unit.Role == UnitRole.Titan && unit.OwnerSlot == ownerSlot && !unit.IsParkedAtBase)
                {
                    return unit;
                }
            }

            Assert.Fail($"Deployed titan for slot {ownerSlot} not found.");
            return null;
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
