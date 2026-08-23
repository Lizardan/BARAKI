using Game.Core;
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
            for (var players = 2; players <= MatchModeRules.MaxPlayers; players++)
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
            for (var players = 2; players <= MatchModeRules.MaxPlayers; players++)
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

        [TestCase(3)]
        [TestCase(5)]
        public void Build_FfaRing_SharedPathClosesOnItself(int players)
        {
            var layout = MatchArenaGenerator.Generate(players);
            var ring = PerimeterRingPathBuilder.BuildSharedFlankRing(layout.ArenaRadius, players);
            Assert.IsTrue(ring.IsClosedLoop);
            var first = ring.GetWaypoint(0);
            var last = ring.GetWaypoint(ring.WaypointCount - 1);
            first.y = 0f;
            last.y = 0f;
            Assert.Less((first - last).sqrMagnitude, 0.05f);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Build_MinimapRing_HasNoOpenPerimeterGap(int players)
        {
            var layout = MatchArenaGenerator.Generate(players);
            var graph = LaneGraphBuilder.Build(layout);
            var topology = MatchMinimapTopologyBuilder.Build(layout, graph);
            var ring = PerimeterRingPathBuilder.BuildSharedFlankRing(layout.ArenaRadius, players);
            var lastIndex = ring.WaypointCount - 1;
            for (var i = 1; i <= lastIndex; i++)
            {
                var a = ring.GetWaypoint(i - 1);
                var b = ring.GetWaypoint(i);
                Assert.IsTrue(
                    HasSegmentNear(topology, a, b, 0.4f),
                    $"N={players} ring gap between waypoints {i - 1} and {i}");
            }
        }

        static bool HasSegmentNear(
            MatchMinimapTopology topology,
            Vector3 a,
            Vector3 b,
            float tolerance)
        {
            var from = new Vector2(a.x, a.z);
            var to = new Vector2(b.x, b.z);
            var maxDistSq = tolerance * tolerance;
            foreach (var segment in topology.RoadSegments)
            {
                var a0 = (segment.A - from).sqrMagnitude <= maxDistSq;
                var b0 = (segment.B - to).sqrMagnitude <= maxDistSq;
                var a1 = (segment.A - to).sqrMagnitude <= maxDistSq;
                var b1 = (segment.B - from).sqrMagnitude <= maxDistSq;
                if ((a0 && b0) || (a1 && b1))
                {
                    return true;
                }
            }

            return false;
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
