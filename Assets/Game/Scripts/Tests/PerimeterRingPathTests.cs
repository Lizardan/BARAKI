using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PerimeterRingPathTests
    {
        const float HalfSize = MatchArenaGenerator.DefaultArenaRadius;

        [Test]
        public void BuildSharedFlankRing_N4_ReturnsClosedSquarePath()
        {
            var path = PerimeterRingPathBuilder.BuildSharedFlankRing(HalfSize, playerCount: 4);

            Assert.Greater(path.WaypointCount, 40);
            Assert.Less(Vector3.Distance(path.Start, path.End), 0.01f);
        }

        [Test]
        public void BuildSharedFlankRing_N4_PassesNorthEdgeStrips()
        {
            var path = PerimeterRingPathBuilder.BuildSharedFlankRing(HalfSize, playerCount: 4);
            var hasWestHalf = false;
            var hasEastHalf = false;

            for (var i = 0; i < path.WaypointCount; i++)
            {
                var point = path.GetWaypoint(i);
                if (Mathf.Abs(point.z - HalfSize) > 0.1f)
                {
                    continue;
                }

                if (point.x <= N4PerimeterLaneGeometry.NegativeHalfMax + 0.01f
                    && point.x >= N4PerimeterLaneGeometry.NegativeHalfMin - 0.01f)
                {
                    hasWestHalf = true;
                }

                if (point.x >= N4PerimeterLaneGeometry.PositiveHalfMin - 0.01f
                    && point.x <= N4PerimeterLaneGeometry.PositiveHalfMax + 0.01f)
                {
                    hasEastHalf = true;
                }
            }

            Assert.IsTrue(hasWestHalf);
            Assert.IsTrue(hasEastHalf);
        }

        [Test]
        public void BuildSharedFlankRing_N8_ReturnsClosedCircularPath()
        {
            var path = PerimeterRingPathBuilder.BuildSharedFlankRing(HalfSize, playerCount: 8);

            Assert.AreEqual(PerimeterRingPathBuilder.CircularRingSegments + 1, path.WaypointCount);
            Assert.Less(Vector3.Distance(path.Start, path.End), 0.01f);
            Assert.AreEqual(HalfSize, path.Start.magnitude, 0.01f);
        }
    }
}
