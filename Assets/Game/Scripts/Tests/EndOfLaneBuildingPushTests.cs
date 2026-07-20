using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EndOfLaneBuildingPushTests
    {
        [Test]
        public void AtMidFinish_WithoutAggro_AcquiresFarBarracksAndChases()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            const int attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var enemySlot = lane.OpponentSlot;

            // Leave only a flank barracks standing — past Main, outside default aggro (~8).
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot != enemySlot || !building.IsIntact)
                {
                    continue;
                }

                if (building.BuildingId == GameIds.Buildings.BarracksLeft)
                {
                    continue;
                }

                controller.Buildings.TryApplyDamage(building.InstanceId, 99999f, attacker);
            }

            BuildingState flankBarracks = null;
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == enemySlot
                    && building.BuildingId == GameIds.Buildings.BarracksLeft
                    && building.IsIntact)
                {
                    flankBarracks = building;
                    break;
                }
            }

            Assert.IsNotNull(flankBarracks);

            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, GameIds.Lanes.Center, out var route));
            var stats = new UnitCombatStats(
                UnitRole.Melee, 200f, 0f, 5f, 5f, 1f, 1.5f, 8f, 1);
            var unit = controller.Combat.SpawnUnit(
                attacker, GameIds.Lanes.Center, UnitRole.Melee, stats, route.TotalLength);
            unit.WorldPosition = route.Path.End;
            unit.MarchProgressDistance = route.TotalLength;

            var surfaceToBarracks = BuildingRules.GetSurfaceDistance(
                Vector3.Distance(
                    Flat(unit.WorldPosition),
                    Flat(flankBarracks.WorldPosition)),
                flankBarracks.BuildingId);
            Assert.Greater(surfaceToBarracks, CombatRules.GetAggroRadius(stats),
                "Barracks must sit outside normal aggro so the bug is reproducible.");

            // Force a scan tick.
            unit.TargetScanCooldown = 0f;
            controller.Combat.Tick(0.05f);

            Assert.AreEqual(flankBarracks.InstanceId, unit.CurrentTargetBuildingInstanceId,
                "End-of-mid push should lock the remaining barracks outside aggro.");
            Assert.AreEqual(UnitBehaviorState.Chase, unit.BehaviorState);

            var before = Flat(unit.WorldPosition);
            for (var i = 0; i < 40; i++)
            {
                controller.Combat.Tick(0.25f);
            }

            Assert.Greater(Vector3.Distance(before, Flat(unit.WorldPosition)), 2f,
                "Unit should leave the mid finish point toward the barracks.");
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
