using System.Collections.Generic;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RoadSurfaceUnionTests
    {
        [Test]
        public void Union_OverlappingRects_RemovesDuplicateArea_KeepsCoverage()
        {
            var a = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(10f, 0f),
                new Vector2(10f, 10f),
                new Vector2(0f, 10f),
            };
            var b = new[]
            {
                new Vector2(5f, 5f),
                new Vector2(15f, 5f),
                new Vector2(15f, 15f),
                new Vector2(5f, 15f),
            };

            var unioned = RoadPolygonUnion.Union(new List<Vector2[]> { a, b });
            Assert.GreaterOrEqual(unioned.Count, 1);

            Assert.IsTrue(RoadFootprintShapes.PointInAnyPolygon(new Vector2(2f, 2f), unioned));
            Assert.IsTrue(RoadFootprintShapes.PointInAnyPolygon(new Vector2(12f, 12f), unioned));
            Assert.IsTrue(RoadFootprintShapes.PointInAnyPolygon(new Vector2(7f, 7f), unioned));
            Assert.IsFalse(RoadFootprintShapes.PointInAnyPolygon(new Vector2(20f, 20f), unioned));

            var area = 0f;
            foreach (var poly in unioned)
            {
                area += Mathf.Abs(SignedArea(poly));
            }

            Assert.Less(area, 10f * 10f + 10f * 10f - 1f);
            Assert.Greater(area, 10f * 10f + 10f * 10f - 5f * 5f - 1f);
        }

        [Test]
        public void PopulateN4_EmitsSingleRoadSurface_WithCoverage()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("RoadSurfaceUnionN4");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N4SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);
                Assert.AreEqual(1, sourceParts.childCount);
                Assert.AreEqual(1, CountNamed(sourceParts, RoadSurfaceMeshBuilder.ObjectName));

                var surface = sourceParts.GetChild(0);
                var filter = surface.GetComponent<MeshFilter>();
                Assert.NotNull(filter);
                Assert.NotNull(filter.sharedMesh);
                Assert.Greater(filter.sharedMesh.triangles.Length, 0);
                Assert.Greater(filter.sharedMesh.normals[0].y, 0.9f);

                var walkable = WalkableSurfaceBuilder.BuildFromSourceParts(sourceParts);
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, 50f)));
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, 120f)));
                Assert.IsTrue(walkable.Contains(new Vector3(110f, 0f, 110f)));
                Assert.IsTrue(walkable.Contains(new Vector3(125f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(120f, 0f, -12f)), "East barracks pad must stay walkable.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN2_EmitsSingleRoadSurface_WithCoverage()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("RoadSurfaceUnionN2");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N2SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);
                Assert.AreEqual(1, sourceParts.childCount);
                Assert.AreEqual(RoadSurfaceMeshBuilder.ObjectName, sourceParts.GetChild(0).name);

                var walkable = WalkableSurfaceBuilder.BuildFromSourceParts(sourceParts);
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(120f, 0f, 30f)));
                Assert.IsTrue(walkable.Contains(new Vector3(125f, 0f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static float SignedArea(Vector2[] polygon)
        {
            var area = 0f;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                area += (polygon[j].x * polygon[i].y) - (polygon[i].x * polygon[j].y);
            }

            return area * 0.5f;
        }

        static int CountNamed(Transform parent, string objectName)
        {
            var count = 0;
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
