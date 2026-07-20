using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class LaneGraphTests
    {
        const float Epsilon = 0.5f;

        [Test]
        public void Build_N4_HasTwelveLanes()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);

            Assert.AreEqual(12, graph.Lanes.Count);
            Assert.AreEqual(GameIds.Topology.Ring, graph.TopologyId);
        }

        [Test]
        public void Build_N2_HasSixLanes_DuelTopology()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);

            Assert.AreEqual(6, graph.Lanes.Count);
            Assert.AreEqual(GameIds.Topology.Duel, graph.TopologyId);
        }

        [Test]
        public void TryGetLane_ReturnsLeftCenterRightPerOwner()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);

            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var left));
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Center, out var center));
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Right, out var right));

            Assert.AreEqual(GameIds.Lanes.Left, left.LaneId);
            Assert.AreEqual(GameIds.Lanes.Center, center.LaneId);
            Assert.AreEqual(GameIds.Lanes.Right, right.LaneId);
            Assert.IsTrue(center.IsCenterLane);
        }

        [Test]
        public void FlankPath_AllPlayers_SideBarracksExitAwayFromBase()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: MatchArenaGenerator.DefaultArenaRadius);
            var graph = LaneGraphBuilder.Build(layout);

            for (var slot = 0; slot < 4; slot++)
            {
                foreach (var laneId in new[] { GameIds.Lanes.Left, GameIds.Lanes.Right })
                {
                    graph.TryGetLane(slot, laneId, out var lane);
                    var playerSlot = layout.Slots[slot];
                    var main = playerSlot.GetBuildingWorldPosition(GameIds.Buildings.Main);
                    var barracksId = laneId == GameIds.Lanes.Left
                        ? GameIds.Buildings.BarracksLeft
                        : GameIds.Buildings.BarracksRight;
                    var barracks = playerSlot.GetBuildingWorldPosition(barracksId);

                    main.y = 0f;
                    barracks.y = 0f;
                    var outward = barracks - main;
                    Assert.Greater(outward.sqrMagnitude, 0.01f);

                    var start = lane.Path.Start;
                    start.y = 0f;
                    var next = lane.Path.GetWaypoint(1);
                    var firstSegment = next - start;
                    firstSegment.y = 0f;

                    Assert.Greater(firstSegment.magnitude, 0.1f);
                    Assert.Greater(
                        Vector3.Dot(firstSegment.normalized, outward.normalized),
                        0.45f,
                        $"Slot {slot} {laneId} should leave the barracks away from base, not inward.");
                }
            }
        }

        [Test]
        public void FlankPath_AllPlayers_SideBarracks_DoNotRouteThroughBaseInterior()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: MatchArenaGenerator.DefaultArenaRadius);
            var graph = LaneGraphBuilder.Build(layout);

            for (var slot = 0; slot < 4; slot++)
            {
                var playerSlot = layout.Slots[slot];
                var geometry = BaseRoadCenterlineBuilder.GetBaseRoadGeometry(playerSlot);
                var centerStripWorld = playerSlot.BasePosition
                    + playerSlot.BaseRotation * new Vector3(0f, 0f, geometry.CenterStripStart);
                centerStripWorld.y = 0f;

                foreach (var laneId in new[] { GameIds.Lanes.Left, GameIds.Lanes.Right })
                {
                    graph.TryGetLane(slot, laneId, out var lane);
                    for (var i = 1; i < lane.Path.WaypointCount - 1; i++)
                    {
                        var point = lane.Path.GetWaypoint(i);
                        point.y = 0f;
                        Assert.Greater(
                            Vector3.Distance(point, centerStripWorld),
                            1.5f,
                            $"Slot {slot} {laneId} flank path must not cut through base interior at {point}.");
                    }
                }
            }
        }

        [Test]
        public void FlankPath_NorthSouthSideBarracks_SpawnClearanceLiesOnRing()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: MatchArenaGenerator.DefaultArenaRadius);
            var graph = LaneGraphBuilder.Build(layout);
            var clearance = CombatFormationRules.BarracksSpawnForwardClearance;

            foreach (var slot in new[] { 1, 3 })
            {
                foreach (var laneId in new[] { GameIds.Lanes.Left, GameIds.Lanes.Right })
                {
                    graph.TryGetLane(slot, laneId, out var lane);
                    Assert.IsTrue(lane.Path.IsClosedLoop);
                    var route = LaneRoute.FromPath(lane.Path);
                    var spawnPosition = route.ResolveSpawnPosition(clearance, Vector3.zero);
                    var barracksId = laneId == GameIds.Lanes.Left
                        ? GameIds.Buildings.BarracksLeft
                        : GameIds.Buildings.BarracksRight;
                    var barracks = layout.Slots[slot].GetBuildingWorldPosition(barracksId);

                    spawnPosition.y = 0f;
                    barracks.y = 0f;
                    Assert.Greater(
                        Vector3.Distance(spawnPosition, barracks),
                        CombatFormationRules.BarracksSpawnForwardClearance * 0.5f,
                        $"Slot {slot} {laneId} spawn should start ahead of barracks.");
                    Assert.Less(
                        DistanceFromPath(lane.Path, spawnPosition),
                        0.5f,
                        $"Slot {slot} {laneId} spawn should lie on the flank ring.");
                }
            }
        }

        [Test]
        public void LanePath_StartsAtOwnerBarracks()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);

            for (var slot = 0; slot < layout.PlayerCount; slot++)
            {
                var playerSlot = layout.Slots[slot];
                Assert.IsTrue(graph.TryGetLane(slot, GameIds.Lanes.Center, out var center));
                Assert.Less(
                    Vector3.Distance(
                        center.Path.Start,
                        playerSlot.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter)),
                    Epsilon);

                Assert.IsTrue(graph.TryGetLane(slot, GameIds.Lanes.Left, out var left));
                Assert.Less(
                    Vector3.Distance(
                        left.Path.Start,
                        playerSlot.GetBuildingWorldPosition(GameIds.Buildings.BarracksLeft)),
                    Epsilon,
                    $"Slot {slot} left flank must start at barracks.");

                Assert.IsTrue(graph.TryGetLane(slot, GameIds.Lanes.Right, out var right));
                Assert.Less(
                    Vector3.Distance(
                        right.Path.Start,
                        playerSlot.GetBuildingWorldPosition(GameIds.Buildings.BarracksRight)),
                    Epsilon,
                    $"Slot {slot} right flank must start at barracks.");
            }
        }

        [Test]
        public void SpawnClearance_IsSameWorldDistanceFromBarracks_OnCenterAndFlanks()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var clearance = CombatFormationRules.BarracksSpawnForwardClearance;

            for (var slot = 0; slot < layout.PlayerCount; slot++)
            {
                AssertLaneSpawnDistance(layout, graph, slot, GameIds.Lanes.Center, GameIds.Buildings.BarracksCenter, clearance);
                AssertLaneSpawnDistance(layout, graph, slot, GameIds.Lanes.Left, GameIds.Buildings.BarracksLeft, clearance);
                AssertLaneSpawnDistance(layout, graph, slot, GameIds.Lanes.Right, GameIds.Buildings.BarracksRight, clearance);
            }
        }

        static void AssertLaneSpawnDistance(
            MatchArenaLayout layout,
            LaneGraph graph,
            int slot,
            string laneId,
            string barracksId,
            float clearance)
        {
            Assert.IsTrue(graph.TryGetLane(slot, laneId, out var lane));
            var barracks = layout.Slots[slot].GetBuildingWorldPosition(barracksId);
            barracks.y = 0f;
            var spawn = lane.Path.EvaluateDistance(clearance);
            spawn.y = 0f;
            Assert.AreEqual(
                clearance,
                Vector3.Distance(spawn, barracks),
                0.35f,
                $"Slot {slot} {laneId} spawn should be ~{clearance} from barracks (path start offset bug).");
        }

        [Test]
        public void LanePath_FlanksAreClosedRingsPassingOpponentBarracks()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(0, GameIds.Lanes.Left, out var leftLane);
            graph.TryGetLane(0, GameIds.Lanes.Right, out var rightLane);

            var leftOpponent = layout.Slots[leftLane.OpponentSlot];
            var rightOpponent = layout.Slots[rightLane.OpponentSlot];
            var leftDestination = leftOpponent.GetBuildingWorldPosition(GameIds.Buildings.BarracksRight);
            var rightDestination = rightOpponent.GetBuildingWorldPosition(GameIds.Buildings.BarracksLeft);

            Assert.IsTrue(leftLane.Path.IsClosedLoop);
            Assert.IsTrue(rightLane.Path.IsClosedLoop);
            Assert.Less(Vector3.Distance(leftLane.Path.Start, leftLane.Path.End), 0.25f);
            Assert.Less(Vector3.Distance(rightLane.Path.Start, rightLane.Path.End), 0.25f);

            var leftJoin = N4RoadCenterlineBuilder.GetStripJoinPoint(
                leftDestination,
                leftOpponent.BasePosition,
                layout.ArenaRadius);
            var rightJoin = N4RoadCenterlineBuilder.GetStripJoinPoint(
                rightDestination,
                rightOpponent.BasePosition,
                layout.ArenaRadius);
            Assert.Less(DistanceFromPath(leftLane.Path, leftJoin), 2f);
            Assert.Less(DistanceFromPath(rightLane.Path, rightJoin), 2f);
        }

        [Test]
        public void FlankPath_FollowsSharedRing_LeftClockwise_RightCounterClockwise()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: MatchArenaGenerator.DefaultArenaRadius);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Left, out var leftLane);
            graph.TryGetLane(1, GameIds.Lanes.Right, out var rightLane);

            var leftCorner = FindFirstCornerWaypoint(leftLane.Path, positiveX: true);
            var rightCorner = FindFirstCornerWaypoint(rightLane.Path, positiveX: false);

            Assert.Greater(leftCorner.x, 0f, "North left lane should turn toward East (CW) along square perimeter.");
            Assert.Less(rightCorner.x, 0f, "North right lane should turn toward West (CCW) along square perimeter.");
        }

        [Test]
        public void FlankPath_DoesNotPassNearMain()
        {
            const float mainClearance = 2f;
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: MatchArenaGenerator.DefaultArenaRadius);
            var graph = LaneGraphBuilder.Build(layout);

            for (var slot = 0; slot < layout.PlayerCount; slot++)
            {
                var main = layout.Slots[slot].GetBuildingWorldPosition(GameIds.Buildings.Main);

                foreach (var laneId in new[] { GameIds.Lanes.Left, GameIds.Lanes.Right })
                {
                    graph.TryGetLane(slot, laneId, out var lane);

                    for (var i = 0; i < lane.Path.WaypointCount; i++)
                    {
                        var point = lane.Path.GetWaypoint(i);
                        point.y = 0f;
                        main.y = 0f;
                        if (Vector3.Distance(point, main) < 0.25f)
                        {
                            continue;
                        }

                        Assert.GreaterOrEqual(
                            Vector3.Distance(point, main),
                            mainClearance,
                            $"Slot {slot} {laneId} waypoint {i} at {point} passes through Main at {main}");
                    }
                }
            }
        }

        [Test]
        public void CenterLane_PassesThroughCentralArena()
        {
            const float arenaRadius = 20f;
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: MatchArenaGenerator.DefaultArenaRadius);
            var graph = LaneGraphBuilder.Build(layout, arenaRadius);
            graph.TryGetLane(0, GameIds.Lanes.Center, out var lane);

            Assert.GreaterOrEqual(lane.Path.WaypointCount, 5);

            var minDistToCenter = float.MaxValue;
            var insideArenaSamples = 0;
            for (var i = 0; i < lane.Path.WaypointCount; i++)
            {
                var p = lane.Path.GetWaypoint(i);
                p.y = 0f;
                minDistToCenter = Mathf.Min(minDistToCenter, p.magnitude);
            }

            for (var i = 0; i <= 80; i++)
            {
                var t = i / 80f;
                var p = lane.Path.EvaluateNormalized(t);
                p.y = 0f;
                var dist = p.magnitude;
                minDistToCenter = Mathf.Min(minDistToCenter, dist);
                if (dist <= arenaRadius + Epsilon)
                {
                    insideArenaSamples++;
                }
            }

            Assert.Less(minDistToCenter, Epsilon);
            Assert.Greater(insideArenaSamples, 5, "Center lane should spend meaningful distance inside the arena.");
        }

        [Test]
        public void CenterLane_EntryExitLieOnArenaBoundary()
        {
            const float arenaRadius = 20f;
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: MatchArenaGenerator.DefaultArenaRadius);
            var graph = LaneGraphBuilder.Build(layout, arenaRadius);
            graph.TryGetLane(0, GameIds.Lanes.Center, out var lane);

            var entry = lane.Path.GetWaypoint(1);
            var exit = lane.Path.GetWaypoint(3);
            entry.y = 0f;
            exit.y = 0f;

            Assert.AreEqual(arenaRadius, entry.magnitude, Epsilon);
            Assert.AreEqual(arenaRadius, exit.magnitude, Epsilon);
        }

        [Test]
        public void LanePath_MarchIncreasesDistanceFromStart()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(0, GameIds.Lanes.Right, out var lane);

            var start = lane.Path.EvaluateNormalized(0f);
            var mid = lane.Path.EvaluateNormalized(0.5f);
            var end = lane.Path.EvaluateNormalized(1f);

            Assert.Greater(Vector3.Distance(start, mid), 1f);
            Assert.Greater(Vector3.Distance(mid, end), 1f);
            Assert.Greater(lane.Path.TotalLength, 10f);
        }

        [Test]
        public void Build_N8_AllSlotsHaveThreeLanes()
        {
            var layout = MatchArenaGenerator.Generate(8);
            var graph = LaneGraphBuilder.Build(layout);

            for (var i = 0; i < 8; i++)
            {
                Assert.IsTrue(graph.TryGetLane(i, GameIds.Lanes.Left, out _));
                Assert.IsTrue(graph.TryGetLane(i, GameIds.Lanes.Center, out _));
                Assert.IsTrue(graph.TryGetLane(i, GameIds.Lanes.Right, out _));
            }
        }

        static Vector3 FindFirstCornerWaypoint(LanePath path, bool positiveX)
        {
            for (var i = 2; i < path.WaypointCount; i++)
            {
                var point = path.GetWaypoint(i);
                if (positiveX && point.x > 0f)
                {
                    return point;
                }

                if (!positiveX && point.x < 0f)
                {
                    return point;
                }
            }

            return path.GetWaypoint(2);
        }

        static float DistanceFromPath(LanePath path, Vector3 worldPosition)
        {
            var projected = path.EvaluateDistance(path.ProjectDistance(worldPosition));
            worldPosition.y = 0f;
            projected.y = 0f;
            return Vector3.Distance(worldPosition, projected);
        }
    }
}
