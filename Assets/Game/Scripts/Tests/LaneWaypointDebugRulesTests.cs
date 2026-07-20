using System.Collections.Generic;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class LaneWaypointDebugRulesTests
    {
        [Test]
        public void AppendWaypoints_CopiesFlattenedPathPointsInOrder()
        {
            var path = new LanePath(new List<Vector3>
            {
                new Vector3(0f, 2f, 0f),
                new Vector3(5f, 3f, 0f),
                new Vector3(5f, 4f, 10f),
            });
            var result = new List<Vector3>();

            LaneWaypointDebugRules.AppendWaypoints(path, result);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), result[0]);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), result[1]);
            Assert.AreEqual(new Vector3(5f, 0f, 10f), result[2]);
        }

        [Test]
        public void AppendWaypoints_NullPath_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                LaneWaypointDebugRules.AppendWaypoints(null, new List<Vector3>()));
        }

        [Test]
        public void AppendWaypoints_BuiltLane_HasAtLeastTwoPoints()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, Game.Core.GameIds.Lanes.Center, out var lane));

            var result = new List<Vector3>();
            LaneWaypointDebugRules.AppendWaypoints(lane.Path, result);

            Assert.GreaterOrEqual(result.Count, 2);
        }
    }
}
