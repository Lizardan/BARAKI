using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class N4PerimeterLaneGeometryTests
    {
        const float HalfSize = MatchArenaGenerator.DefaultArenaRadius;

        [Test]
        public void HalfStripBounds_MatchHandTunedRoad()
        {
            Assert.AreEqual(-90f, N4PerimeterLaneGeometry.NegativeHalfMin, 0.01f);
            Assert.AreEqual(-20f, N4PerimeterLaneGeometry.NegativeHalfMax, 0.01f);
            Assert.AreEqual(20f, N4PerimeterLaneGeometry.PositiveHalfMin, 0.01f);
            Assert.AreEqual(90f, N4PerimeterLaneGeometry.PositiveHalfMax, 0.01f);
        }

        [Test]
        public void BuildSharedFlankRing_N4_CrossesSpokeJunctions()
        {
            var path = PerimeterRingPathBuilder.BuildSharedFlankRing(HalfSize, playerCount: 4);
            Assert.IsTrue(ContainsNear(path, new Vector3(0f, N4PerimeterLaneGeometry.LaneHeight, HalfSize)));
            Assert.IsTrue(ContainsNear(path, new Vector3(HalfSize, N4PerimeterLaneGeometry.LaneHeight, 0f)));
        }

        [Test]
        public void BuildSharedFlankRing_N4_AvoidsGapExceptAtJunctions()
        {
            var path = PerimeterRingPathBuilder.BuildSharedFlankRing(HalfSize, playerCount: 4);
            var gapSamples = 0;

            for (var i = 0; i < path.WaypointCount; i++)
            {
                var point = path.GetWaypoint(i);
                if (Mathf.Abs(point.z - HalfSize) < 0.1f
                    && point.x > N4PerimeterLaneGeometry.NegativeHalfMax + 0.1f
                    && point.x < N4PerimeterLaneGeometry.PositiveHalfMin - 0.1f
                    && !Mathf.Approximately(point.x, 0f))
                {
                    gapSamples++;
                }
            }

            Assert.AreEqual(0, gapSamples);
        }

        static bool ContainsNear(LanePath path, Vector3 target)
        {
            for (var i = 0; i < path.WaypointCount; i++)
            {
                if (Vector3.Distance(path.GetWaypoint(i), target) < 0.1f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
