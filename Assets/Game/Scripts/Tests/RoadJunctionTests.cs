using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RoadJunctionTests
    {
        [Test]
        public void BaseTurnRadius_IsSharperThanPerimeter()
        {
            Assert.Less(RoadJunctionBuilder.BaseTurnRadius, PerimeterCornerArc.CornerArcRadius);
            Assert.Less(RoadJunctionBuilder.BaseTurnRadius, RoadJunctionBuilder.CenterLineTurnRadius);
        }

        [Test]
        public void GetTurnArcRadius_BaseExtent_LeavesStraightRunFromBarracks()
        {
            var offsets = BaseLayoutDefinition.GetLocalOffsets(MatchArenaGenerator.DefaultMainToTowerDistance);
            var extentZ = offsets[GameIds.Buildings.BarracksCenter].z;
            var radius = RoadJunctionBuilder.GetTurnArcRadius(extentZ);
            var centerStripStart = radius + radius;

            Assert.Less(centerStripStart, extentZ);
            Assert.Greater(extentZ - centerStripStart, extentZ * 0.4f);
        }

        [Test]
        public void SouthSpokeFillet_ConnectsSpokeToWestPerimeter()
        {
            const float h = 120f;
            var radius = RoadJunctionBuilder.CenterLineTurnRadius;
            var junction = new Vector3(0f, 0f, -h);
            var entry = junction - Vector3.back * radius;
            var exit = junction + Vector3.left * radius;

            Assert.AreEqual(0f, entry.x, 0.01f);
            Assert.AreEqual(-h + radius, entry.z, 0.01f);
            Assert.AreEqual(-h, exit.z, 0.01f);
            Assert.AreEqual(-radius, exit.x, 0.01f);
        }

        [Test]
        public void BaseCrossRoads_TopFillet_StartsBelowBarracks()
        {
            var offsets = BaseLayoutDefinition.GetLocalOffsets(MatchArenaGenerator.DefaultMainToTowerDistance);
            var extentZ = offsets[GameIds.Buildings.BarracksCenter].z;
            var turnRadius = Mathf.Min(RoadJunctionBuilder.BaseTurnRadius, extentZ * 0.25f);
            var centerStripStart = turnRadius + turnRadius;

            Assert.Less(centerStripStart, extentZ);
            Assert.Greater(extentZ - centerStripStart, extentZ * 0.4f);
        }
    }
}
