using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchArenaGeneratorTests
    {
        [Test]
        public void Generate_N2_UsesDuelTopology()
        {
            var layout = MatchArenaGenerator.Generate(2);
            Assert.AreEqual(TopologyKind.Duel, layout.Topology);
            Assert.AreEqual(GameIds.Topology.Duel, layout.TopologyId);
            Assert.AreEqual(2, layout.Slots.Count);
            Assert.AreEqual(6, layout.Lanes.Count);
        }

        [Test]
        public void Generate_N2_OppositeBasesShareCenterArenaTargets()
        {
            var layout = MatchArenaGenerator.Generate(2, arenaRadius: 100f);
            var distance = Vector3.Distance(layout.Slots[0].BasePosition, layout.Slots[1].BasePosition);
            Assert.Greater(distance, 150f);
            Assert.AreEqual(1, layout.Slots[0].CenterPrimaryTargetSlot);
            Assert.AreEqual(0, layout.Slots[1].CenterPrimaryTargetSlot);

            var centerLanes = 0;
            foreach (var lane in layout.Lanes)
            {
                if (lane.IsCenterLane)
                {
                    centerLanes++;
                }
            }

            Assert.AreEqual(2, centerLanes);
        }

        [Test]
        public void Generate_N4_NeighborsAndCenterTarget()
        {
            var layout = MatchArenaGenerator.Generate(4);
            Assert.AreEqual(TopologyKind.Ring, layout.Topology);
            Assert.AreEqual(3, layout.Slots[0].LeftOpponentSlot);
            Assert.AreEqual(1, layout.Slots[0].RightOpponentSlot);
            Assert.AreEqual(2, layout.Slots[0].CenterPrimaryTargetSlot);
        }

        [Test]
        public void Generate_N5_HasThreeLanesPerPlayer()
        {
            var layout = MatchArenaGenerator.Generate(5);
            Assert.AreEqual(15, layout.Lanes.Count);
            foreach (var slot in layout.Slots)
            {
                Assert.AreEqual(MatchArenaGenerator.Mod(slot.SlotIndex - 1, 5), slot.LeftOpponentSlot);
                Assert.AreEqual(MatchArenaGenerator.Mod(slot.SlotIndex + 1, 5), slot.RightOpponentSlot);
                Assert.AreEqual(MatchArenaGenerator.Mod(slot.SlotIndex + 2, 5), slot.CenterPrimaryTargetSlot);
            }
        }

        [Test]
        public void BaseLayout_HasEightBuildingsAndRearAlongNegativeLocalZ()
        {
            var offsets = BaseLayoutDefinition.GetLocalOffsets(8f);
            Assert.AreEqual(BaseLayoutDefinition.BuildingsPerBase, offsets.Count);
            Assert.AreEqual(new Vector3(0f, 0f, 12f), offsets[GameIds.Buildings.BarracksCenter]);
            Assert.Less(offsets[GameIds.Buildings.TowerSw].z, 0f);
            Assert.Less(offsets[GameIds.Buildings.TowerSe].z, 0f);
        }

        [Test]
        public void BaseLayout_Facing_MainAndCenterTowardRoad_SidesTowardCreepExit()
        {
            Assert.AreEqual(Vector3.forward, BaseLayoutDefinition.GetLocalFacingDirection(GameIds.Buildings.Main));
            Assert.AreEqual(
                Vector3.forward,
                BaseLayoutDefinition.GetLocalFacingDirection(GameIds.Buildings.BarracksCenter));
            Assert.AreEqual(
                Vector3.left,
                BaseLayoutDefinition.GetLocalFacingDirection(GameIds.Buildings.BarracksLeft));
            Assert.AreEqual(
                Vector3.right,
                BaseLayoutDefinition.GetLocalFacingDirection(GameIds.Buildings.BarracksRight));
            Assert.AreEqual(
                Vector3.forward,
                BaseLayoutDefinition.GetLocalFacingDirection(GameIds.Buildings.TowerNw));
        }

        [Test]
        public void BaseLayout_LocalRotation_DoorAxisAlignsWithFacing()
        {
            // TT meshes: door on +X; after GetLocalRotation the door axis is the desired face.
            AssertDoorFaces(GameIds.Buildings.Main, Vector3.forward);
            AssertDoorFaces(GameIds.Buildings.BarracksCenter, Vector3.forward);
            AssertDoorFaces(GameIds.Buildings.BarracksLeft, Vector3.left);
            AssertDoorFaces(GameIds.Buildings.BarracksRight, Vector3.right);
        }

        static void AssertDoorFaces(string buildingId, Vector3 expectedFace)
        {
            var rotation = BaseLayoutDefinition.GetLocalRotation(buildingId);
            var doorAxis = rotation * Vector3.right;
            Assert.Greater(
                Vector3.Dot(doorAxis.normalized, expectedFace.normalized),
                0.99f,
                $"{buildingId} door should face {expectedFace}, got {doorAxis}");
        }

        [Test]
        public void SlotRotation_ForwardPointsTowardArenaCenter()
        {
            var layout = MatchArenaGenerator.Generate(4, arenaRadius: 50f);
            foreach (var slot in layout.Slots)
            {
                var forward = slot.BaseRotation * Vector3.forward;
                var toCenter = Vector3.zero - slot.BasePosition;
                toCenter.y = 0f;
                var dot = Vector3.Dot(forward.normalized, toCenter.normalized);
                Assert.Greater(dot, 0.99f, $"Slot {slot.SlotIndex} should face map center.");
            }
        }
    }
}
