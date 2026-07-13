using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Shared visual path for the perimeter flank ring (all player counts).</summary>
    public static class PerimeterRingPathBuilder
    {
        public const int CircularRingSegments = 32;

        public static LanePath BuildSharedFlankRing(float ringRadius, int playerCount)
        {
            if (playerCount == 2)
            {
                return DuelPathBuilder.BuildSharedFlankRing(ringRadius);
            }

            return playerCount == 4
                ? N4RoadCenterlineBuilder.BuildSharedFlankRing(ringRadius)
                : BuildCircularClockwiseRing(ringRadius, CircularRingSegments);
        }

        public static LanePath BuildClockwiseRing(float ringRadius) =>
            BuildSharedFlankRing(ringRadius, playerCount: 4);

        static LanePath BuildCircularClockwiseRing(float radius, int segments)
        {
            var points = new System.Collections.Generic.List<Vector3>(segments + 1);
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = -2f * Mathf.PI * t;
                points.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    N4PerimeterLaneGeometry.LaneHeight,
                    Mathf.Sin(angle) * radius));
            }

            return new LanePath(points);
        }
    }
}
