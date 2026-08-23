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

            AddBasePads(filledRects, layout);
            AppendClosedPolyline(
                roadSegments,
                PathWaypoints(DuelPathBuilder.BuildSharedFlankRing(halfSize)));
            AddSegment(
                roadSegments,
                MatchArenaGenerator.RotateAuthoredToLayout(new Vector3(-halfSize, 0f, 0f)),
                MatchArenaGenerator.RotateAuthoredToLayout(new Vector3(halfSize, 0f, 0f)));

            return new MatchMinimapTopology(filledRects, roadSegments);
        }

        static MatchMinimapTopology BuildSquareCardinal(MatchArenaLayout layout, LaneGraph graph)
        {
            var filledRects = new List<MatchMinimapRect>();
            var roadSegments = new List<MatchMinimapSegment>();

            AddBasePads(filledRects, layout);
            AppendN4PerimeterStrips(roadSegments, layout.ArenaRadius);
            AppendN4CornerArcs(roadSegments, layout.ArenaRadius);
            AppendCenterSpokes(roadSegments, layout);
            AppendSharedFlankRing(roadSegments, layout.ArenaRadius);

            return new MatchMinimapTopology(filledRects, roadSegments);
        }

        static MatchMinimapTopology BuildN4(MatchArenaLayout layout, LaneGraph graph) =>
            BuildSquareCardinal(layout, graph);

        static MatchMinimapTopology BuildGeneric(MatchArenaLayout layout, LaneGraph graph)
        {
            var filledRects = new List<MatchMinimapRect>();
            var roadSegments = new List<MatchMinimapSegment>();

            AddBasePads(filledRects, layout);
            var ring = PerimeterRingPathBuilder.BuildSharedFlankRing(layout.ArenaRadius, layout.PlayerCount);
            AppendClosedPolyline(roadSegments, PathWaypoints(ring));
            AppendCenterSpokes(roadSegments, layout);

            if (layout.PlayerCount >= 5)
            {
                var exit = CircularRingRoadGeometry.ExitStraightLength;
                foreach (var slot in layout.Slots)
                {
                    CircularRingRoadGeometry.GetBaseFrame(slot.BasePosition, out var junction, out _, out var tangent);
                    AddSegment(roadSegments, junction, junction + tangent * exit);
                    AddSegment(roadSegments, junction, junction - tangent * exit);
                }
            }

            return new MatchMinimapTopology(filledRects, roadSegments);
        }

        static void AddBasePads(List<MatchMinimapRect> filledRects, MatchArenaLayout layout)
        {
            foreach (var slot in layout.Slots)
            {
                var yaw = MatchMinimapProjection.YawDegrees(slot.BaseRotation);
                filledRects.Add(new MatchMinimapRect(
                    ToXZ(slot.BasePosition),
                    new Vector2(
                        MatchArenaGreyboxBuilder.BaseArenaWidth * 0.5f,
                        MatchArenaGreyboxBuilder.BaseArenaDepth * 0.5f),
                    yaw,
                    slot.SlotIndex));
            }
        }

        static void AppendCenterSpokes(List<MatchMinimapSegment> segments, MatchArenaLayout layout)
        {
            foreach (var slot in layout.Slots)
            {
                AddSegment(segments, slot.BasePosition, Vector3.zero);
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

        static void AppendSharedFlankRing(List<MatchMinimapSegment> segments, float halfSize)
        {
            var path = N4RoadCenterlineBuilder.BuildSharedFlankRing(halfSize);
            var points = new List<Vector3>(path.WaypointCount);
            for (var i = 0; i < path.WaypointCount; i++)
            {
                points.Add(path.GetWaypoint(i));
            }

            AppendClosedPolyline(segments, points);
        }

        static void AppendClosedPolyline(List<MatchMinimapSegment> segments, IReadOnlyList<Vector3> points)
        {
            AppendPolyline(segments, points);
            if (points == null || points.Count < 2)
            {
                return;
            }

            var first = points[0];
            var last = points[points.Count - 1];
            first.y = 0f;
            last.y = 0f;
            if ((first - last).sqrMagnitude > 0.01f)
            {
                AddSegment(segments, points[points.Count - 1], points[0]);
            }
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
