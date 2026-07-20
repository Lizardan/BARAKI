using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MeleeBuildingAttackTests
    {
        [Test]
        public void BeginBuildingAttack_Melee_CreatesStrikeForAnimationAndDamage()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();

            var building = FindBuilding(controller, 1, GameIds.Buildings.BarracksCenter);
            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 200f,
                armor: 0f,
                damageMin: 50f,
                damageMax: 50f,
                attackSpeed: 10f,
                attackRange: 1.5f,
                moveSpeed: 4f,
                goldBounty: 1);
            var melee = controller.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, 10f);
            var engage = BuildingRules.GetEngageRadius(building.BuildingId);
            melee.WorldPosition = building.WorldPosition + Vector3.forward * (engage + 0.5f);
            melee.CurrentTargetBuildingInstanceId = building.InstanceId;
            melee.TargetScanCooldown = 1f;
            melee.AttackCooldownRemaining = 0f;

            var hpBefore = building.CurrentHp;
            controller.Combat.Tick(0.05f);
            Assert.Greater(controller.Combat.MeleeStrikes.Count, 0);
            Assert.AreEqual(melee.UnitId, controller.Combat.MeleeStrikes[0].AttackerUnitId);
            Assert.AreEqual(building.InstanceId, controller.Combat.MeleeStrikes[0].TargetBuildingInstanceId);

            controller.Combat.Tick(CombatAttackRules.MeleeStrikeDuration + 0.01f);
            Assert.Less(building.CurrentHp, hpBefore);
        }

        [Test]
        public void GetBuildingAttackReach_IncludesUnitBodySlack()
        {
            Assert.Greater(CombatRules.GetBuildingAttackReach(1.5f), 1.5f);
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
    }
}
