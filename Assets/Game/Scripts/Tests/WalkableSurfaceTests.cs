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
            // N2 CenterArena half-size = 22.
            Assert.IsTrue(surface.Contains(new Vector3(0f, 0f, 21f)));
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
