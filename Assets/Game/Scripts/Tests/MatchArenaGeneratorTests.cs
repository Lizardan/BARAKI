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
        public void Generate_N4_NeighborsAndCenterTarget()
        {
            var layout = MatchArenaGenerator.Generate(4);
            Assert.AreEqual(TopologyKind.Ring, layout.Topology);
            Assert.AreEqual(3, layout.Slots[0].LeftOpponentSlot);
            Assert.AreEqual(1, layout.Slots[0].RightOpponentSlot);
            Assert.AreEqual(2, layout.Slots[0].CenterPrimaryTargetSlot);
        }

        [Test]
        public void Generate_N8_HasThreeLanesPerPlayer()
        {
            var layout = MatchArenaGenerator.Generate(8);
            Assert.AreEqual(24, layout.Lanes.Count);
            foreach (var slot in layout.Slots)
            {
                Assert.AreEqual(MatchArenaGenerator.Mod(slot.SlotIndex - 1, 8), slot.LeftOpponentSlot);
                Assert.AreEqual(MatchArenaGenerator.Mod(slot.SlotIndex + 1, 8), slot.RightOpponentSlot);
                Assert.AreEqual(MatchArenaGenerator.Mod(slot.SlotIndex + 4, 8), slot.CenterPrimaryTargetSlot);
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
