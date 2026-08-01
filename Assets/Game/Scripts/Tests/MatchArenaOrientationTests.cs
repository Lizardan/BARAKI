using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchArenaOrientationTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(8)]
        public void Generate_Slot0_IsAtBottomCenter(int playerCount)
        {
            var layout = MatchArenaGenerator.Generate(playerCount);
            var slot0 = layout.Slots[0].BasePosition;
            Assert.AreEqual(0f, slot0.x, 0.5f, "Slot 0 must be centered on X.");
            Assert.Less(slot0.z, -layout.ArenaRadius * 0.5f, "Slot 0 must sit on the bottom (−Z) side.");
            Assert.AreEqual(layout.ArenaRadius, slot0.magnitude, 0.5f);
        }

        [Test]
        public void Build_MinimapN3_IncludesStraightEdgeBetweenJoins()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var graph = LaneGraphBuilder.Build(layout);
            var topology = MatchMinimapTopologyBuilder.Build(layout, graph);
            var a = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                layout.Slots[0], GameIds.Buildings.BarracksRight, layout.ArenaRadius);
            var b = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                layout.Slots[1], GameIds.Buildings.BarracksLeft, layout.ArenaRadius);
            var mid = Vector3.Lerp(a, b, 0.5f);
            var found = false;
            foreach (var segment in topology.RoadSegments)
            {
                if (PointNearSegment(new Vector2(mid.x, mid.z), segment.A, segment.B, 2f))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Minimap must draw the straight perimeter edge between exit joins.");
        }

        static bool PointNearSegment(Vector2 point, Vector2 a, Vector2 b, float maxDist)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
            {
                return Vector2.Distance(point, a) <= maxDist;
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            return Vector2.Distance(point, a + ab * t) <= maxDist;
        }
    }
}
