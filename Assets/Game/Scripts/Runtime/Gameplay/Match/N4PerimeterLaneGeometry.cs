using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>N=4 hand-tuned perimeter lane centerlines (matches road half-strips and spoke gaps).</summary>
    public static class N4PerimeterLaneGeometry
    {
        public const float LaneHeight = 0.15f;

        public static float NegativeHalfMin => N4RoadCenterlineBuilder.NegativeHalfMin;

        public static float NegativeHalfMax => N4RoadCenterlineBuilder.NegativeHalfMax;

        public static float PositiveHalfMin => N4RoadCenterlineBuilder.PositiveHalfMin;

        public static float PositiveHalfMax => N4RoadCenterlineBuilder.PositiveHalfMax;

        public static float GetTravelCoord(Vector3 point, int edge) =>
            N4RoadCenterlineBuilder.GetTravelCoord(point, edge);

        public static Vector3 ToEdgePoint(int edge, float travel, float halfSize) =>
            N4RoadCenterlineBuilder.ToEdgePoint(edge, travel, halfSize);

        public static Vector3 GetJunctionPoint(int edge, float halfSize) =>
            N4RoadCenterlineBuilder.GetJunctionPoint(edge, halfSize);

        public static Vector3 GetStripJoinPoint(Vector3 barracks, Vector3 basePosition, float halfSize) =>
            N4RoadCenterlineBuilder.GetStripJoinPoint(barracks, basePosition, halfSize);

        public static float ClampTravelToStrip(float travel) =>
            N4RoadCenterlineBuilder.ClampTravelToStrip(travel);

        public static bool IsClockwiseTravelIncreasing(int edge, bool clockwise) =>
            N4RoadCenterlineBuilder.IsClockwiseTravelIncreasing(edge, clockwise);

        public static void AppendStripEdgeTravel(
            List<Vector3> points,
            int edge,
            float fromTravel,
            float toTravel,
            bool clockwise,
            float halfSize) =>
            N4RoadCenterlineBuilder.AppendStripEdgeTravel(points, edge, fromTravel, toTravel, clockwise, halfSize);

        public static void AppendJunctionGapCrossing(
            List<Vector3> points,
            int edge,
            bool travelIncreasing,
            float halfSize) =>
            N4RoadCenterlineBuilder.AppendJunctionGapCrossing(points, edge, travelIncreasing, halfSize);

        public static Vector3 GetEdgeTangentDir(int edge, bool travelIncreasing)
        {
            return (edge, travelIncreasing) switch
            {
                (0, true) => Vector3.right,
                (0, false) => Vector3.left,
                (1, true) => Vector3.forward,
                (1, false) => Vector3.back,
                (2, true) => Vector3.right,
                (2, false) => Vector3.left,
                (3, true) => Vector3.forward,
                _ => Vector3.back,
            };
        }

        public static Vector3 GetSpokeInwardDir(int edge)
        {
            return edge switch
            {
                0 => Vector3.back,
                1 => Vector3.left,
                2 => Vector3.forward,
                _ => Vector3.right,
            };
        }

        public static void AppendUnique(List<Vector3> points, Vector3 point) =>
            N4RoadCenterlineBuilder.AppendUnique(points, point);

        public static Vector3 WithHeight(Vector3 point) =>
            N4RoadCenterlineBuilder.WithHeight(point);
    }
}
