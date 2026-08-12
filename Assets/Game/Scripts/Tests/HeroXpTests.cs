using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HeroXpTests
    {
        [Test]
        public void HeroGainsXpOnUnitKill()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 1500;
            controller.TryHireHero(0, 1);
            controller.Tick(HeroRules.HireResearchSeconds);
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            controller.TryDeployHero(0, barracks.InstanceId, 1);
            var hero = GetDeployedHero(controller, 0);

            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee,
                new UnitCombatStats(UnitRole.Melee, 100f, 0f, 10f, 10f, 1f, 1f, 3.5f, 40),
                distanceAlongLane: 20f);

            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(hero.UnitId, enemy.UnitId, 500f, 0.1f));

            Assert.AreEqual(40, controller.GetHeroRoster(0).Get(1).Xp);
        }

        [Test]
        public void HeroGainsXpOnEnemyBuildingDestroyedByOwnerUnits()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 500;
            controller.TryHireHero(0, 1);
            controller.Tick(HeroRules.HireResearchSeconds);

            var enemyBarracks = FindBuilding(controller, 1, GameIds.Buildings.BarracksRight);
            Assert.IsTrue(controller.Buildings.TryApplyDamage(enemyBarracks.InstanceId, 100000f, 0));

            var slot = controller.GetHeroRoster(0).Get(1);
            Assert.AreEqual(HeroLevelRules.GetBuildingKillXp(), slot.Xp);
        }

        [Test]
        public void HeroLevelUpsAndDeploysAtSlotLevel()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 1500;
            controller.TryHireHero(0, 1);
            controller.Tick(HeroRules.HireResearchSeconds);

            var slot = controller.GetHeroRoster(0).Get(1);
            slot.AddXp(HeroLevelRules.XpToNext(1));

            Assert.AreEqual(2, slot.Level);
            Assert.AreEqual(0, slot.Xp);

            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            Assert.IsTrue(controller.TryDeployHero(0, barracks.InstanceId, 1));
            var hero = GetDeployedHero(controller, 0);

            Assert.AreEqual(2, hero.Level);
            Assert.AreEqual(600f + HeroLevelRules.MaxHpPerLevel, hero.Stats.MaxHp, 0.001f);
        }

        [Test]
        public void HeroXpSurvivesDeath()
        {
            var controller = CreateEarlyMatch();
            controller.Players[0].Gold = 1500;
            controller.TryHireHero(0, 1);
            controller.Tick(HeroRules.HireResearchSeconds);
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksLeft);
            controller.TryDeployHero(0, barracks.InstanceId, 1);
            var hero = GetDeployedHero(controller, 0);

            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee,
                new UnitCombatStats(UnitRole.Melee, 100f, 0f, 10f, 10f, 1f, 1f, 3.5f, 40),
                distanceAlongLane: 20f);
            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(hero.UnitId, enemy.UnitId, 500f, 0.1f));

            var slot = controller.GetHeroRoster(0).Get(1);
            Assert.AreEqual(40, slot.Xp);

            var enemyKiller = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee,
                new UnitCombatStats(UnitRole.Melee, 100f, 0f, 10f, 10f, 1f, 1f, 3.5f, 40),
                distanceAlongLane: 30f);
            controller.Combat.ResolveMeleeImpact(
                new CombatMeleeStrikeState(enemyKiller.UnitId, hero.UnitId, 5000f, 0.1f));

            Assert.AreEqual(HeroLifecycleState.Dead, slot.State);
            Assert.AreEqual(40, slot.Xp);
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
        }

        static MatchUnitState GetDeployedHero(MatchController controller, int ownerSlot)
        {
            foreach (var unit in controller.Combat.Units)
            {
                if (unit.IsHero && unit.OwnerSlot == ownerSlot && !unit.IsParkedAtBase)
                {
                    return unit;
                }
            }

            Assert.Fail($"Deployed hero for slot {ownerSlot} not found.");
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
