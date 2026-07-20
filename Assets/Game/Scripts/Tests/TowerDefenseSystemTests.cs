using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TowerDefenseSystemTests
    {
        [Test]
        public void Tick_MainAndBarracks_DealDamageInRange()
        {
            var controller = CreateEarlyMatch();
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);
            var barracks = FindBuilding(controller, 0, GameIds.Buildings.BarracksCenter);
            var enemy = SpawnEnemyNear(controller, main.WorldPosition);

            var hpBefore = enemy.CurrentHp;
            Assert.IsTrue(WaitUntilDamaged(controller, enemy, hpBefore), "Main should damage enemies in range.");

            // Fresh target — prior unit may already be dead/removed from combat.
            var barracksEnemy = SpawnEnemyNear(controller, barracks.WorldPosition);
            hpBefore = barracksEnemy.CurrentHp;
            Assert.IsTrue(
                WaitUntilDamaged(controller, barracksEnemy, hpBefore),
                "Barracks should damage enemies in range.");
        }

        [Test]
        public void Tick_TowerFire_CreatesBuildingProjectileThenDamagesOnImpact()
        {
            var controller = CreateEarlyMatch();
            var tower = FindBuilding(controller, 0, GameIds.Buildings.TowerNw);
            var enemy = SpawnEnemyNear(controller, tower.WorldPosition);
            var hpBefore = enemy.CurrentHp;

            CombatProjectileState buildingShot = null;
            for (var i = 0; i < 40; i++)
            {
                controller.Tick(0.05f);
                foreach (var projectile in controller.Combat.Projectiles)
                {
                    if (projectile.IsBuildingAttack
                        && projectile.SourceBuildingInstanceId == tower.InstanceId)
                    {
                        buildingShot = projectile;
                        break;
                    }
                }

                if (buildingShot != null)
                {
                    break;
                }
            }

            Assert.IsNotNull(buildingShot, "Tower should fire a building projectile.");
            Assert.AreEqual(0, buildingShot.AttackerOwnerSlot);
            Assert.AreEqual(enemy.UnitId, buildingShot.TargetUnitId);
            Assert.AreEqual(hpBefore, enemy.CurrentHp, 0.01f, "Damage applies on impact, not on fire.");

            for (var i = 0; i < 40; i++)
            {
                controller.Tick(0.05f);
                if (enemy.CurrentHp < hpBefore)
                {
                    break;
                }
            }

            Assert.Less(enemy.CurrentHp, hpBefore);
        }

        [Test]
        public void ManualTargetLost_RetargetsOtherEnemyInRange()
        {
            var controller = CreateEarlyMatch();
            var tower = FindBuilding(controller, 0, GameIds.Buildings.TowerNw);
            var a = SpawnEnemyNear(controller, tower.WorldPosition);
            var b = SpawnEnemyNear(controller, tower.WorldPosition + new Vector3(0f, 0f, 2f));

            Assert.IsTrue(controller.TrySetTowerTarget(0, tower.InstanceId, a.UnitId));
            controller.Combat.ApplyExternalDamage(a.UnitId, 9999f, 0);

            // One defense tick: drop dead sticky target and pick another living enemy in range.
            controller.Tick(0.05f);

            var retarget = controller.Towers.GetManualTarget(tower.InstanceId);
            Assert.IsTrue(retarget.HasValue);
            Assert.AreEqual(b.UnitId, retarget.Value);
            Assert.IsTrue(b.IsAlive);
        }

        [Test]
        public void TrySetTowerTarget_AcceptsMainAsDefensiveBuilding()
        {
            var controller = CreateEarlyMatch();
            var main = FindBuilding(controller, 0, GameIds.Buildings.Main);
            var enemy = SpawnEnemyNear(controller, main.WorldPosition);
            Assert.IsTrue(controller.TrySetTowerTarget(0, main.InstanceId, enemy.UnitId));
            Assert.AreEqual(enemy.UnitId, controller.Towers.GetManualTarget(main.InstanceId));
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

            return null;
        }

        static MatchUnitState SpawnEnemyNear(MatchController controller, Vector3 near)
        {
            var enemy = controller.Combat.SpawnUnit(
                1,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                new UnitCombatStats(UnitRole.Melee, 500f, 1f, 10f, 14f, 1f, 1.5f, 3.5f, 20));
            enemy.WorldPosition = near + new Vector3(2f, 0f, 0f);
            // Keep test targets stationary (combat Tick would march them off lane).
            enemy.IsParkedAtBase = true;
            return enemy;
        }

        static bool WaitUntilDamaged(MatchController controller, MatchUnitState enemy, float hpBefore)
        {
            for (var i = 0; i < 80; i++)
            {
                controller.Tick(0.05f);
                if (enemy.CurrentHp < hpBefore)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
