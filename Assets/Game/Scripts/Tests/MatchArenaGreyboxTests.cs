using System.Collections.Generic;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchArenaGreyboxTests
    {
        [Test]
        public void CreateSpec_N4_HasBaseArenaWidth()
        {
            Assert.AreEqual(40f, MatchArenaGreyboxBuilder.BaseArenaWidth);
        }

        [Test]
        public void Rebuild_BuildsLayoutAndGraph_WithoutThrowing()
        {
            var go = new GameObject("GreyboxRebuildTest");
            try
            {
                var greybox = go.AddComponent<MatchArenaGreybox>();
                greybox.Configure(2);
                Assert.IsNotNull(greybox.Layout);
                Assert.IsNotNull(greybox.Graph);
                Assert.AreEqual(2, greybox.Layout.PlayerCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void N4_HandTunedRoadConstants_MatchPrefab()
        {
            Assert.AreEqual(70f, MatchArenaGreyboxBuilder.PerimeterHalfStripLength);
            Assert.AreEqual(55f, MatchArenaGreyboxBuilder.PerimeterHalfStripCenter);
            Assert.AreEqual(30f, MatchArenaGreyboxBuilder.BaseArenaDepth);
            Assert.AreEqual(5f, MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
        }

        [Test]
        public void N2_SideFlankConstants_UseTwentyUnitStrips()
        {
            Assert.AreEqual(20f, N2RoadReferenceSpec.SideFlankHalfStripLength);
            Assert.AreEqual(40f, N2RoadReferenceSpec.SideFlankOuterBound);
            Assert.AreEqual(65f, N2RoadReferenceSpec.GetNorthSouthRoadEdge());
            Assert.AreEqual(25f, N2RoadReferenceSpec.CornerRadius);
        }

        [Test]
        public void PopulateN2_SideFlankStrips_EndAtOuterBound()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsN2FlankTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N2SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);
                var walkable = WalkableSurfaceBuilder.BuildFromSourceParts(sourceParts);
                Assert.IsTrue(walkable.Contains(new Vector3(120f, 0f, 30f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PerimeterCornerArc_N2_AsymmetricCorner_HasValidMesh()
        {
            var corner = N2RoadReferenceSpec.GetMapCornerArcCorner(0);
            var mesh = PerimeterCornerArc.BuildCornerRoadMesh(
                corner,
                turnClockwise: true,
                MatchArenaGreyboxBuilder.RoadHeight);

            Assert.Greater(mesh.vertexCount, 0);
            Assert.Greater(mesh.triangles.Length, 0);

            PerimeterCornerArc.GetClockwiseEndpoints(corner, out var entry, out var exit);
            foreach (var vertex in mesh.vertices)
            {
                var flat = new Vector3(vertex.x, 0f, vertex.z);
                Assert.Less(Vector3.Distance(flat, entry), 45f);
                Assert.Less(Vector3.Distance(flat, exit), 45f);
            }
        }

        [Test]
        public void PopulateN2_CornerArcs_KeepN4Radius()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsN2CornerTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N2SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);

                PerimeterCornerArc.GetClockwiseEndpoints(
                    N2RoadReferenceSpec.GetMapCornerArcCorner(0),
                    out var entry,
                    out var exit);
                AssertVector3(new Vector3(95f, 0f, 65f), entry);
                AssertVector3(new Vector3(120f, 0f, 40f), exit);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN2_SourceParts_MatchesN4SquareLayout()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsN2Test");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N2SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);
                Assert.AreEqual(N2SourcePartsBuilder.PartCount, sourceParts.childCount);
                Assert.AreEqual(1, CountNamedChildren(sourceParts, RoadSurfaceMeshBuilder.ObjectName));
                Assert.AreEqual(0, CountNamedChildren(sourceParts, "RoadStrip"));
                Assert.AreEqual(0, CountNamedChildren(sourceParts, "FlankArcNorth"));
                Assert.AreEqual(0, CountNamedChildren(sourceParts, "RoadCorner"));
                Assert.IsNull(root.transform.Find("Roads"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN2_PlayerSlots_HaveNoRoadGeometry()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsN2SlotsTest");
            try
            {
                MatchArenaGreyboxBuilder.Populate(root.transform, layout, graph);
                for (var slot = 0; slot < layout.PlayerCount; slot++)
                {
                    var slotRoot = root.transform.Find($"Bases/Player_{slot}");
                    Assert.NotNull(slotRoot);
                    foreach (Transform child in slotRoot)
                    {
                        Assert.IsFalse(
                            child.name is "RoadStrip" or "RoadFilletArc" or "RoadCorner" or "BaseArena",
                            $"Player_{slot} should not contain road geometry '{child.name}'.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN2_BaseArenas_AreOnEastAndWest()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsN2BasesTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N2SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);
                var walkable = WalkableSurfaceBuilder.BuildFromSourceParts(sourceParts);
                Assert.IsTrue(walkable.Contains(new Vector3(125f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(-125f, 0f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void N2SourceParts_HasExpectedPartCount()
        {
            Assert.AreEqual(1, N2SourcePartsBuilder.PartCount);
            Assert.AreEqual(N2SourcePartsBuilder.PartCount, N2RoadReferenceSpec.SourcePartsCount);
        }

        [Test]
        public void PopulateN4_SourceParts_HasExpectedChildCount()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N4SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);
                Assert.AreEqual(N4SourcePartsBuilder.PartCount, sourceParts.childCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN4_SourceParts_TypeCounts()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N4SourcePartsBuilder.RootName);
                Assert.AreEqual(1, CountNamedChildren(sourceParts, RoadSurfaceMeshBuilder.ObjectName));
                Assert.AreEqual(0, CountNamedChildren(sourceParts, "RoadStrip"));
                Assert.AreEqual(0, CountNamedChildren(sourceParts, "CenterArena"));
                Assert.AreEqual(0, CountNamedChildren(sourceParts, "BaseArena"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN4_CenterArena_IsFlatCircle()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N4SourcePartsBuilder.RootName);
                Assert.AreEqual(1, CountNamedChildren(sourceParts, RoadSurfaceMeshBuilder.ObjectName));
                Assert.IsNull(sourceParts.Find("CenterArena/Platform"));
                Assert.IsNull(sourceParts.Find("Platform"));

                var roadSurface = FindFirstNamedChild(sourceParts, RoadSurfaceMeshBuilder.ObjectName);
                var mesh = roadSurface.GetComponent<MeshFilter>()?.sharedMesh;
                Assert.NotNull(mesh);
                Assert.GreaterOrEqual(mesh.bounds.size.x, N4RoadReferenceSpec.CenterArenaDiameter - 0.5f);
                Assert.GreaterOrEqual(mesh.bounds.size.z, N4RoadReferenceSpec.CenterArenaDiameter - 0.5f);

                var walkable = WalkableSurfaceBuilder.BuildFromSourceParts(sourceParts);
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, 24f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN4_RoadGeometry_MatchesHandTunedPrefab()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N4SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);
                Assert.AreEqual(1, CountNamedChildren(sourceParts, RoadSurfaceMeshBuilder.ObjectName));

                var walkable = WalkableSurfaceBuilder.BuildFromSourceParts(sourceParts);
                Assert.IsTrue(walkable.Contains(new Vector3(-57.5f, 0f, 120f)));
                Assert.IsTrue(walkable.Contains(new Vector3(54.5f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(125f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, 125f)));
                Assert.IsTrue(walkable.Contains(new Vector3(-125f, 0f, 0f)));
                Assert.IsTrue(walkable.Contains(new Vector3(0f, 0f, -125f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateN4_PlayerSlots_HaveNoRoadGeometry()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("ArenaRoadsTest");
            try
            {
                MatchArenaGreyboxBuilder.Populate(root.transform, layout, graph);
                for (var slot = 0; slot < layout.PlayerCount; slot++)
                {
                    var slotRoot = root.transform.Find($"Bases/Player_{slot}");
                    Assert.NotNull(slotRoot);
                    foreach (Transform child in slotRoot)
                    {
                        Assert.IsFalse(
                            child.name is "RoadStrip" or "RoadFilletArc" or "RoadCorner" or "BaseArena",
                            $"Player_{slot} should not contain road geometry '{child.name}'.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void N4SourceParts_HasExpectedPartCount()
        {
            Assert.AreEqual(1, N4SourcePartsBuilder.PartCount);
            Assert.AreEqual(N4SourcePartsBuilder.PartCount, N4RoadReferenceSpec.SourcePartsCount);
        }

        static int CountNamedChildren(Transform parent, string objectName)
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

        static Transform FindFirstNamedChild(Transform parent, string objectName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }

        static void AssertSourcePartStripBounds(Transform sourceParts, Vector3 expectedCenter, Vector3 expectedSize)
        {
            Transform match = null;
            foreach (Transform child in sourceParts)
            {
                if (child.name != "RoadStrip")
                {
                    continue;
                }

                var mesh = child.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                var bounds = mesh.bounds;
                if (Vector3.Distance(bounds.center, expectedCenter) < 0.1f
                    && Vector3.Distance(bounds.size, expectedSize) < 0.1f)
                {
                    match = child;
                    break;
                }
            }

            Assert.NotNull(match, $"Missing road strip near {expectedCenter} size {expectedSize}");
        }

        static void AssertSourcePartStrip(Transform sourceParts, Vector3 expectedPosition, Vector3 expectedScale)
        {
            Transform match = null;
            foreach (Transform child in sourceParts)
            {
                if (child.name != "RoadStrip")
                {
                    continue;
                }

                if (Vector3.Distance(child.position, expectedPosition) < 0.05f
                    && Vector3.Distance(child.localScale, expectedScale) < 0.05f)
                {
                    match = child;
                    break;
                }
            }

            Assert.NotNull(match, $"Missing road strip at {expectedPosition}");
        }

        static void AssertVector3(Vector3 expected, Vector3 actual, float tolerance = 0.05f)
        {
            Assert.Less(Mathf.Abs(expected.x - actual.x), tolerance, $"X mismatch: expected {expected.x}, got {actual.x}");
            Assert.Less(Mathf.Abs(expected.y - actual.y), tolerance, $"Y mismatch: expected {expected.y}, got {actual.y}");
            Assert.Less(Mathf.Abs(expected.z - actual.z), tolerance, $"Z mismatch: expected {expected.z}, got {actual.z}");
        }

        [Test]
        public void RoadPlatformMesh_FacesUp_ForRectAndDisc()
        {
            var rect = RoadPlatformMesh.BuildRect(new Vector3(40f, MatchArenaGreyboxBuilder.RoadHeight, 30f));
            var disc = RoadPlatformMesh.BuildDisc(50f, MatchArenaGreyboxBuilder.RoadHeight);

            Assert.Greater(rect.normals[0].y, 0.9f);
            Assert.Greater(disc.normals[0].y, 0.9f);
            Assert.AreEqual(6, rect.triangles.Length);
            Assert.AreEqual(4, new HashSet<int>(rect.triangles).Count, "Rect must use all four corners.");
            Assert.AreEqual(40f, rect.bounds.size.x, 0.01f);
            Assert.AreEqual(30f, rect.bounds.size.z, 0.01f);
        }

        [Test]
        public void RoadStripMesh_FacesUp_ForOppositeTravelDirections()
        {
            var north = RoadStripMesh.BuildStraight(
                new Vector3(0f, 0f, 19f),
                new Vector3(0f, 0f, 90f),
                MatchArenaGreyboxBuilder.RoadWidth,
                MatchArenaGreyboxBuilder.RoadHeight);
            var south = RoadStripMesh.BuildStraight(
                new Vector3(0f, 0f, -90f),
                new Vector3(0f, 0f, -19f),
                MatchArenaGreyboxBuilder.RoadWidth,
                MatchArenaGreyboxBuilder.RoadHeight);

            Assert.Greater(north.normals[0].y, 0.9f);
            Assert.Greater(south.normals[0].y, 0.9f);
        }

        [Test]
        public void RoadStripMesh_HasWidthPerpendicularToTravelDirection()
        {
            var mesh = RoadStripMesh.BuildStraight(
                new Vector3(-95f, 0f, 120f),
                new Vector3(-20f, 0f, 120f),
                MatchArenaGreyboxBuilder.RoadWidth,
                MatchArenaGreyboxBuilder.RoadHeight);

            Assert.Greater(mesh.vertexCount, 0);
            var bounds = mesh.bounds;
            Assert.Greater(bounds.size.z, 10f, "North strip width should span Z.");
            Assert.Greater(bounds.size.x, bounds.size.z, "North strip length should run along X.");
        }

        [Test]
        public void SpokeStripBounds_StopBeforeJunctionFilletAndCenterArena()
        {
            const float halfSize = 120f;
            N4RoadReferenceSpec.GetPositiveZSpokeStrip(halfSize, out var from, out var to);

            Assert.AreEqual(19f, from.z, 0.01f);
            Assert.AreEqual(90f, to.z, 0.01f);
            Assert.AreEqual(71f, to.z - from.z, 0.01f);
        }

        [Test]
        public void N4_ReferenceConstants_MatchHandTunedPrefab()
        {
            Assert.AreEqual(25f, N4RoadReferenceSpec.PerimeterCornerCenterlineRadius);
            Assert.AreEqual(35f, N4RoadReferenceSpec.PerimeterCornerMeshHalfExtent);
            Assert.AreEqual(55.25f, N4RoadReferenceSpec.SpokeConnectorCenter);
            Assert.AreEqual(70.5f, N4RoadReferenceSpec.SpokeConnectorLength);
        }

        [Test]
        public void CreateSpec_N4_HasBaseArenaRadius()
        {
            Assert.AreEqual(40f, MatchArenaGreyboxBuilder.DefaultBaseArenaRadius * 2f);
        }

        [Test]
        public void CreateSpec_N4_HasRoundedCornerCount()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var spec = MatchArenaGreyboxBuilder.CreateSpec(layout, graph);

            Assert.AreEqual(4, spec.CenterRingMarkerCount);
            Assert.AreEqual(MatchArenaGreyboxBuilder.PerimeterCornerArcRadius, PerimeterCornerArc.CornerArcRadius);
        }

        [Test]
        public void PerimeterCornerArc_AllCorners_UseDistinctAngles()
        {
            const float halfSize = 120f;
            var corners = new[]
            {
                new Vector3(halfSize, 0f, halfSize),
                new Vector3(halfSize, 0f, -halfSize),
                new Vector3(-halfSize, 0f, -halfSize),
                new Vector3(-halfSize, 0f, halfSize),
            };

            foreach (var corner in corners)
            {
                var mesh = PerimeterCornerArc.BuildCornerRoadMesh(corner, turnClockwise: true, MatchArenaGreyboxBuilder.RoadHeight);
                Assert.Greater(mesh.vertexCount, 0);
                Assert.Greater(mesh.triangles.Length, 0);
            }
        }

        [Test]
        public void CreateSpec_N4_HasExpectedCounts()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var spec = MatchArenaGreyboxBuilder.CreateSpec(layout, graph);

            Assert.AreEqual(4, spec.PlayerCount);
            Assert.AreEqual(32, spec.BuildingMarkerCount);
            Assert.AreEqual(5, spec.LaneLineCount);
            Assert.AreEqual(4, spec.CenterRingMarkerCount);
        }

        [Test]
        public void CreateSpec_N2_HasDuelLaneCount()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            var spec = MatchArenaGreyboxBuilder.CreateSpec(layout, graph);

            Assert.AreEqual(16, spec.BuildingMarkerCount);
            Assert.AreEqual(3, spec.LaneLineCount);
        }

        [Test]
        public void CreateSpec_N8_HasTwentyFourLanes()
        {
            var layout = MatchArenaGenerator.Generate(8);
            var graph = LaneGraphBuilder.Build(layout);
            var spec = MatchArenaGreyboxBuilder.CreateSpec(layout, graph);

            Assert.AreEqual(64, spec.BuildingMarkerCount);
            Assert.AreEqual(9, spec.LaneLineCount);
        }
    }
}
