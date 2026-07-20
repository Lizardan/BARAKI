using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BarracksSpawnDebugRulesTests
    {
        [Test]
        public void BuildRegion_PlacesSpawnAheadOfPathStart_WithPositiveArea()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, Game.Core.GameIds.Lanes.Center, out var lane));

            var region = BarracksSpawnDebugRules.BuildRegion(lane.Path);

            Assert.Greater(region.HalfLengthAlongLane, 0f);
            Assert.AreEqual(CombatFormationRules.MaxLateralOffset, region.HalfWidthLateral);
            Assert.Greater(region.SpawnPoint.sqrMagnitude, 0.01f);
            Assert.Greater(Vector3.Dot(region.Forward, region.Forward), 0.99f);
            Assert.Less(Mathf.Abs(Vector3.Dot(region.Forward, region.Right)), 0.05f);
        }

        [Test]
        public void BuildRegion_AreaCoversDistanceJitterAndRows()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, Game.Core.GameIds.Lanes.Left, out var lane));

            var region = BarracksSpawnDebugRules.BuildRegion(lane.Path, maxRowIndex: 2);
            var expectedHalfLength = (
                2 * CombatFormationRules.SpawnRowDepth
                + 2f * CombatFormationRules.SpawnDistanceJitter) * 0.5f;

            Assert.AreEqual(expectedHalfLength, region.HalfLengthAlongLane, 0.001f);
        }

        [Test]
        public void GetAreaCorners_ReturnsOrientedRectangle()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, Game.Core.GameIds.Lanes.Right, out var lane));

            var region = BarracksSpawnDebugRules.BuildRegion(lane.Path);
            region.GetAreaCorners(out var fl, out var fr, out var br, out var bl);

            Assert.AreEqual(
                region.HalfWidthLateral * 2f,
                Vector3.Distance(fl, fr),
                0.05f);
            Assert.AreEqual(
                region.HalfLengthAlongLane * 2f,
                Vector3.Distance(fr, br),
                0.05f);
            Assert.AreEqual(
                region.HalfWidthLateral * 2f,
                Vector3.Distance(br, bl),
                0.05f);
        }
    }
}
