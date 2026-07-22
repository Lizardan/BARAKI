using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class WalkableSurfaceTests
    {
        [TearDown]
        public void TearDown()
        {
            WalkableSurfaceCache.Clear();
        }

        [Test]
        public void FromTriangles_ContainsInside_RejectsOutside()
        {
            var surface = WalkableSurfaceBuilder.FromTriangles(
                new Vector2(0f, 0f),
                new Vector2(10f, 0f),
                new Vector2(0f, 10f));

            Assert.IsTrue(surface.Contains(new Vector3(1f, 0f, 1f)));
            Assert.IsFalse(surface.Contains(new Vector3(9f, 0f, 9f)));
        }

        [Test]
        public void Clamp_Outside_PullsOntoEdge()
        {
            var surface = WalkableSurfaceBuilder.FromTriangles(
                new Vector2(-5f, -5f),
                new Vector2(5f, -5f),
                new Vector2(0f, 5f));

            var clamped = surface.Clamp(new Vector3(0f, 0.15f, 20f));
            Assert.IsTrue(surface.Contains(clamped));
            Assert.Less(clamped.z, 6f);
            Assert.AreEqual(0.15f, clamped.y, 0.001f);
        }

        [Test]
        public void Cache_GetOrCreate_ReusesSameInstance()
        {
            var a = WalkableSurfaceCache.GetOrCreate(4);
            var b = WalkableSurfaceCache.GetOrCreate(4);
            Assert.AreSame(a, b);
            Assert.Greater(a.PartCount, 0);
        }

        [Test]
        public void N4_CenterArenaInterior_IsWalkable()
        {
            var surface = WalkableSurfaceCache.GetOrCreate(4);
            // Visual CenterArena half-size = 25; logic circle was 20 — r=24 must be walkable now.
            Assert.IsTrue(
                surface.Contains(new Vector3(0f, 0f, 24f)),
                "N4 SourceParts CenterArena should allow positions beyond legacy r=20.");
        }

        [Test]
        public void N4_FarOffMap_IsNotWalkable_AndClamps()
        {
            var surface = WalkableSurfaceCache.GetOrCreate(4);
            var far = new Vector3(500f, 0f, 500f);
            Assert.IsFalse(surface.Contains(far));

            var clamped = surface.Clamp(far);
            Assert.IsTrue(surface.Contains(clamped));
            Assert.Less(clamped.magnitude, 200f);
        }

        [Test]
        public void N2_CenterArenaInterior_IsWalkable()
        {
            var surface = WalkableSurfaceCache.GetOrCreate(2);
            // N2 CenterArena matches N=4 (half-size = 25).
            Assert.IsTrue(surface.Contains(new Vector3(0f, 0f, 24f)));
        }

        [Test]
        public void N2_FlankRoadSamples_AreWalkable()
        {
            var surface = WalkableSurfaceCache.GetOrCreate(2);
            var samples = new[]
            {
                new Vector3(120f, 0f, 30f),
                new Vector3(-120f, 0f, 30f),
                new Vector3(0f, 0f, 65f),
                new Vector3(57.5f, 0f, 65f),
                new Vector3(-57.5f, 0f, -65f),
            };

            foreach (var sample in samples)
            {
                Assert.IsTrue(surface.Contains(sample), $"Expected walkable at {sample}");
            }
        }

        [Test]
        public void N2_FlankPath_MarchStepsAdvanceAlongWalkableSurface()
        {
            WalkableSurfaceCache.Clear();
            var surface = WalkableSurfaceCache.GetOrCreate(2);
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(0, GameIds.Lanes.Left, out var lane);
            var route = LaneRoute.FromPath(lane.Path);

            var progress = CombatFormationRules.BarracksSpawnForwardClearance;
            var position = route.EvaluateDistance(progress);
            const float speed = 8f;
            const float deltaTime = 0.05f;
            var stalledTicks = 0;

            for (var tick = 0; tick < 400; tick++)
            {
                var maxStep = speed * deltaTime;
                progress = route.ProjectDistanceForward(position, progress);
                var destination = UnitLocomotionRules.GetRouteLookaheadDestination(
                    route,
                    position,
                    maxStep,
                    progress);
                var proposed = UnitLocomotionRules.MoveTowards(
                    position,
                    destination,
                    maxStep,
                    null,
                    out _,
                    0);
                var next = UnitLocomotionRules.ApplyWalkableLimit(
                    route,
                    position,
                    proposed,
                    maxStep,
                    progress,
                    LaneGraphBuilder.DefaultCenterArenaRadius,
                    surface);

                var moved = Vector3.Distance(
                    new Vector3(position.x, 0f, position.z),
                    new Vector3(next.x, 0f, next.z));
                if (moved < 0.001f)
                {
                    stalledTicks++;
                    if (stalledTicks > 10)
                    {
                        Assert.Fail(
                            $"March stalled at progress={progress:F2} pos={position} walkable={surface.Contains(position)}");
                    }
                }
                else
                {
                    stalledTicks = 0;
                }

                position = next;
                progress = route.AdvanceProgress(
                    progress,
                    route.ProjectDistanceForward(position, progress));
            }

            Assert.Greater(progress, CombatFormationRules.BarracksSpawnForwardClearance + 20f);
        }

        [Test]
        public void N2_FlankLanePath_IsOnWalkableSurface()
        {
            WalkableSurfaceCache.Clear();
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var surface = WalkableSurfaceCache.GetOrCreate(2);

            foreach (var lane in graph.Lanes)
            {
                if (lane.IsCenterLane)
                {
                    continue;
                }

                var spacing = 5f;
                for (var distance = 0f; distance <= lane.Path.TotalLength; distance += spacing)
                {
                    var point = lane.Path.EvaluateDistance(distance);
                    Assert.IsTrue(
                        surface.Contains(point),
                        $"Lane P{lane.OwnerSlot}_{lane.LaneId} off walkable at d={distance:F1} pos={point}");
                }
            }
        }

        [Test]
        public void N2_WalkablePartCount_MatchesSourcePartsMeshes()
        {
            WalkableSurfaceCache.Clear();
            var surface = WalkableSurfaceCache.GetOrCreate(2);
            Assert.AreEqual(1, surface.PartCount);
        }

        [Test]
        public void N4_FlankLanePath_IsOnWalkableSurface()
        {
            WalkableSurfaceCache.Clear();
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var surface = WalkableSurfaceCache.GetOrCreate(4);

            foreach (var lane in graph.Lanes)
            {
                if (lane.IsCenterLane)
                {
                    continue;
                }

                var spacing = 8f;
                for (var distance = 0f; distance <= lane.Path.TotalLength; distance += spacing)
                {
                    var point = lane.Path.EvaluateDistance(distance);
                    Assert.IsTrue(
                        surface.Contains(point),
                        $"Lane P{lane.OwnerSlot}_{lane.LaneId} off walkable at d={distance:F1} pos={point}");
                }
            }
        }

        [Test]
        public void ApplyWalkableLimit_WithSurface_IgnoresLaneCorridor()
        {
            var path = new LanePath(new System.Collections.Generic.List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(40f, 0f, 0f),
            });
            var route = LaneRoute.FromPath(path);

            // Wide square covering a point that corridor would reject.
            var surface = WalkableSurfaceBuilder.FromTriangles(
                new Vector2(-30f, -30f),
                new Vector2(30f, -30f),
                new Vector2(30f, 30f),
                new Vector2(-30f, -30f),
                new Vector2(30f, 30f),
                new Vector2(-30f, 30f));

            var previous = new Vector3(20f, 0f, UnitLocomotionRules.RoadHalfWidth + 4f);
            var proposed = new Vector3(20f, 0f, UnitLocomotionRules.RoadHalfWidth + 5f);
            Assert.IsTrue(surface.Contains(proposed));

            var result = UnitLocomotionRules.ApplyWalkableLimit(
                route,
                previous,
                proposed,
                maxStep: 10f,
                progressDistance: 20f,
                centerArenaRadius: 0f,
                surface);

            Assert.AreEqual(proposed.x, result.x, 0.01f);
            Assert.AreEqual(proposed.z, result.z, 0.01f);
        }
    }
}
