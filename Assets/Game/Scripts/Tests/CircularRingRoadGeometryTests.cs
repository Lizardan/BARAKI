using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CircularRingRoadGeometryTests
    {
        [Test]
        public void N3_Bases_AreEvenlyOnCircle()
        {
            var layout = MatchArenaGenerator.Generate(3);
            Assert.AreEqual(3, layout.Slots.Count);
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(layout.ArenaRadius, layout.Slots[i].BasePosition.magnitude, 0.05f);
            }
        }

        [Test]
        public void BuildN3_Footprints_CoverStraightBetweenExitJoins()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var footprints = RoadFootprintFactory.BuildN3(layout);
            var r = layout.ArenaRadius;
            var a = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                layout.Slots[0], GameIds.Buildings.BarracksRight, r);
            var b = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                layout.Slots[1], GameIds.Buildings.BarracksLeft, r);
            var mid = Vector3.Lerp(a, b, 0.5f);
            Assert.IsTrue(
                RoadFootprintShapes.PointInAnyPolygon(new Vector2(mid.x, mid.z), footprints),
                "Perimeter between exit joins must be a straight road.");

            // Chord sits inside the circle — the old circular mid-arc should no longer be required.
            var onCircle = new Vector2(Mathf.Cos(Mathf.PI / 3f) * r, Mathf.Sin(Mathf.PI / 3f) * r);
            var chordDist = Vector2.Distance(onCircle, new Vector2(mid.x, mid.z));
            Assert.Greater(chordDist, MatchArenaGreyboxBuilder.RoadWidth * 0.25f);
        }

        [Test]
        public void BuildN3_Footprints_CoverSpokeFilletNearJunction()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var footprints = RoadFootprintFactory.BuildN3(layout);
            var filletRadius = CircularRingRoadGeometry.JunctionFilletRadius;

            foreach (var slot in layout.Slots)
            {
                CircularRingRoadGeometry.GetSpokeJunction(
                    slot.BasePosition,
                    out var junction,
                    out var spokeDir,
                    out var leftDir,
                    out _);
                var sample = junction - spokeDir * (filletRadius * 0.5f) + leftDir * (filletRadius * 0.5f);
                Assert.IsTrue(
                    RoadFootprintShapes.PointInAnyPolygon(new Vector2(sample.x, sample.z), footprints),
                    $"Missing spoke fillet near junction for slot {slot.SlotIndex} at {sample}");
            }
        }

        [Test]
        public void BuildN3_Footprints_ExitCurve_HasNoInnerEdgeGaps()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var footprints = RoadFootprintFactory.BuildN3(layout);
            var radius = layout.ArenaRadius;
            var slot = layout.Slots[0];
            const string left = GameIds.Buildings.BarracksLeft;
            var halfWidth = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;

            var curve = new System.Collections.Generic.List<Vector3>(
                CircularRingRoadGeometry.ExitCurveSamples + 1);
            CircularRingRoadGeometry.SampleExitCurve(slot, left, layout, curve);

            // Sample inward of the centerline (toward curve inside) — strip seams used to leave gaps here.
            for (var i = 1; i < curve.Count - 1; i++)
            {
                var prev = curve[i - 1];
                var next = curve[i + 1];
                var tangent = next - prev;
                tangent.y = 0f;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                tangent.Normalize();
                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                // Both sides near the edge must stay on the road surface.
                var inward = curve[i] - right * (halfWidth * 0.85f);
                var outward = curve[i] + right * (halfWidth * 0.85f);
                Assert.IsTrue(
                    RoadFootprintShapes.PointInAnyPolygon(new Vector2(inward.x, inward.z), footprints),
                    $"Inner edge gap on exit curve at sample {i}: {inward}");
                Assert.IsTrue(
                    RoadFootprintShapes.PointInAnyPolygon(new Vector2(outward.x, outward.z), footprints),
                    $"Outer edge gap on exit curve at sample {i}: {outward}");
            }
        }

        [Test]
        public void BuildN3_Footprints_StraightThenCurveThenStraightEdge()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var footprints = RoadFootprintFactory.BuildN3(layout);
            var radius = layout.ArenaRadius;
            var slot = layout.Slots[0];
            const string right = GameIds.Buildings.BarracksRight;

            var exitEnd = CircularRingRoadGeometry.GetSideExitEnd(slot, right);
            Assert.IsTrue(
                RoadFootprintShapes.PointInAnyPolygon(new Vector2(exitEnd.x, exitEnd.z), footprints),
                "Straight exit tip must remain.");

            var join = CircularRingRoadGeometry.GetExitCurveJoinPoint(slot, right, radius);
            Assert.IsTrue(
                RoadFootprintShapes.PointInAnyPolygon(new Vector2(join.x, join.z), footprints),
                "Curve join onto the perimeter must be covered.");

            var curve = new System.Collections.Generic.List<Vector3>(
                CircularRingRoadGeometry.ExitCurveSamples + 1);
            CircularRingRoadGeometry.SampleExitCurve(slot, right, layout, curve);
            var mid = curve[curve.Count / 2];
            Assert.IsTrue(
                RoadFootprintShapes.PointInAnyPolygon(new Vector2(mid.x, mid.z), footprints),
                "Rounding after the straight exit must be filled.");

            var nextJoin = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                layout.Slots[1], GameIds.Buildings.BarracksLeft, radius);
            var alongStraight = Vector3.Lerp(join, nextJoin, 0.35f);
            Assert.IsTrue(
                RoadFootprintShapes.PointInAnyPolygon(new Vector2(alongStraight.x, alongStraight.z), footprints),
                "After the exit curve the perimeter must continue as a straight to the next base.");
        }

        [Test]
        public void BuildN3_Footprints_CoverShortStraightSideExit()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var footprints = RoadFootprintFactory.BuildN3(layout);
            var slot = layout.Slots[0];
            var mid = CircularRingRoadGeometry.GetSideExitEnd(slot, GameIds.Buildings.BarracksLeft);
            var barracks = slot.GetBuildingWorldPosition(GameIds.Buildings.BarracksLeft);
            var sample = Vector3.Lerp(barracks, mid, 0.5f);
            Assert.IsTrue(
                RoadFootprintShapes.PointInAnyPolygon(new Vector2(sample.x, sample.z), footprints),
                "Short straight side exit must be on the road surface.");
        }

        [Test]
        public void N3_FlankPath_LeavesSideBarracksStraightAlongLocalAxis()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var graph = LaneGraphBuilder.Build(layout);
            var slot = layout.Slots[0];
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var left));

            var start = left.Path.GetWaypoint(0);
            var next = left.Path.GetWaypoint(1);
            var step = next - start;
            step.y = 0f;
            step.Normalize();

            var expected = CircularRingRoadGeometry.GetSideExitDir(slot, GameIds.Buildings.BarracksLeft);
            Assert.Greater(Vector3.Dot(step, expected), 0.85f, "First flank segment must be the short straight exit.");
        }

        [Test]
        public void SharedFlankRing_N3_IsClosedWithStraightEdges()
        {
            var path = PerimeterRingPathBuilder.BuildSharedFlankRing(
                MatchArenaGenerator.DefaultArenaRadius,
                playerCount: 3);
            Assert.GreaterOrEqual(path.WaypointCount, 12);
            Assert.IsTrue(path.IsClosedLoop);

            var layout = MatchArenaGenerator.Generate(3);
            var a = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                layout.Slots[0], GameIds.Buildings.BarracksRight, layout.ArenaRadius);
            var b = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                layout.Slots[1], GameIds.Buildings.BarracksLeft, layout.ArenaRadius);
            var foundStraight = false;
            for (var i = 0; i < path.WaypointCount - 1; i++)
            {
                var p0 = path.GetWaypoint(i);
                var p1 = path.GetWaypoint(i + 1);
                var p0Flat = new Vector3(p0.x, 0f, p0.z);
                var p1Flat = new Vector3(p1.x, 0f, p1.z);
                if ((Vector3.Distance(p0Flat, a) < 1f && Vector3.Distance(p1Flat, b) < 1f)
                    || (Vector3.Distance(p0Flat, b) < 1f && Vector3.Distance(p1Flat, a) < 1f))
                {
                    foundStraight = true;
                    break;
                }
            }

            Assert.IsTrue(foundStraight, "Shared flank ring must include the straight edge between exit joins.");
        }

        [Test]
        public void FlankPath_N3_LeftClockwiseToLeftOpponent_RightCounterClockwiseToRightOpponent()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var leftLane));
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Right, out var rightLane));

            var leftBarracks = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.BarracksLeft);
            var rightBarracks = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.BarracksRight);
            var firstFarLeft = FirstWaypointBeyond(leftLane.Path, leftBarracks, 35f);
            var firstFarRight = FirstWaypointBeyond(rightLane.Path, rightBarracks, 35f);

            var slot1 = layout.Slots[1].BasePosition;
            var slot2 = layout.Slots[2].BasePosition;

            Assert.Less(
                FlatDistance(firstFarLeft, slot2),
                FlatDistance(firstFarLeft, slot1),
                "Left lane should head clockwise toward the left opponent (slot i-1).");
            Assert.Less(
                FlatDistance(firstFarRight, slot1),
                FlatDistance(firstFarRight, slot2),
                "Right lane should head counter-clockwise toward the right opponent (slot i+1).");
        }

        static Vector3 FirstWaypointBeyond(LanePath path, Vector3 origin, float minDistance)
        {
            for (var i = 0; i < path.WaypointCount; i++)
            {
                var point = path.GetWaypoint(i);
                if (FlatDistance(point, origin) >= minDistance)
                {
                    return point;
                }
            }

            return path.GetWaypoint(path.WaypointCount - 1);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
