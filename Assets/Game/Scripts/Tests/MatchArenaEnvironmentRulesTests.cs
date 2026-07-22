using System.Linq;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchArenaEnvironmentRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            WalkableSurfaceCache.Clear();
        }

        [Test]
        public void CanPlace_OnWalkable_Rejects()
        {
            var surface = WalkableSurfaceBuilder.FromTriangles(
                new Vector2(-5f, -5f),
                new Vector2(5f, -5f),
                new Vector2(0f, 5f));

            Assert.IsFalse(MatchArenaEnvironmentRules.CanPlace(new Vector3(0f, 0f, 0f), surface, null));
            Assert.IsTrue(MatchArenaEnvironmentRules.CanPlace(new Vector3(50f, 0f, 50f), surface, null));
        }

        [Test]
        public void CanPlace_PathPiece_AllowsWalkable()
        {
            var surface = WalkableSurfaceBuilder.FromTriangles(
                new Vector2(-5f, -5f),
                new Vector2(5f, -5f),
                new Vector2(0f, 5f));

            Assert.IsTrue(MatchArenaEnvironmentRules.CanPlace(
                new Vector3(0f, 0f, 0f),
                surface,
                null,
                EnvironmentPropKind.PathPiece,
                arenaRadius: 120f));
            Assert.IsTrue(MatchArenaEnvironmentRules.FootprintClearOfWalkable(
                new Vector3(0f, 0f, 0f),
                EnvironmentPropKind.PathPiece,
                surface));
        }

        [Test]
        public void CanPlace_NearBase_Rejects()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var slot = layout.Slots[0];
            Assert.IsFalse(MatchArenaEnvironmentRules.CanPlace(
                slot.BasePosition,
                walkable: null,
                layout.Slots));
        }

        [Test]
        public void CanPlace_OutsideMap_Rejects()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var far = new Vector3(layout.ArenaRadius + 80f, 0f, 0f);
            Assert.IsFalse(MatchArenaEnvironmentRules.CanPlace(
                far,
                walkable,
                layout.Slots,
                EnvironmentPropKind.Tree,
                layout.ArenaRadius));
        }

        [Test]
        public void BuildPlacements_N4_NonOverlayFootprintsStayOffWalkable()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            Assert.Greater(placements.Count, 10);
            Assert.Greater(MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Tree), 0);
            Assert.Greater(MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Mountain)
                + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Cliff), 0);

            foreach (var placement in placements)
            {
                if (MatchArenaEnvironmentRules.AllowsWalkableOverlay(placement.Kind))
                {
                    continue;
                }

                Assert.IsTrue(
                    MatchArenaEnvironmentRules.IsWithinMapBounds(placement.Position, layout.ArenaRadius),
                    $"{placement.Kind} at {placement.Position} is outside map.");
                Assert.IsTrue(
                    MatchArenaEnvironmentRules.FootprintClearOfWalkable(
                        placement.Position,
                        placement.Kind,
                        walkable),
                    $"Footprint of {placement.Kind} at {placement.Position} hits walkable.");
                Assert.IsTrue(
                    MatchArenaEnvironmentRules.CanPlace(
                        placement.Position,
                        walkable,
                        layout.Slots,
                        placement.Kind,
                        layout.ArenaRadius,
                        MatchArenaEnvironmentRules.MinBaseDistance * 0.4f),
                    $"Placement {placement.Kind} at {placement.Position} failed CanPlace.");
            }
        }

        [Test]
        public void BuildPlacements_N4_CobbleCoversWalkableUniformly()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);
            var pathPieces = placements
                .Where(p => p.Kind == EnvironmentPropKind.PathPiece)
                .ToList();

            Assert.Greater(pathPieces.Count, 200);
            Assert.AreEqual(pathPieces.Count, pathPieces.Count(p => walkable.Contains(p.Position)));

            // Spot-check: random walkable samples should have a nearby cobble.
            var hits = 0;
            var samples = 40;
            var half = MatchArenaEnvironmentRules.MapHalfExtent(layout.ArenaRadius);
            for (var i = 0; i < samples; i++)
            {
                var angle = (Mathf.PI * 2f * i) / samples;
                var probe = new Vector3(Mathf.Cos(angle) * layout.ArenaRadius, 0f, Mathf.Sin(angle) * layout.ArenaRadius);
                if (!walkable.Contains(probe))
                {
                    probe = walkable.Clamp(probe);
                }

                var near = pathPieces.Any(p =>
                {
                    var dx = p.Position.x - probe.x;
                    var dz = p.Position.z - probe.z;
                    return dx * dx + dz * dz < 4f * 4f;
                });
                if (near)
                {
                    hits++;
                }
            }

            Assert.Greater(hits, samples * 0.7f);
            Assert.LessOrEqual(Mathf.Abs(half), layout.ArenaRadius + 30f);
        }

        [Test]
        public void BuildPlacements_N4_HasRoadsideClutter()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            Assert.Greater(MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Flower), 10);
            Assert.Greater(MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Lantern), 0);
            Assert.Greater(
                MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Crate)
                + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Bench),
                0);
        }

        [Test]
        public void BuildPlacements_N4_PropsStayInsideMap()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);
            var max = MatchArenaEnvironmentRules.MapHalfExtent(layout.ArenaRadius);

            foreach (var placement in placements)
            {
                Assert.LessOrEqual(Mathf.Abs(placement.Position.x), max + 0.05f);
                Assert.LessOrEqual(Mathf.Abs(placement.Position.z), max + 0.05f);
            }
        }

        [Test]
        public void BuildPlacements_N4_HasDenseTreesAndRivers()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            var trees = MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Tree)
                        + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Pine);
            Assert.Greater(trees, 40);
            Assert.Greater(MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.River), 0);
        }

        [Test]
        public void BuildPlacements_N2_HasNatureAndDeterministic()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var walkable = WalkableSurfaceCache.GetOrCreate(2);
            var a = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);
            var b = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            Assert.Greater(a.Count, 5);
            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(MatchArenaEnvironmentRules.CountKind(a, EnvironmentPropKind.Tree)
                + MatchArenaEnvironmentRules.CountKind(a, EnvironmentPropKind.Pine), 0);

            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Position.x, b[i].Position.x, 0.001f);
                Assert.AreEqual(a[i].Position.z, b[i].Position.z, 0.001f);
            }
        }

        [Test]
        public void BuildPlacements_N4_HasMorePlacementsThanN2()
        {
            var n2 = MatchArenaEnvironmentRules.BuildPlacements(
                MatchArenaGenerator.Generate(2),
                WalkableSurfaceCache.GetOrCreate(2));
            var n4 = MatchArenaEnvironmentRules.BuildPlacements(
                MatchArenaGenerator.Generate(4),
                WalkableSurfaceCache.GetOrCreate(4));

            Assert.Greater(n4.Count, n2.Count);
        }

        [Test]
        public void Decorator_PopulateTwice_IsIdempotent()
        {
            var root = new GameObject("EnvDecorTestRoot");
            try
            {
                var layout = MatchArenaGenerator.Generate(4);
                var walkable = WalkableSurfaceCache.GetOrCreate(4);
                var prefabs = MatchArenaEnvironmentDecorator.CreateTestPrefabSet();

                MatchArenaEnvironmentDecorator.Populate(root.transform, layout, walkable, prefabs);
                var first = root.transform.Find(MatchArenaEnvironmentRules.DecorRootName);
                Assert.IsNotNull(first);
                var firstCount = first.childCount;

                MatchArenaEnvironmentDecorator.Populate(root.transform, layout, walkable, prefabs);
                var second = root.transform.Find(MatchArenaEnvironmentRules.DecorRootName);
                Assert.IsNotNull(second);
                Assert.AreEqual(1, root.transform.Cast<Transform>()
                    .Count(t => t.name == MatchArenaEnvironmentRules.DecorRootName));
                Assert.AreEqual(firstCount, second.childCount);
                Assert.Greater(firstCount, 0);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
