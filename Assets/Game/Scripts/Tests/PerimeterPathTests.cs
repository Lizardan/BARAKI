using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PerimeterPathTests
    {
        const float HalfSize = MatchArenaGenerator.DefaultArenaRadius;
        const float Epsilon = 1f;

        [Test]
        public void DuelPath_N2_FlanksUseStadiumStraightBetweenCorners()
        {
            var layout = MatchArenaGenerator.Generate(2, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var left));
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Right, out var right));

            var leftPeak = GetMaxAbsZ(left.Path);
            var rightPeak = GetMaxAbsZ(right.Path);
            Assert.AreEqual(N2RoadReferenceSpec.FlankStraightAbsZ, leftPeak, 0.5f);
            Assert.AreEqual(N2RoadReferenceSpec.FlankStraightAbsZ, rightPeak, 0.5f);
        }

        [Test]
        public void DuelPath_N2_SideBarracksExitGoesStraightFirst()
        {
            var layout = MatchArenaGenerator.Generate(2, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var left));

            var slot = layout.Slots[0];
            var barracks = Flat(slot.GetBuildingWorldPosition(GameIds.Buildings.BarracksLeft));
            var p0 = Flat(left.Path.GetWaypoint(0));
            var p1 = Flat(left.Path.GetWaypoint(1));

            Assert.Less(Vector3.Distance(p0, barracks), 0.25f, "Path must start on barracks center.");

            var outward = DuelPathBuilder.GetBarracksOutwardDir(slot, GameIds.Buildings.BarracksLeft);
            var firstStep = (p1 - p0).normalized;
            Assert.Greater(Vector3.Dot(firstStep, outward), 0.9f, "First segment must leave barracks straight outward.");
            Assert.Greater(
                Vector3.Distance(p0, p1),
                N2RoadReferenceSpec.SideExitStraightLength * 0.85f);
        }

        [Test]
        public void DuelPath_N2_ExitsAreMirroredLeftRight()
        {
            var layout = MatchArenaGenerator.Generate(2, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var left));
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Right, out var right));

            var slot = layout.Slots[0];
            var leftOut = DuelPathBuilder.GetBarracksOutwardDir(slot, GameIds.Buildings.BarracksLeft);
            var rightOut = DuelPathBuilder.GetBarracksOutwardDir(slot, GameIds.Buildings.BarracksRight);
            Assert.AreEqual(-1f, Vector3.Dot(leftOut, rightOut), 0.01f, "Left/right outward must be opposite.");

            var leftStep = (Flat(left.Path.GetWaypoint(1)) - Flat(left.Path.GetWaypoint(0))).normalized;
            var rightStep = (Flat(right.Path.GetWaypoint(1)) - Flat(right.Path.GetWaypoint(0))).normalized;
            Assert.Greater(Vector3.Dot(leftStep, leftOut), 0.9f);
            Assert.Greater(Vector3.Dot(rightStep, rightOut), 0.9f);
        }

        [Test]
        public void DuelPath_N2_NorthCenterlineLinksNorthernBarracksWithoutJumps()
        {
            var layout = MatchArenaGenerator.Generate(2, arenaRadius: HalfSize);
            var west = layout.Slots[0].BasePosition.x < 0f ? layout.Slots[0] : layout.Slots[1];
            var east = layout.Slots[0].BasePosition.x >= 0f ? layout.Slots[0] : layout.Slots[1];
            var north = DuelPathBuilder.BuildFlankCenterline(west, east, northSide: true, HalfSize, 24);

            var westNorth = Flat(west.GetBuildingWorldPosition(GameIds.Buildings.BarracksLeft));
            var eastNorth = Flat(east.GetBuildingWorldPosition(GameIds.Buildings.BarracksRight));
            Assert.Less(Vector3.Distance(Flat(north[0]), westNorth), 0.25f);
            Assert.Less(Vector3.Distance(Flat(north[^1]), eastNorth), 0.25f);
            Assert.Greater(westNorth.z, 0f);
            Assert.Greater(eastNorth.z, 0f);

            for (var i = 1; i < north.Count; i++)
            {
                Assert.Less(
                    Vector3.Distance(Flat(north[i - 1]), Flat(north[i])),
                    30f,
                    $"Large jump at index {i} — corner/straight join broken.");
            }
        }

        [Test]
        public void DuelPath_N2_WestLeftLaneUsesNorthFlank()
        {
            var layout = MatchArenaGenerator.Generate(2, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            var westIndex = layout.Slots[0].BasePosition.x < 0f ? 0 : 1;
            Assert.IsTrue(graph.TryGetLane(westIndex, GameIds.Lanes.Left, out var left));

            var mid = Flat(left.Path.GetWaypoint(left.Path.WaypointCount / 2));
            Assert.Greater(mid.z, N2RoadReferenceSpec.FlankStraightAbsZ * 0.9f, "West Left must march on the north stadium strip.");
        }

        [Test]
        public void DuelPath_N2_UsesN4CornerArcsOnFlankExits()
        {
            var layout = MatchArenaGenerator.Generate(2, arenaRadius: HalfSize);
            var west = layout.Slots[0].BasePosition.x < 0f ? layout.Slots[0] : layout.Slots[1];
            var east = layout.Slots[0].BasePosition.x >= 0f ? layout.Slots[0] : layout.Slots[1];
            var north = DuelPathBuilder.BuildFlankCenterline(west, east, northSide: true, HalfSize, 12);

            var nw = DuelPathBuilder.GetFlankCorner(northSide: true, eastSide: false, HalfSize);
            var ne = DuelPathBuilder.GetFlankCorner(northSide: true, eastSide: true, HalfSize);
            PerimeterCornerArc.GetClockwiseEndpoints(nw, out _, out var nwExit, N2RoadReferenceSpec.CornerRadius);
            PerimeterCornerArc.GetClockwiseEndpoints(ne, out var neEntry, out _, N2RoadReferenceSpec.CornerRadius);

            Assert.IsTrue(ContainsNear(north, nwExit), "North flank must pass west N4-style corner exit.");
            Assert.IsTrue(ContainsNear(north, neEntry), "North flank must pass east N4-style corner entry.");
        }

        static bool ContainsNear(System.Collections.Generic.List<Vector3> points, Vector3 target)
        {
            for (var i = 0; i < points.Count; i++)
            {
                if (Vector3.Distance(Flat(points[i]), Flat(target)) < 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static float GetMaxAbsZ(LanePath path)
        {
            var max = 0f;
            for (var i = 0; i < path.WaypointCount; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(path.GetWaypoint(i).z));
            }

            return max;
        }

        [Test]
        public void SquarePath_N4_RightLane_PassesNorthWestCornerArc()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Right, out var lane);

            Assert.IsTrue(HasWaypointNearCorner(lane.Path, new Vector3(-HalfSize, 0f, HalfSize)));
        }

        [Test]
        public void SquarePath_N4_LeftLane_PassesNorthEastCornerArc()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Left, out var lane);

            Assert.IsTrue(HasWaypointNearCorner(lane.Path, new Vector3(HalfSize, 0f, HalfSize)));
        }

        [Test]
        public void SquarePath_N4_LeftAndRight_UseSymmetricSpokeJoins()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Left, out var leftLane);
            graph.TryGetLane(1, GameIds.Lanes.Right, out var rightLane);
            var slot = layout.Slots[1];
            var leftBarracks = slot.GetBuildingWorldPosition(GameIds.Buildings.BarracksLeft);
            var rightBarracks = slot.GetBuildingWorldPosition(GameIds.Buildings.BarracksRight);
            var leftJoin = N4RoadCenterlineBuilder.GetStripJoinPoint(leftBarracks, slot.BasePosition, HalfSize);
            var rightJoin = N4RoadCenterlineBuilder.GetStripJoinPoint(rightBarracks, slot.BasePosition, HalfSize);

            Assert.IsTrue(ContainsWaypointNear(leftLane.Path, leftJoin));
            Assert.IsTrue(ContainsWaypointNear(rightLane.Path, rightJoin));
            Assert.AreEqual(HalfSize, leftJoin.z, Epsilon);
            Assert.AreEqual(HalfSize, rightJoin.z, Epsilon);
            Assert.AreEqual(-leftJoin.x, rightJoin.x, Epsilon);
        }

        static bool ContainsWaypointNear(LanePath path, Vector3 target)
        {
            for (var i = 0; i < path.WaypointCount; i++)
            {
                if (Vector3.Distance(path.GetWaypoint(i), target) < 0.25f)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void SquarePath_N4_RightLane_NoBacktrackAtCorners()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Right, out var lane);

            Assert.IsFalse(HasDirectionReversal(lane.Path),
                "Right (counter-clockwise) lane path contains a direction reversal — corner backtrack bug.");
        }

        [Test]
        public void SquarePath_N4_LeftLane_NoBacktrackAtCorners()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Left, out var lane);

            Assert.IsFalse(HasDirectionReversal(lane.Path),
                "Left (clockwise) lane path contains a direction reversal — corner backtrack bug.");
        }

        [Test]
        public void MatchMarchRules_UnitsOnSplineContinueAfterElimination()
        {
            Assert.IsTrue(MatchMarchRules.UnitsOnSplineContinueAfterOwnerEliminated);
        }

        /// <summary>
        /// Checks that no pair of consecutive segments reverses direction (dot &lt; -0.5),
        /// which would indicate a backtrack like the corner arc endpoint bug.
        /// </summary>
        static bool HasDirectionReversal(LanePath path)
        {
            const float minSegmentLength = 0.5f;
            for (var i = 0; i < path.WaypointCount - 2; i++)
            {
                var a = path.GetWaypoint(i);
                var b = path.GetWaypoint(i + 1);
                var c = path.GetWaypoint(i + 2);
                a.y = 0f;
                b.y = 0f;
                c.y = 0f;

                var dir1 = b - a;
                var dir2 = c - b;
                if (dir1.sqrMagnitude < minSegmentLength * minSegmentLength
                    || dir2.sqrMagnitude < minSegmentLength * minSegmentLength)
                {
                    continue;
                }

                dir1.Normalize();
                dir2.Normalize();
                if (Vector3.Dot(dir1, dir2) < -0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasWaypointNearCorner(LanePath path, Vector3 corner)
        {
            for (var i = 0; i < path.WaypointCount; i++)
            {
                if (Vector3.Distance(path.GetWaypoint(i), corner) < HalfSize * 0.35f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
