using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchMinimapTopologyBuilder
    {
        public static MatchMinimapTopology Build(MatchArenaLayout layout, LaneGraph graph)
        {
            if (layout == null)
            {
                throw new System.ArgumentNullException(nameof(layout));
            }

            if (graph == null)
            {
                throw new System.ArgumentNullException(nameof(graph));
            }

            return layout.PlayerCount == 4
                ? BuildSquareCardinal(layout, graph)
                : layout.PlayerCount == 2
                    ? BuildDuelOval(layout, graph)
                    : BuildGeneric(layout, graph);
        }

        static MatchMinimapTopology BuildDuelOval(MatchArenaLayout layout, LaneGraph graph)
        {
            var filledRects = new List<MatchMinimapRect>();
            var roadSegments = new List<MatchMinimapSegment>();
            var halfSize = layout.ArenaRadius;
            var centerHalf = N2RoadReferenceSpec.CenterArenaHalfSize;

            filledRects.Add(new MatchMinimapRect(Vector2.zero, new Vector2(centerHalf, centerHalf), 0f));

            foreach (var slot in layout.Slots)
            {
                var localOffset = new Vector3(
                    0f,
                    0f,
                    -MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
                var worldCenter = slot.BasePosition + slot.BaseRotation * localOffset;
                filledRects.Add(new MatchMinimapRect(
                    ToXZ(worldCenter),
                    new Vector2(
                        MatchArenaGreyboxBuilder.BaseArenaWidth * 0.5f,
                        MatchArenaGreyboxBuilder.BaseArenaDepth * 0.5f),
                    slot.BaseRotation.eulerAngles.y,
                    slot.SlotIndex));
            }

            AddSegment(
                roadSegments,
                MatchArenaGenerator.RotateAuthoredToLayout(new Vector3(-halfSize, 0f, 0f)),
                MatchArenaGenerator.RotateAuthoredToLayout(new Vector3(halfSize, 0f, 0f)));
            AppendPolyline(roadSegments, DuelPathBuilder.SampleStadiumHalf(northSide: true, halfSize));
            AppendPolyline(roadSegments, DuelPathBuilder.SampleStadiumHalf(northSide: false, halfSize));

            return new MatchMinimapTopology(filledRects, roadSegments);
        }

        static MatchMinimapTopology BuildSquareCardinal(MatchArenaLayout layout, LaneGraph graph)
        {
            var filledRects = new List<MatchMinimapRect>();
            var roadSegments = new List<MatchMinimapSegment>();

            var centerHalf = N4RoadReferenceSpec.CenterArenaHalfSize;
            filledRects.Add(new MatchMinimapRect(Vector2.zero, new Vector2(centerHalf, centerHalf), 0f));

            foreach (var slot in layout.Slots)
            {
                var localOffset = new Vector3(
                    0f,
                    0f,
                    -MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
                var worldCenter = slot.BasePosition + slot.BaseRotation * localOffset;
                var rotationY = slot.BaseRotation.eulerAngles.y;
                filledRects.Add(new MatchMinimapRect(
                    ToXZ(worldCenter),
                    new Vector2(
                        MatchArenaGreyboxBuilder.BaseArenaWidth * 0.5f,
                        MatchArenaGreyboxBuilder.BaseArenaDepth * 0.5f),
                    rotationY,
                    slot.SlotIndex));
            }

            AppendN4PerimeterStrips(roadSegments, layout.ArenaRadius);
            AppendN4CornerArcs(roadSegments, layout.ArenaRadius);
            AppendN4SpokeConnectors(roadSegments);
            AppendSharedFlankRing(roadSegments, layout.ArenaRadius);

            return new MatchMinimapTopology(filledRects, roadSegments);
        }

        static MatchMinimapTopology BuildN4(MatchArenaLayout layout, LaneGraph graph) =>
            BuildSquareCardinal(layout, graph);

        static MatchMinimapTopology BuildGeneric(MatchArenaLayout layout, LaneGraph graph)
        {
            var filledRects = new List<MatchMinimapRect>();
            var roadSegments = new List<MatchMinimapSegment>();
            var centerHalf = N4RoadReferenceSpec.CenterArenaHalfSize;

            filledRects.Add(new MatchMinimapRect(Vector2.zero, new Vector2(centerHalf, centerHalf), 0f));

            foreach (var slot in layout.Slots)
            {
                var localOffset = new Vector3(0f, 0f, -MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
                var worldCenter = slot.BasePosition + slot.BaseRotation * localOffset;
                filledRects.Add(new MatchMinimapRect(
                    ToXZ(worldCenter),
                    new Vector2(
                        MatchArenaGreyboxBuilder.BaseArenaWidth * 0.5f,
                        MatchArenaGreyboxBuilder.BaseArenaDepth * 0.5f),
                    slot.BaseRotation.eulerAngles.y,
                    slot.SlotIndex));
            }

            if (layout.PlayerCount == 3)
            {
                AppendN3Perimeter(roadSegments, layout);
            }
            else
            {
                var ring = PerimeterRingPathBuilder.BuildSharedFlankRing(layout.ArenaRadius, layout.PlayerCount);
                AppendPolyline(roadSegments, PathWaypoints(ring));
                foreach (var slot in layout.Slots)
                {
                    CircularRingRoadGeometry.GetBaseFrame(slot.BasePosition, out var junction, out var radial, out var tangent);
                    AddSegment(roadSegments, junction, radial * centerHalf);
                    var exit = CircularRingRoadGeometry.ExitStraightLength;
                    AddSegment(roadSegments, junction, junction + tangent * exit);
                    AddSegment(roadSegments, junction, junction - tangent * exit);
                }
            }

            return new MatchMinimapTopology(filledRects, roadSegments);
        }

        static void AppendN3Perimeter(List<MatchMinimapSegment> segments, MatchArenaLayout layout)
        {
            var radius = layout.ArenaRadius;
            var centerHalf = N4RoadReferenceSpec.CenterArenaHalfSize;
            var curve = new List<Vector3>(CircularRingRoadGeometry.ExitCurveSamples + 1);
            var n = layout.PlayerCount;

            for (var i = 0; i < n; i++)
            {
                var slot = layout.Slots[i];
                var next = layout.Slots[(i + 1) % n];

                curve.Clear();
                CircularRingRoadGeometry.SampleExitCurve(
                    slot, Game.Core.GameIds.Buildings.BarracksRight, layout, curve);
                AppendPolyline(segments, curve);

                var from = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                    slot, Game.Core.GameIds.Buildings.BarracksRight, radius);
                var to = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                    next, Game.Core.GameIds.Buildings.BarracksLeft, radius);
                AddSegment(segments, from, to);

                curve.Clear();
                CircularRingRoadGeometry.SampleExitCurve(
                    next, Game.Core.GameIds.Buildings.BarracksLeft, layout, curve);
                AppendPolyline(segments, curve);
            }

            foreach (var slot in layout.Slots)
            {
                CircularRingRoadGeometry.GetBaseFrame(slot.BasePosition, out var junction, out var radial, out _);
                AddSegment(segments, junction, radial * centerHalf);

                var leftTip = CircularRingRoadGeometry.GetSideExitEnd(slot, Game.Core.GameIds.Buildings.BarracksLeft);
                var rightTip = CircularRingRoadGeometry.GetSideExitEnd(slot, Game.Core.GameIds.Buildings.BarracksRight);
                var leftBarracks = slot.GetBuildingWorldPosition(Game.Core.GameIds.Buildings.BarracksLeft);
                var rightBarracks = slot.GetBuildingWorldPosition(Game.Core.GameIds.Buildings.BarracksRight);
                AddSegment(segments, leftBarracks, leftTip);
                AddSegment(segments, rightBarracks, rightTip);
            }
        }

        static List<Vector3> PathWaypoints(LanePath path)
        {
            var points = new List<Vector3>(path.WaypointCount);
            for (var i = 0; i < path.WaypointCount; i++)
            {
                points.Add(path.GetWaypoint(i));
            }

            return points;
        }

        static void AppendN4PerimeterStrips(List<MatchMinimapSegment> segments, float halfSize)
        {
            var innerNeg = N4RoadReferenceSpec.PerimeterHalfStripInnerBound;
            var innerPos = N4RoadReferenceSpec.PerimeterHalfStripOuterBound;
            var cornerOuter = N4RoadReferenceSpec.GetPerimeterStripCornerOuter(halfSize);

            AddSegment(segments, new Vector3(-cornerOuter, 0f, halfSize), new Vector3(innerNeg, 0f, halfSize));
            AddSegment(segments, new Vector3(innerPos, 0f, halfSize), new Vector3(cornerOuter, 0f, halfSize));
            AddSegment(segments, new Vector3(-cornerOuter, 0f, -halfSize), new Vector3(innerNeg, 0f, -halfSize));
            AddSegment(segments, new Vector3(innerPos, 0f, -halfSize), new Vector3(cornerOuter, 0f, -halfSize));
            AddSegment(segments, new Vector3(halfSize, 0f, -cornerOuter), new Vector3(halfSize, 0f, innerNeg));
            AddSegment(segments, new Vector3(halfSize, 0f, innerPos), new Vector3(halfSize, 0f, cornerOuter));
            AddSegment(segments, new Vector3(-halfSize, 0f, -cornerOuter), new Vector3(-halfSize, 0f, innerNeg));
            AddSegment(segments, new Vector3(-halfSize, 0f, innerPos), new Vector3(-halfSize, 0f, cornerOuter));
        }

        static void AppendN4CornerArcs(List<MatchMinimapSegment> segments, float halfSize)
        {
            for (var i = 0; i < 4; i++)
            {
                var corner = N4RoadReferenceSpec.GetMapCornerArcCorner(i, halfSize);
                var samples = PerimeterCornerArc.GetCanonicalArcSamples(
                    corner,
                    PerimeterCornerArc.PathArcSegments);
                AppendPolyline(segments, samples);
            }
        }

        static void AppendN4SpokeConnectors(List<MatchMinimapSegment> segments)
        {
            var center = N4RoadReferenceSpec.SpokeConnectorCenter;
            var halfLength = N4RoadReferenceSpec.SpokeConnectorHalfLength;

            AddSegment(
                segments,
                new Vector3(0f, 0f, center - halfLength),
                new Vector3(0f, 0f, center + halfLength));
            AddSegment(
                segments,
                new Vector3(0f, 0f, -center - halfLength),
                new Vector3(0f, 0f, -center + halfLength));
            AddSegment(
                segments,
                new Vector3(center - halfLength, 0f, 0f),
                new Vector3(center + halfLength, 0f, 0f));
            AddSegment(
                segments,
                new Vector3(-center - halfLength, 0f, 0f),
                new Vector3(-center + halfLength, 0f, 0f));
        }

        static void AppendSharedFlankRing(List<MatchMinimapSegment> segments, float halfSize)
        {
            var path = N4RoadCenterlineBuilder.BuildSharedFlankRing(halfSize);
            var points = new List<Vector3>(path.WaypointCount);
            for (var i = 0; i < path.WaypointCount; i++)
            {
                points.Add(path.GetWaypoint(i));
            }

            AppendPolyline(segments, points);
        }

        static void AppendPolyline(List<MatchMinimapSegment> segments, IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            for (var i = 1; i < points.Count; i++)
            {
                AddSegment(segments, points[i - 1], points[i]);
            }
        }

        static void AddSegment(List<MatchMinimapSegment> segments, Vector3 a, Vector3 b)
        {
            segments.Add(new MatchMinimapSegment(ToXZ(a), ToXZ(b)));
        }

        static Vector2 ToXZ(Vector3 world) => new(world.x, world.z);
    }
}
