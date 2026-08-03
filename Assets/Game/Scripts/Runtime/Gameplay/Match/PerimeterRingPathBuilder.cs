using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Shared visual path for the perimeter flank ring (all player counts).</summary>
    public static class PerimeterRingPathBuilder
    {
        public const int CircularRingSegments = 48;

        public static LanePath BuildSharedFlankRing(float ringRadius, int playerCount)
        {
            if (playerCount == 2)
            {
                return DuelPathBuilder.BuildSharedFlankRing(ringRadius);
            }

            if (playerCount == 4)
            {
                return N4RoadCenterlineBuilder.BuildSharedFlankRing(ringRadius);
            }

            if (playerCount == 3)
            {
                return BuildStraightEdgeRing(ringRadius, playerCount);
            }

            return BuildCircularClockwiseRing(ringRadius, CircularRingSegments);
        }

        public static LanePath BuildClockwiseRing(float ringRadius) =>
            BuildSharedFlankRing(ringRadius, playerCount: 4);

        /// <summary>
        /// N=3: exit curves + straight edges between joins (not a circular arc).
        /// Order is CW around the map (matches the N=4 / N=5+ shared ring convention).
        /// </summary>
        static LanePath BuildStraightEdgeRing(float ringRadius, int playerCount)
        {
            var layout = MatchArenaGenerator.Generate(playerCount, ringRadius);
            var points = new List<Vector3>(playerCount * (CircularRingRoadGeometry.ExitCurveSamples * 2 + 4));
            var curve = new List<Vector3>(CircularRingRoadGeometry.ExitCurveSamples + 1);

            for (var i = 0; i < playerCount; i++)
            {
                var current = layout.Slots[i];
                var next = layout.Slots[(i + 1) % playerCount];

                curve.Clear();
                CircularRingRoadGeometry.SampleExitCurve(
                    current, Game.Core.GameIds.Buildings.BarracksRight, layout, curve);
                AppendPath(points, curve, skipDuplicateStart: true);

                var leftJoin = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                    next, Game.Core.GameIds.Buildings.BarracksLeft, ringRadius);
                AppendPoint(points, leftJoin);

                curve.Clear();
                CircularRingRoadGeometry.SampleExitCurve(
                    next, Game.Core.GameIds.Buildings.BarracksLeft, layout, curve);
                for (var c = curve.Count - 1; c >= 0; c--)
                {
                    AppendPoint(points, curve[c]);
                }

                var rightTip = CircularRingRoadGeometry.GetSideExitEnd(
                    next, Game.Core.GameIds.Buildings.BarracksRight);
                AppendPoint(points, rightTip);
            }

            if (points.Count > 0)
            {
                points.Reverse();
                AppendPoint(points, points[0]);
            }

            return new LanePath(points, isClosedLoop: true);
        }

        static void AppendPath(List<Vector3> points, List<Vector3> add, bool skipDuplicateStart)
        {
            var start = 0;
            if (skipDuplicateStart && points.Count > 0 && add.Count > 0
                && FlatDistance(points[^1], add[0]) <= 0.05f)
            {
                start = 1;
            }

            for (var i = start; i < add.Count; i++)
            {
                AppendPoint(points, add[i]);
            }
        }

        static void AppendPoint(List<Vector3> points, Vector3 point)
        {
            point = WithLaneHeight(point);
            if (points.Count > 0 && FlatDistance(points[^1], point) <= 0.05f)
            {
                return;
            }

            points.Add(point);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static Vector3 WithLaneHeight(Vector3 point) =>
            new(point.x, N4PerimeterLaneGeometry.LaneHeight, point.z);

        static LanePath BuildCircularClockwiseRing(float radius, int segments)
        {
            var points = new List<Vector3>(segments + 1);
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = -2f * Mathf.PI * t;
                points.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    N4PerimeterLaneGeometry.LaneHeight,
                    Mathf.Sin(angle) * radius));
            }

            return new LanePath(points, isClosedLoop: true);
        }
    }
}
