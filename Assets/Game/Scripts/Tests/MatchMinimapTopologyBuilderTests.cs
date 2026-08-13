using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchMinimapTopologyBuilderTests
    {
        [Test]
        public void Build_N4_IncludesCenterDiscBaseRectsAndRoadNetwork()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var topology = MatchMinimapTopologyBuilder.Build(layout, graph);

            Assert.AreEqual(4, topology.FilledRects.Count);
            Assert.Greater(topology.RoadSegments.Count, 20);
            Assert.AreEqual(N4RoadReferenceSpec.CenterArenaHalfSize, topology.CenterArenaRadius, 0.001f);

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

            Assert.AreEqual(2, topology.FilledRects.Count);
            Assert.GreaterOrEqual(topology.RoadSegments.Count, 4);
            Assert.AreEqual(N4RoadReferenceSpec.CenterArenaHalfSize, topology.CenterArenaRadius, 0.001f);

            var maxCoord = 0f;
            foreach (var segment in topology.RoadSegments)
            {
                maxCoord = Mathf.Max(maxCoord, Mathf.Abs(segment.A.x), Mathf.Abs(segment.A.y));
                maxCoord = Mathf.Max(maxCoord, Mathf.Abs(segment.B.x), Mathf.Abs(segment.B.y));
            }

            Assert.GreaterOrEqual(maxCoord, layout.ArenaRadius - 1f);
        }

        [Test]
        public void Build_CenterSpokes_ReachPerimeterJunctions_ForAllModes()
        {
            for (var players = 2; players <= 8; players++)
            {
                var layout = MatchArenaGenerator.Generate(players);
                var graph = LaneGraphBuilder.Build(layout);
                var topology = MatchMinimapTopologyBuilder.Build(layout, graph);

                foreach (var slot in layout.Slots)
                {
                    var junction = new Vector2(slot.BasePosition.x, slot.BasePosition.z);
                    Assert.IsTrue(
                        HasSegmentTouching(topology, junction, 0.05f),
                        $"N={players} slot {slot.SlotIndex} spoke does not reach perimeter junction {junction}");
                }
            }
        }

        [Test]
        public void Build_BasePads_AreCenteredOnMain()
        {
            for (var players = 2; players <= 8; players++)
            {
                var layout = MatchArenaGenerator.Generate(players);
                var graph = LaneGraphBuilder.Build(layout);
                var topology = MatchMinimapTopologyBuilder.Build(layout, graph);

                foreach (var rect in topology.FilledRects)
                {
                    Assert.GreaterOrEqual(rect.OwnerSlot, 0);
                    var slot = layout.Slots[rect.OwnerSlot];
                    Assert.AreEqual(slot.BasePosition.x, rect.Center.x, 0.05f, $"N={players} slot {rect.OwnerSlot} x");
                    Assert.AreEqual(slot.BasePosition.z, rect.Center.y, 0.05f, $"N={players} slot {rect.OwnerSlot} z");
                }
            }
        }

        [Test]
        public void Build_N3_BaseRectsMatchWorldPads()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var graph = LaneGraphBuilder.Build(layout);
            var topology = MatchMinimapTopologyBuilder.Build(layout, graph);

            Assert.AreEqual(3, topology.FilledRects.Count);

            var size = new Vector3(
                MatchArenaGreyboxBuilder.BaseArenaWidth,
                MatchArenaGreyboxBuilder.RoadHeight,
                MatchArenaGreyboxBuilder.BaseArenaDepth);

            foreach (var rect in topology.FilledRects)
            {
                Assert.GreaterOrEqual(rect.OwnerSlot, 0);
                var slot = layout.Slots[rect.OwnerSlot];
                var expected = RoadFootprintShapes.OrientedRect(slot.BasePosition, slot.BaseRotation, size);

                for (var i = 0; i < 4; i++)
                {
                    var actual = rect.GetWorldCorner(i);
                    Assert.AreEqual(expected[i].x, actual.x, 0.05f, $"slot {rect.OwnerSlot} corner {i} x");
                    Assert.AreEqual(expected[i].y, actual.y, 0.05f, $"slot {rect.OwnerSlot} corner {i} z");
                }
            }
        }

        static bool HasSegmentTouching(MatchMinimapTopology topology, Vector2 point, float tolerance)
        {
            var maxDistSq = tolerance * tolerance;
            foreach (var segment in topology.RoadSegments)
            {
                if ((segment.A - point).sqrMagnitude <= maxDistSq
                    || (segment.B - point).sqrMagnitude <= maxDistSq)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
