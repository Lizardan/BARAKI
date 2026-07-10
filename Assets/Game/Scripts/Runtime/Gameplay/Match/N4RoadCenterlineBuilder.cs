using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Shared N=4 road centerlines for mesh placement, lane paths, and lane markers.</summary>
    public static class N4RoadCenterlineBuilder
    {
        public static float LaneHeight => N4PerimeterLaneGeometry.LaneHeight;

        public static float NegativeHalfMin => N4RoadReferenceSpec.NegativeHalfMin;

        public static float NegativeHalfMax => N4RoadReferenceSpec.NegativeHalfMax;

        public static float PositiveHalfMin => N4RoadReferenceSpec.PositiveHalfMin;

        public static float PositiveHalfMax => N4RoadReferenceSpec.PositiveHalfMax;

        public static float GetTravelCoord(Vector3 point, int edge) =>
            edge is 0 or 2 ? point.x : point.z;

        public static Vector3 ToEdgePoint(int edge, float travel, float halfSize) =>
            edge switch
            {
                0 => new Vector3(travel, 0f, halfSize),
                1 => new Vector3(halfSize, 0f, travel),
                2 => new Vector3(travel, 0f, -halfSize),
                _ => new Vector3(-halfSize, 0f, travel),
            };

        public static Vector3 GetJunctionPoint(int edge, float halfSize) =>
            edge switch
            {
                0 => new Vector3(0f, 0f, halfSize),
                1 => new Vector3(halfSize, 0f, 0f),
                2 => new Vector3(0f, 0f, -halfSize),
                _ => new Vector3(-halfSize, 0f, 0f),
            };

        public static Vector3 WithHeight(Vector3 point)
        {
            point.y = LaneHeight;
            return point;
        }

        public static void AppendUnique(List<Vector3> points, Vector3 point)
        {
            if (points.Count > 0)
            {
                var last = points[^1];
                last.y = 0f;
                var candidate = point;
                candidate.y = 0f;
                if (Vector3.Distance(last, candidate) < 0.05f)
                {
                    return;
                }
            }

            points.Add(point);
        }

        public static Vector3 GetStripJoinPoint(Vector3 barracks, Vector3 basePosition, float halfSize)
        {
            barracks.y = 0f;
            var edge = PerimeterPathBuilder.GetEdgeIndex(basePosition, halfSize);
            var travel = ClampTravelToStrip(GetTravelCoord(barracks, edge));
            return WithHeight(ToEdgePoint(edge, travel, halfSize));
        }

        public static float ClampTravelToStrip(float travel)
        {
            if (travel >= NegativeHalfMin && travel <= NegativeHalfMax)
            {
                return travel;
            }

            if (travel >= PositiveHalfMin && travel <= PositiveHalfMax)
            {
                return travel;
            }

            if (travel < 0f)
            {
                return travel < (NegativeHalfMax + PositiveHalfMin) * 0.5f
                    ? NegativeHalfMax
                    : PositiveHalfMin;
            }

            return travel < (NegativeHalfMax + PositiveHalfMin) * 0.5f
                ? NegativeHalfMax
                : PositiveHalfMin;
        }

        public static bool IsInSpokeGap(float travel) =>
            travel > NegativeHalfMax && travel < PositiveHalfMin;

        public static void AppendBarracksToStripJoin(
            List<Vector3> points,
            Vector3 barracks,
            Vector3 basePosition,
            float halfSize)
        {
            var join = GetStripJoinPoint(barracks, basePosition, halfSize);
            var edge = PerimeterPathBuilder.GetEdgeIndex(basePosition, halfSize);
            var barracksTravel = GetTravelCoord(barracks, edge);

            if (IsInSpokeGap(barracksTravel))
            {
                AppendUnique(points, WithHeight(GetJunctionPoint(edge, halfSize)));
            }

            AppendUnique(points, join);
        }

        public static void AppendStripJoinToBarracks(
            List<Vector3> points,
            Vector3 join,
            Vector3 barracks,
            Vector3 basePosition,
            float halfSize)
        {
            var edge = PerimeterPathBuilder.GetEdgeIndex(basePosition, halfSize);
            var barracksTravel = GetTravelCoord(barracks, edge);

            AppendUnique(points, join);

            if (IsInSpokeGap(barracksTravel))
            {
                AppendUnique(points, WithHeight(GetJunctionPoint(edge, halfSize)));
            }

            AppendUnique(points, WithHeight(barracks));
        }

        public static bool IsClockwiseTravelIncreasing(int edge, bool clockwise) =>
            edge switch
            {
                0 => clockwise,
                1 => !clockwise,
                2 => !clockwise,
                _ => clockwise,
            };

        public static void AppendStripEdgeTravel(
            List<Vector3> points,
            int edge,
            float fromTravel,
            float toTravel,
            bool clockwise,
            float halfSize)
        {
            if (Mathf.Approximately(fromTravel, toTravel))
            {
                return;
            }

            var increasing = IsClockwiseTravelIncreasing(edge, clockwise);
            if (increasing && fromTravel > toTravel || !increasing && fromTravel < toTravel)
            {
                return;
            }

            var travel = fromTravel;
            while (!Mathf.Approximately(travel, toTravel))
            {
                if (NeedsGapCrossing(travel, toTravel, increasing))
                {
                    AppendJunctionGapCrossing(points, edge, increasing, halfSize);
                    travel = increasing ? PositiveHalfMin : NegativeHalfMax;
                    continue;
                }

                travel = toTravel;
                AppendUnique(points, WithHeight(ToEdgePoint(edge, travel, halfSize)));
            }
        }

        public static void AppendJunctionGapCrossing(
            List<Vector3> points,
            int edge,
            bool travelIncreasing,
            float halfSize)
        {
            var gapNear = travelIncreasing ? NegativeHalfMax : PositiveHalfMin;
            var gapFar = travelIncreasing ? PositiveHalfMin : NegativeHalfMax;

            AppendUnique(points, WithHeight(ToEdgePoint(edge, gapNear, halfSize)));
            AppendUnique(points, WithHeight(GetJunctionPoint(edge, halfSize)));
            AppendUnique(points, WithHeight(ToEdgePoint(edge, gapFar, halfSize)));
        }

        public static void AppendCornerArc(
            List<Vector3> points,
            Vector3 corner,
            bool turnClockwise)
        {
            PerimeterCornerArc.AppendPathWaypoints(points, corner, turnClockwise, LaneHeight);
        }

        public static LanePath BuildSharedFlankRing(float halfSize)
        {
            var h = halfSize;
            var r = N4RoadReferenceSpec.PerimeterCornerCenterlineRadius;
            var points = new List<Vector3>();

            AppendUnique(points, WithHeight(new Vector3(-h + r, 0f, h)));
            AppendStripEdgeTravel(points, 0, -h + r, h - r, clockwise: true, h);
            AppendCornerArc(points, new Vector3(h, 0f, h), turnClockwise: true);

            AppendStripEdgeTravel(points, 1, h - r, -h + r, clockwise: true, h);
            AppendCornerArc(points, new Vector3(h, 0f, -h), turnClockwise: true);

            AppendStripEdgeTravel(points, 2, h - r, -h + r, clockwise: true, h);
            AppendCornerArc(points, new Vector3(-h, 0f, -h), turnClockwise: true);

            AppendStripEdgeTravel(points, 3, -h + r, h - r, clockwise: true, h);
            AppendCornerArc(points, new Vector3(-h, 0f, h), turnClockwise: true);

            AppendUnique(points, points[0]);
            return new LanePath(points);
        }

        static bool NeedsGapCrossing(float fromTravel, float toTravel, bool increasing)
        {
            if (increasing)
            {
                return fromTravel <= NegativeHalfMax && toTravel >= PositiveHalfMin;
            }

            return fromTravel >= PositiveHalfMin && toTravel <= NegativeHalfMax;
        }
    }
}
