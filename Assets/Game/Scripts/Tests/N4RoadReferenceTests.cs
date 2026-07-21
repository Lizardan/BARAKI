using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class N4RoadReferenceTests
    {
        const float HalfSize = MatchArenaGenerator.DefaultArenaRadius;
        const float Tolerance = 0.06f;

        [Test]
        public void PerimeterHalfStripCornerOuter_MatchesCornerArcTangent()
        {
            var halfSize = MatchArenaGenerator.DefaultArenaRadius;
            var cornerOuter = N4RoadReferenceSpec.GetPerimeterStripCornerOuter(halfSize);
            Assert.AreEqual(95f, cornerOuter, Tolerance);
            Assert.AreEqual(75f, N4RoadReferenceSpec.PerimeterHalfStripCornerLength, Tolerance);

            var corner = new Vector3(-halfSize, 0f, halfSize);
            PerimeterCornerArc.GetClockwiseEndpoints(corner, out var entry, out var exit);
            Assert.AreEqual(-cornerOuter, exit.x, Tolerance);
            Assert.AreEqual(cornerOuter, entry.z, Tolerance);
        }

        [Test]
        public void ProceduralCornerArc_NE_MatchesReferenceEndpoints()
        {
            var corner = new Vector3(HalfSize, 0f, HalfSize);
            PerimeterCornerArc.GetClockwiseEndpoints(corner, out var entry, out var exit);

            Assert.AreEqual(HalfSize - N4RoadReferenceSpec.PerimeterCornerCenterlineRadius, entry.x, Tolerance);
            Assert.AreEqual(HalfSize, entry.z, Tolerance);
            Assert.AreEqual(HalfSize, exit.x, Tolerance);
            Assert.AreEqual(HalfSize - N4RoadReferenceSpec.PerimeterCornerCenterlineRadius, exit.z, Tolerance);

            var arcCenter = N4RoadReferenceSpec.GetCornerArcCenterlineCenter(corner);
            Assert.AreEqual(arcCenter.x, entry.x, Tolerance);
            Assert.AreEqual(arcCenter.z, exit.z, Tolerance);
        }

        [Test]
        public void ProceduralCornerArc_CenterlineEndpoints_EquidistantFromEdges()
        {
            var corner = new Vector3(HalfSize, 0f, HalfSize);
            PerimeterCornerArc.GetClockwiseEndpoints(corner, out var entry, out var exit);
            var halfWidth = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;
            var innerNorth = HalfSize - halfWidth;
            var innerEast = HalfSize - halfWidth;

            Assert.AreEqual(halfWidth, entry.z - innerNorth, Tolerance);
            Assert.AreEqual(halfWidth, exit.x - innerEast, Tolerance);
        }

        [Test]
        public void PopulateN4_IncludesSpokeConnectorStrips()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var root = new GameObject("N4RoadReferenceTest");
            try
            {
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);
                var sourceParts = root.transform.Find(N4SourcePartsBuilder.RootName);
                Assert.NotNull(sourceParts);

                var foundConnector = false;
                foreach (Transform child in sourceParts)
                {
                    if (child.name != "RoadStrip")
                    {
                        continue;
                    }

                    if (Vector3.Distance(child.position, new Vector3(0f, 0f, 55.25f)) < Tolerance
                        && Mathf.Abs(child.localScale.z - 70.5f) < Tolerance)
                    {
                        foundConnector = true;
                        break;
                    }
                }

                Assert.IsTrue(foundConnector, "Missing N-S spoke connector strip at (0, 55.25).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SharedFlankRing_N4_IsClosedAndUsesCornerRadius()
        {
            var path = N4RoadCenterlineBuilder.BuildSharedFlankRing(HalfSize);
            Assert.Less(Vector3.Distance(path.Start, path.End), 0.01f);

            PerimeterCornerArc.GetClockwiseEndpoints(
                new Vector3(HalfSize, 0f, HalfSize),
                out var entry,
                out _);
            Assert.AreEqual(HalfSize - N4RoadReferenceSpec.PerimeterCornerCenterlineRadius, entry.x, Tolerance);
            Assert.AreEqual(HalfSize, entry.z, Tolerance);
        }

        [Test]
        public void SharedFlankRing_N4_AvoidsGapExceptAtJunctions()
        {
            var path = N4RoadCenterlineBuilder.BuildSharedFlankRing(HalfSize);
            var gapSamples = 0;

            for (var i = 0; i < path.WaypointCount; i++)
            {
                var point = path.GetWaypoint(i);
                if (Mathf.Abs(point.z - HalfSize) < 0.1f
                    && point.x > N4RoadReferenceSpec.NegativeHalfMax + 0.1f
                    && point.x < N4RoadReferenceSpec.PositiveHalfMin - 0.1f
                    && !Mathf.Approximately(point.x, 0f))
                {
                    gapSamples++;
                }
            }

            Assert.AreEqual(0, gapSamples);
        }

        [Test]
        public void SharedFlankRing_N4_CrossesAllSpokeJunctions()
        {
            var path = N4RoadCenterlineBuilder.BuildSharedFlankRing(HalfSize);
            Assert.IsTrue(ContainsNear(path, new Vector3(0f, N4PerimeterLaneGeometry.LaneHeight, HalfSize)));
            Assert.IsTrue(ContainsNear(path, new Vector3(HalfSize, N4PerimeterLaneGeometry.LaneHeight, 0f)));
            Assert.IsTrue(ContainsNear(path, new Vector3(0f, N4PerimeterLaneGeometry.LaneHeight, -HalfSize)));
            Assert.IsTrue(ContainsNear(path, new Vector3(-HalfSize, N4PerimeterLaneGeometry.LaneHeight, 0f)));
        }

        [Test]
        public void FlankPath_N4_SideBarracks_DoesNotCutAcrossSpokeGapOnEdge()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);

            foreach (var laneId in new[] { GameIds.Lanes.Left, GameIds.Lanes.Right })
            {
                graph.TryGetLane(1, laneId, out var lane);
                for (var i = 1; i < lane.Path.WaypointCount - 1; i++)
                {
                    var point = lane.Path.GetWaypoint(i);
                    if (Mathf.Abs(point.z - HalfSize) > 0.1f)
                    {
                        continue;
                    }

                    if (point.x > N4RoadReferenceSpec.NegativeHalfMax + 0.1f
                        && point.x < N4RoadReferenceSpec.PositiveHalfMin - 0.1f
                        && Mathf.Abs(point.x) > 0.25f)
                    {
                        Assert.Fail($"{laneId} flank path samples gap at {point}");
                    }
                }
            }
        }

        [Test]
        public void FlankPath_N4_DoesNotSampleInsideGapExceptJunction()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Left, out var lane);

            for (var i = 1; i < lane.Path.WaypointCount - 1; i++)
            {
                var point = lane.Path.GetWaypoint(i);
                if (Mathf.Abs(point.z - HalfSize) > 0.1f)
                {
                    continue;
                }

                if (point.x > N4RoadReferenceSpec.NegativeHalfMax + 0.1f
                    && point.x < N4RoadReferenceSpec.PositiveHalfMin - 0.1f
                    && Mathf.Abs(point.x) > 0.25f)
                {
                    Assert.Fail($"Flank path samples gap at {point}");
                }
            }
        }

        static bool ContainsNear(LanePath path, Vector3 target)
        {
            for (var i = 0; i < path.WaypointCount; i++)
            {
                if (Vector3.Distance(path.GetWaypoint(i), target) < 0.1f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
