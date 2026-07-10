using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PerimeterCornerArcTests
    {
        const float HalfSize = 120f;

        [Test]
        public void CanonicalArcSamples_AllCorners_BulgeTowardMapCenter()
        {
            var corners = new[]
            {
                new Vector3(HalfSize, 0f, HalfSize),
                new Vector3(HalfSize, 0f, -HalfSize),
                new Vector3(-HalfSize, 0f, -HalfSize),
                new Vector3(-HalfSize, 0f, HalfSize),
            };

            foreach (var corner in corners)
            {
                var samples = PerimeterCornerArc.GetCanonicalArcSamples(corner, PerimeterCornerArc.PathArcSegments);
                var mid = samples[samples.Count / 2];
                Assert.Less(mid.magnitude, corner.magnitude, $"Arc at {corner} should cut inward toward center.");
            }
        }

        [Test]
        public void GetClockwiseEndpoints_NorthWest_MatchesPerimeterTravel()
        {
            var corner = new Vector3(-HalfSize, 0f, HalfSize);
            PerimeterCornerArc.GetClockwiseEndpoints(corner, out var entry, out var exit);

            Assert.AreEqual(-HalfSize, entry.x, 0.01f);
            Assert.AreEqual(HalfSize - PerimeterCornerArc.CornerArcRadius, entry.z, 0.01f);
            Assert.AreEqual(-HalfSize + PerimeterCornerArc.CornerArcRadius, exit.x, 0.01f);
            Assert.AreEqual(HalfSize, exit.z, 0.01f);
        }

        [Test]
        public void BuildCornerRoadMesh_NorthEast_EndpointsAlignWithStraightStrips()
        {
            const float halfSize = 120f;
            var corner = new Vector3(halfSize, 0f, halfSize);
            var halfWidth = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;
            PerimeterCornerArc.GetClockwiseEndpoints(corner, out var entry, out var exit);

            var mesh = PerimeterCornerArc.BuildCornerRoadMesh(
                corner,
                turnClockwise: true,
                MatchArenaGreyboxBuilder.RoadHeight);

            var entryInner = new Vector3(entry.x, 0f, entry.z - halfWidth);
            var entryOuter = new Vector3(entry.x, 0f, entry.z + halfWidth);
            var exitInner = new Vector3(exit.x - halfWidth, 0f, exit.z);
            var exitOuter = new Vector3(exit.x + halfWidth, 0f, exit.z);

            Assert.Less(HorizontalDistance(mesh.vertices[0], entryInner), 0.01f);
            Assert.Less(HorizontalDistance(mesh.vertices[1], entryOuter), 0.01f);

            var last = mesh.vertices.Length - 2;
            Assert.Less(HorizontalDistance(mesh.vertices[last], exitInner), 0.01f);
            Assert.Less(HorizontalDistance(mesh.vertices[last + 1], exitOuter), 0.01f);
        }

        [Test]
        public void BuildCornerRoadMesh_SouthWest_ConnectsStraightStrips()
        {
            var corner = new Vector3(-HalfSize, 0f, -HalfSize);
            var radius = PerimeterCornerArc.CornerArcRadius;
            var mesh = PerimeterCornerArc.BuildCornerRoadMesh(
                corner,
                turnClockwise: true,
                MatchArenaGreyboxBuilder.RoadHeight);

            Assert.Greater(mesh.bounds.size.x, MatchArenaGreyboxBuilder.RoadWidth * 0.9f);
            Assert.Greater(mesh.bounds.size.z, MatchArenaGreyboxBuilder.RoadWidth * 0.9f);
            Assert.Less(mesh.bounds.min.x, -HalfSize + radius);
            Assert.Less(mesh.bounds.min.z, -HalfSize + radius);
        }

        [Test]
        public void CanonicalArcSamples_ClockwiseAndCounterClockwise_UseSamePoints()
        {
            var corner = new Vector3(HalfSize, 0f, HalfSize);
            var clockwise = PerimeterCornerArc.GetCanonicalArcSamples(corner, PerimeterCornerArc.PathArcSegments);
            var counterClockwise = new List<Vector3>(clockwise);
            counterClockwise.Reverse();

            Assert.AreEqual(clockwise.Count, counterClockwise.Count);
            for (var i = 0; i < clockwise.Count; i++)
            {
                Assert.AreEqual(clockwise[i].x, counterClockwise[clockwise.Count - 1 - i].x, 0.01f);
                Assert.AreEqual(clockwise[i].z, counterClockwise[clockwise.Count - 1 - i].z, 0.01f);
            }
        }

        [Test]
        public void SharedNorthEdge_LeftAndRightLanes_UseMatchingArcSamples()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: HalfSize);
            var graph = LaneGraphBuilder.Build(layout);
            graph.TryGetLane(1, GameIds.Lanes.Left, out var leftLane);
            graph.TryGetLane(1, GameIds.Lanes.Right, out var rightLane);

            var neArc = PerimeterCornerArc.GetCanonicalArcSamples(
                new Vector3(HalfSize, 0f, HalfSize),
                PerimeterCornerArc.PathArcSegments);
            var nwArc = PerimeterCornerArc.GetCanonicalArcSamples(
                new Vector3(-HalfSize, 0f, HalfSize),
                PerimeterCornerArc.PathArcSegments);
            nwArc.Reverse();

            Assert.IsTrue(ContainsArcSequence(leftLane.Path, neArc));
            Assert.IsTrue(ContainsArcSequence(rightLane.Path, nwArc));
        }

        static bool ContainsArcSequence(LanePath path, List<Vector3> arc)
        {
            for (var start = 0; start < path.WaypointCount; start++)
            {
                if (HorizontalDistance(path.GetWaypoint(start), arc[0]) > 0.05f)
                {
                    continue;
                }

                var matches = true;
                for (var i = 1; i < arc.Count && start + i < path.WaypointCount; i++)
                {
                    if (HorizontalDistance(path.GetWaypoint(start + i), arc[i]) > 0.05f)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
