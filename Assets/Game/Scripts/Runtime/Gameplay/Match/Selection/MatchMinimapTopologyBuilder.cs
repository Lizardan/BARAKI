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
                ? BuildN4(layout, graph)
                : BuildGeneric(layout, graph);
        }

        static MatchMinimapTopology BuildN4(MatchArenaLayout layout, LaneGraph graph)
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

        static MatchMinimapTopology BuildGeneric(MatchArenaLayout layout, LaneGraph graph)
        {
            var filledRects = new List<MatchMinimapRect>();
            var roadSegments = new List<MatchMinimapSegment>();
            var radius = layout.ArenaRadius;
            var centerRadius = graph.CenterArenaRadius;

            filledRects.Add(new MatchMinimapRect(
                Vector2.zero,
                new Vector2(centerRadius, centerRadius),
                0f));

            foreach (var slot in layout.Slots)
            {
                filledRects.Add(new MatchMinimapRect(
                    ToXZ(slot.BasePosition),
                    new Vector2(12f, 12f),
                    slot.BaseRotation.eulerAngles.y,
                    slot.SlotIndex));
            }

            roadSegments.Add(new MatchMinimapSegment(new Vector2(-radius, -radius), new Vector2(radius, -radius)));
            roadSegments.Add(new MatchMinimapSegment(new Vector2(radius, -radius), new Vector2(radius, radius)));
            roadSegments.Add(new MatchMinimapSegment(new Vector2(radius, radius), new Vector2(-radius, radius)));
            roadSegments.Add(new MatchMinimapSegment(new Vector2(-radius, radius), new Vector2(-radius, -radius)));

            return new MatchMinimapTopology(filledRects, roadSegments);
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
