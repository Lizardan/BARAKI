using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchMinimapTopologyBuilderTests
    {
        [Test]
        public void Build_N4_IncludesCenterBaseRectsAndRoadNetwork()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var topology = MatchMinimapTopologyBuilder.Build(layout, graph);

            Assert.GreaterOrEqual(topology.FilledRects.Count, 5);
            Assert.Greater(topology.RoadSegments.Count, 20);

            var center = topology.FilledRects[0];
            Assert.AreEqual(0f, center.Center.x, 0.001f);
            Assert.AreEqual(0f, center.Center.y, 0.001f);
            Assert.AreEqual(N4RoadReferenceSpec.CenterArenaHalfSize, center.HalfExtents.x, 0.001f);
            Assert.AreEqual(N4RoadReferenceSpec.CenterArenaHalfSize, center.HalfExtents.y, 0.001f);

            var baseRects = 0;
            foreach (var rect in topology.FilledRects)
            {
                if (rect.OwnerSlot >= 0)
                {
                    baseRects++;
                }
            }

            Assert.AreEqual(4, baseRects);
        }

        [Test]
        public void Build_GenericN2_HasPerimeterBounds()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var topology = MatchMinimapTopologyBuilder.Build(layout, graph);

            Assert.GreaterOrEqual(topology.FilledRects.Count, 3);
            Assert.GreaterOrEqual(topology.RoadSegments.Count, 4);

            var maxCoord = 0f;
            foreach (var segment in topology.RoadSegments)
            {
                maxCoord = Mathf.Max(maxCoord, Mathf.Abs(segment.A.x), Mathf.Abs(segment.A.y));
                maxCoord = Mathf.Max(maxCoord, Mathf.Abs(segment.B.x), Mathf.Abs(segment.B.y));
            }

            Assert.GreaterOrEqual(maxCoord, layout.ArenaRadius - 1f);
        }
    }
}
