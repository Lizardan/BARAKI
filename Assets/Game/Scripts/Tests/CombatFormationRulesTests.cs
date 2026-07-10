using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CombatFormationRulesTests
    {        [Test]
        public void BuildRowSpawnFormationOffset_UsesLateralSpread()
        {
            var path = new LanePath(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 20f),
            });
            var slot = new SquadSpawnSlot(UnitRole.Melee, rowIndex: 0, indexInRow: 1, countInRow: 3);
            var random = new System.Random(7);

            var offset = CombatFormationRules.BuildRowSpawnFormationOffset(path, slot, random, 6f, unitIndex: 3);
            var lateral = CombatFormationRules.GetLateralOffset(path, 6f, offset);

            Assert.Greater(Mathf.Abs(lateral), 0.001f);
        }

        [Test]
        public void SampleSpawnLateralOffset_StaysWithinSpread()
        {
            var random = new System.Random(42);
            for (var i = 0; i < 20; i++)
            {
                var offset = CombatFormationRules.SampleSpawnLateralOffset(random, i);
                Assert.GreaterOrEqual(offset, -CombatFormationRules.SpawnLateralSpread);
                Assert.LessOrEqual(offset, CombatFormationRules.SpawnLateralSpread);
            }
        }

        [Test]
        public void GetSlotColor_ReturnsFourDistinctMvpColors()
        {
            var red = MatchPlayerColors.GetSlotColor(0);
            var blue = MatchPlayerColors.GetSlotColor(1);
            var green = MatchPlayerColors.GetSlotColor(2);
            var yellow = MatchPlayerColors.GetSlotColor(3);

            Assert.Greater(red.r, red.b);
            Assert.Greater(blue.b, blue.r);
            Assert.Greater(green.g, green.r);
            Assert.Greater(yellow.r, yellow.b);
            Assert.AreNotEqual(red, blue);
        }

        [Test]
        public void ReprojectFormationOffset_DropsForwardDriftOnCurvedPath()
        {
            var path = new LanePath(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f),
            });

            const float distanceOnTurn = 12f;
            var right = CombatFormationRules.GetLaneRight(path, distanceOnTurn);
            var offset = right * 2f;
            var lateral = CombatFormationRules.GetLateralOffset(path, distanceOnTurn, offset);
            Assert.AreEqual(2f, lateral, 0.05f);

            var forward = path.EvaluateDirectionAtDistance(distanceOnTurn);
            var drifted = offset + forward * 1.5f;
            var reprojected = CombatFormationRules.ReprojectFormationOffset(path, distanceOnTurn, drifted);
            var lateralAfter = CombatFormationRules.GetLateralOffset(path, distanceOnTurn, reprojected);

            Assert.AreEqual(2f, lateralAfter, 0.05f);
            Assert.Less(Mathf.Abs(Vector3.Dot(reprojected.normalized, forward)), 0.15f);
        }

        [Test]
        public void ClampLateralDelta_BlocksMovementIntoRoadBoundary()
        {
            var current = CombatFormationRules.MaxLateralOffset;
            var delta = CombatFormationRules.ClampLateralDelta(current, 1f);
            Assert.AreEqual(0f, delta, 0.001f);

            var negativeCurrent = -CombatFormationRules.MaxLateralOffset;
            var negativeDelta = CombatFormationRules.ClampLateralDelta(negativeCurrent, -1f);
            Assert.AreEqual(0f, negativeDelta, 0.001f);
        }

        [Test]
        public void Tick_PackMarchesAtUniformSpeedWhenSpaced()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            const float moveSpeed = 6f;
            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: moveSpeed,
                goldBounty: 1);

            var leader = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 0f);
            var middle = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 5f);
            var rear = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 10f);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane);
            var route = LaneRoute.FromPath(lane.Path);

            const float deltaTime = 0.1f;
            const int ticks = 20;
            for (var i = 0; i < ticks; i++)
            {
                combat.Tick(deltaTime);
            }

            var expected = moveSpeed * deltaTime * ticks;
            var leaderDistance = route.ProjectDistance(leader.WorldPosition);
            var middleDistance = route.ProjectDistance(middle.WorldPosition);
            var rearDistance = route.ProjectDistance(rear.WorldPosition);

            Assert.Greater(leaderDistance, expected * 0.85f);
            Assert.Greater(middleDistance, leaderDistance);
            Assert.Greater(rearDistance, middleDistance);

            Assert.GreaterOrEqual(
                Vector3.Distance(leader.WorldPosition, middle.WorldPosition),
                CombatFormationRules.MinUnitSeparation * 0.85f);
            Assert.GreaterOrEqual(
                Vector3.Distance(middle.WorldPosition, rear.WorldPosition),
                CombatFormationRules.MinUnitSeparation * 0.85f);
        }

        [Test]
        public void Tick_FriendlyUnits_DoNotOverlapOnSameLane()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 6f,
                goldBounty: 1);

            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 0f);
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 0.2f, formationOffset: new Vector3(0.1f, 0f, 0f));
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 0.4f, formationOffset: new Vector3(-0.1f, 0f, 0f));

            for (var i = 0; i < 30; i++)
            {
                combat.Tick(0.1f);
            }

            var units = combat.Units;
            for (var i = 0; i < units.Count; i++)
            {
                for (var j = i + 1; j < units.Count; j++)
                {
                    Assert.IsTrue(combat.TryGetUnitWorldPosition(units[i], out var a));
                    Assert.IsTrue(combat.TryGetUnitWorldPosition(units[j], out var b));
                    a.y = 0f;
                    b.y = 0f;
                    Assert.GreaterOrEqual(
                        Vector3.Distance(a, b),
                        CombatFormationRules.MinUnitSeparation * 0.95f);
                }
            }
        }
    }
}
