using System.Linq;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchArenaEnvironmentRulesTests
    {
        static readonly string[] OccaTreePrefabPaths =
        {
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Pine Tree_1.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Pine Tree_2.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Pine Tree_3.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Pine Tree_4.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Pine Tree_5.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_1.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_2.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_3.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_4.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_5.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_6.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_7.prefab",
            "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/Tree_8.prefab",
        };

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
        public void BuildPlacements_N4_DoesNotPlaceMeshDecorOnWalkable()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            Assert.AreEqual(0, MatchArenaEnvironmentRules.CountKind(
                placements,
                EnvironmentPropKind.PathPiece));
            Assert.IsFalse(placements.Any(placement => walkable.Contains(placement.Position)));
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
        public void BuildPlacements_N4_RoadsRemainClearOfCobbleAndRocks()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            Assert.AreEqual(0, MatchArenaEnvironmentRules.CountKind(
                placements,
                EnvironmentPropKind.PathPiece));
            Assert.IsFalse(placements
                .Where(placement => placement.Kind == EnvironmentPropKind.Rock)
                .Any(placement => walkable.Contains(placement.Position)));
        }

        [Test]
        public void BuildPlacements_N4_HasRoadsideClutter()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            Assert.Greater(MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Lantern), 0);
            Assert.Greater(MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Pine), 10);
            Assert.Greater(
                MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Crate)
                + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Bench),
                0);
        }

        [Test]
        public void BuildPlacements_N4_DarkFantasyPaletteDominates()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            var pineCount = MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Pine);
            var treeCount = MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Tree);
            var ruggedCount = pineCount
                              + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Rock)
                              + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Cliff)
                              + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Mountain);
            var brightVillageCount = MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Flower)
                                     + MatchArenaEnvironmentRules.CountKind(placements, EnvironmentPropKind.Boat);

            Assert.GreaterOrEqual(pineCount, treeCount);
            Assert.Greater(ruggedCount, brightVillageCount * 2);
            Assert.Less(brightVillageCount, placements.Count * 0.08f);
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
        public void BuildPlacements_N4_NoFloatingProps()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var walkable = WalkableSurfaceCache.GetOrCreate(4);
            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);

            foreach (var placement in placements)
            {
                // Rivers/boats sit slightly above water plane; everything else is grounded.
                if (placement.Kind is EnvironmentPropKind.River or EnvironmentPropKind.Boat
                    or EnvironmentPropKind.PathPiece)
                {
                    Assert.Less(placement.Position.y, 0.2f, $"{placement.Kind} unexpectedly high.");
                    continue;
                }

                Assert.AreEqual(0f, placement.Position.y, 0.001f, $"{placement.Kind} floats at y={placement.Position.y}");
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
        public void BuildPlacements_N2AndN4_HaveDenseNatureWithoutRoadDecor()
        {
            var n2 = MatchArenaEnvironmentRules.BuildPlacements(
                MatchArenaGenerator.Generate(2),
                WalkableSurfaceCache.GetOrCreate(2));
            var n4 = MatchArenaEnvironmentRules.BuildPlacements(
                MatchArenaGenerator.Generate(4),
                WalkableSurfaceCache.GetOrCreate(4));

            Assert.Greater(n2.Count, 100);
            Assert.Greater(n4.Count, 100);
            Assert.AreEqual(0, MatchArenaEnvironmentRules.CountKind(n2, EnvironmentPropKind.PathPiece));
            Assert.AreEqual(0, MatchArenaEnvironmentRules.CountKind(n4, EnvironmentPropKind.PathPiece));
        }

        [Test]
        public void Decorator_PopulateTwice_IsIdempotent()
        {
            var root = new GameObject("EnvDecorTestRoot");
            MatchArenaEnvironmentPrefabSet prefabs = null;
            try
            {
                var layout = MatchArenaGenerator.Generate(4);
                var walkable = WalkableSurfaceCache.GetOrCreate(4);
                prefabs = MatchArenaEnvironmentDecorator.CreateTestPrefabSet();

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
                if (prefabs != null)
                {
                    MatchArenaEnvironmentDecorator.DestroyTestPrefabSet(prefabs);
                }
            }
        }

        [Test]
        public void EnsureOccaColorMaterial_ReplacesWhiteMaterialButPreservesLight()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var colorMaterial = new Material(shader) { name = "OccaColor" };
            var whiteMaterial = new Material(shader) { name = "Default-Material" };
            var lightMaterial = new Material(shader) { name = "Light" };
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterials = new[] { whiteMaterial, lightMaterial };

            try
            {
                MatchArenaEnvironmentDecorator.EnsureOccaColorMaterial(root, colorMaterial);

                Assert.AreSame(colorMaterial, renderer.sharedMaterials[0]);
                Assert.AreSame(lightMaterial, renderer.sharedMaterials[1]);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(colorMaterial);
                Object.DestroyImmediate(whiteMaterial);
                Object.DestroyImmediate(lightMaterial);
            }
        }

        [Test]
        public void EnsureOccaColorMaterial_ReplacesPaletteColorMaterialWithOccaColor()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var palette = new Texture2D(4, 4) { name = "Color" };
            var paletteMaterial = new Material(shader) { name = "Color" };
            paletteMaterial.SetTexture("_BaseMap", palette);
            var replacement = new Material(shader) { name = "OccaColor" };
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = paletteMaterial;

            try
            {
                MatchArenaEnvironmentDecorator.EnsureOccaColorMaterial(root, replacement);

                Assert.AreSame(replacement, renderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(palette);
                Object.DestroyImmediate(paletteMaterial);
                Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void EnsureOccaColorMaterial_PreservesGeneratedFlowerPalette()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var flowerMaterial = new Material(shader) { name = "OccaFlower3" };
            var replacement = new Material(shader) { name = "OccaColor" };
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = flowerMaterial;

            try
            {
                MatchArenaEnvironmentDecorator.EnsureOccaColorMaterial(root, replacement);
                Assert.AreSame(flowerMaterial, renderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(flowerMaterial);
                Object.DestroyImmediate(replacement);
            }
        }

        [TestCase("Flower_1", "OccaFlower1")]
        [TestCase("Flower_2", "OccaFlower2")]
        [TestCase("Flower_3", "OccaFlower3")]
        [TestCase("Flower_4", "OccaFlower4")]
        [TestCase("Flower_5", "OccaFlower5")]
        [TestCase("Flower Pot", "OccaFlower6")]
        public void OccaFlowerPrefab_UsesDedicatedPalette(
            string prefabName,
            string materialName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/{prefabName}.prefab");
            var material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;

            Assert.IsNotNull(material);
            Assert.AreEqual(materialName, material.name);
            Assert.AreEqual(FilterMode.Point, material.mainTexture.filterMode);
        }

        [TestCaseSource(nameof(OccaTreePrefabPaths))]
        public void OccaPaletteMeshRepair_RepairPrefab_SeparatesWoodAndGradientFoliage(
            string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab);

            var source = prefab.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsTrue(source.isReadable);
            var wood = OccaPaletteMeshRepair.FindWoodTriangles(
                source.vertices,
                source.triangles,
                source.bounds);
            Assert.IsTrue(wood.Any(value => value), "A wood component must touch the tree base.");
            Assert.IsTrue(wood.Any(value => !value), "Foliage must be separate from wood.");

            var repaired = OccaPaletteMeshRepair.RepairMesh(source);
            try
            {
                var uvs = repaired.uv;
                var triangles = repaired.triangles;
                var minFoliageUv = float.MaxValue;
                var maxFoliageUv = float.MinValue;
                for (var triangle = 0; triangle < wood.Length; triangle++)
                {
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var uv = uvs[triangles[triangle * 3 + corner]].x;
                        if (wood[triangle])
                        {
                            Assert.Less(uv, 0.25f, "Wood must use the brown palette region.");
                        }
                        else
                        {
                            Assert.Greater(uv, 0.3f, "Foliage must use only the green palette region.");
                            minFoliageUv = Mathf.Min(minFoliageUv, uv);
                            maxFoliageUv = Mathf.Max(maxFoliageUv, uv);
                        }
                    }
                }

                Assert.Greater(
                    maxFoliageUv - minFoliageUv,
                    0.25f,
                    "Foliage must span a visible dark-to-light gradient.");
            }
            finally
            {
                Object.DestroyImmediate(repaired);
            }
        }

        [Test]
        public void OccaPaletteMeshRepair_TreeMaterial_UsesBilinearGradient()
        {
            var template = Resources.Load<Material>("Art/OccaColor");
            var material = OccaPaletteMeshRepair.GetOrCreateTreeMaterial(template);
            var palette = material.GetTexture("_BaseMap") as Texture2D;

            Assert.IsNotNull(palette);
            Assert.AreEqual(FilterMode.Bilinear, palette.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, palette.wrapMode);
        }
    }
}
