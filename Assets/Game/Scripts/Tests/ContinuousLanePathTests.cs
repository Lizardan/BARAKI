using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ContinuousLanePathTests
    {
        [Test]
        public void CenterPath_IsOpenMarchToEnemyMain()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Center, out var lane));

            Assert.IsFalse(lane.Path.IsClosedLoop);
            var enemy = layout.Slots[lane.OpponentSlot];
            var barracks = Flat(enemy.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter));
            var main = Flat(enemy.GetBuildingWorldPosition(GameIds.Buildings.Main));
            Assert.Less(DistanceFromPath(lane.Path, barracks), 2f);
            Assert.Less(Vector3.Distance(Flat(lane.Path.End), main), 0.5f);
        }

        [Test]
        public void FlankPath_IsClosedRing()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var lane));

            Assert.IsTrue(lane.Path.IsClosedLoop);
            Assert.Less(Vector3.Distance(Flat(lane.Path.Start), Flat(lane.Path.End)), 0.25f);

            var destinationId = BaseLayoutDefinition.GetFlankDestinationBarracks(GameIds.Lanes.Left);
            var destination = Flat(layout.Slots[1].GetBuildingWorldPosition(destinationId));
            Assert.Less(DistanceFromPath(lane.Path, destination), 2f);
        }

        [Test]
        public void ClosedLoop_WrapsDistanceAndLookaheadPastEnd()
        {
            var path = new LanePath(
                new[]
                {
                    new Vector3(0f, 0.15f, 0f),
                    new Vector3(10f, 0.15f, 0f),
                    new Vector3(10f, 0.15f, 10f),
                    new Vector3(0f, 0.15f, 10f),
                },
                isClosedLoop: true);
            Assert.IsTrue(path.IsClosedLoop);
            Assert.AreEqual(0f, path.WrapDistance(path.TotalLength), 0.01f);

            var nearEnd = path.TotalLength - 1f;
            var wrapped = path.EvaluateDistance(nearEnd + 3f);
            Assert.Less(Vector3.Distance(Flat(wrapped), Flat(path.EvaluateDistance(2f))), 0.2f);

            var route = LaneRoute.FromPath(path);
            var destination = UnitLocomotionRules.GetRouteLookaheadDestination(
                route,
                path.EvaluateDistance(nearEnd),
                maxStep: 1f,
                nearEnd);
            Assert.Greater(route.ProjectDistance(destination), 0.5f);
            Assert.Less(route.ProjectDistance(destination), 8f);
        }

        static float DistanceFromPath(LanePath path, Vector3 point)
        {
            var onPath = path.EvaluateDistance(path.ProjectDistance(point));
            return Vector3.Distance(Flat(point), Flat(onPath));
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
