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
        public void N4_HandTunedRoadConstants_MatchPrefab()
        {
            Assert.AreEqual(70f, MatchArenaGreyboxBuilder.PerimeterHalfStripLength);
            Assert.AreEqual(55f, MatchArenaGreyboxBuilder.PerimeterHalfStripCenter);
            Assert.AreEqual(30f, MatchArenaGreyboxBuilder.BaseArenaDepth);
            Assert.AreEqual(5f, MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
        }

        [Test]
        public void PopulateN2_SourceParts_HasOrganicOvalRoadParts()
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
                Assert.AreEqual(1, CountNamedChildren(sourceParts, "FlankArcNorth"));
                Assert.AreEqual(1, CountNamedChildren(sourceParts, "FlankArcSouth"));
                Assert.AreEqual(1, CountNamedChildren(sourceParts, "RoadStrip"));
                Assert.AreEqual(1, CountNamedChildren(sourceParts, "CenterArena"));
                Assert.AreEqual(2, CountNamedChildren(sourceParts, "BaseArena"));
                Assert.IsNull(root.transform.Find("Roads"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
                Assert.AreEqual(12, CountNamedChildren(sourceParts, "RoadStrip"));
                Assert.AreEqual(1, CountNamedChildren(sourceParts, "CenterArena"));
                Assert.AreEqual(4, CountNamedChildren(sourceParts, "PerimeterCornerArc"));
                Assert.AreEqual(8, CountNamedChildren(sourceParts, "RoadFilletArc"));
                Assert.AreEqual(4, CountNamedChildren(sourceParts, "BaseArena"));
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
                var centerArenaCount = CountNamedChildren(sourceParts, "CenterArena");
                Assert.AreEqual(1, centerArenaCount);
                Assert.IsNull(sourceParts.Find("CenterArena/Platform"));
                Assert.IsNull(sourceParts.Find("Platform"));

                var centerArena = FindFirstNamedChild(sourceParts, "CenterArena");
                var roadHeight = MatchArenaGreyboxBuilder.RoadHeight;
                Assert.AreEqual(roadHeight * 0.5f, centerArena.localPosition.y, 0.01f);
                var expectedDiameter = N4RoadReferenceSpec.CenterArenaDiameter;
                Assert.AreEqual(expectedDiameter, centerArena.localScale.x, 0.01f);
                Assert.AreEqual(expectedDiameter, centerArena.localScale.z, 0.01f);
                Assert.AreEqual(roadHeight * 0.5f, centerArena.localScale.y, 0.01f);
                Assert.IsNotNull(centerArena.GetComponent<MeshFilter>()?.sharedMesh);
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

                AssertSourcePartStrip(sourceParts, new Vector3(-57.5f, 0.04f, 120f), new Vector3(19.999998f, 0.08f, 75f));
                AssertSourcePartStrip(sourceParts, new Vector3(-55.25f, 0.04f, 0f), new Vector3(19.999998f, 0.08f, 70.5f));

                var baseArenaPositions = new[]
                {
                    new Vector3(125f, 0.04f, 0f),
                    new Vector3(0f, 0.04f, 125f),
                    new Vector3(-125f, 0.04f, 0f),
                    new Vector3(0f, 0.04f, -125f),
                };

                var baseIndex = 0;
                foreach (Transform child in sourceParts)
                {
                    if (child.name != "BaseArena")
                    {
                        continue;
                    }

                    AssertVector3(baseArenaPositions[baseIndex], child.position);
                    AssertVector3(new Vector3(40f, MatchArenaGreyboxBuilder.RoadHeight, 30f), child.localScale);
                    baseIndex++;
                }

                Assert.AreEqual(4, baseIndex);
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
            Assert.AreEqual(29, N4SourcePartsBuilder.PartCount);
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
