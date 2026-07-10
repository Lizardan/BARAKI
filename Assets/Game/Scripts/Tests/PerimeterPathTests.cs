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
