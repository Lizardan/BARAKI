using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Procedural arena layout for N=2..8. Splines (MVP-003) attach to barracks world positions.
    /// </summary>
    public static class MatchArenaGenerator
    {
        public const float DefaultArenaRadius = 120f;
        public const float DefaultMainToTowerDistance = 8f;

        /// <summary>
        /// World angle of slot 0. −π/2 puts the first player at bottom-center (−Z)
        /// on the minimap and in the default top-down view.
        /// </summary>
        public const float FirstPlayerAngleRadians = -Mathf.PI * 0.5f;

        /// <summary>
        /// World half-extent of the Ground plane along X/Z.
        /// Matches arena ring radius plus one road-width margin beyond the road square.
        /// </summary>
        public static float GroundHalfExtent(float arenaRadius = DefaultArenaRadius) =>
            arenaRadius + MatchArenaGreyboxBuilder.RoadWidth;

        /// <summary>Unity plane is 10×10 at scale 1; scale so world half-extent equals <see cref="GroundHalfExtent"/>.</summary>
        public static float DefaultGroundPlaneScale => GroundHalfExtent() * 2f / 10f;

        /// <summary>
        /// N=2 road/path assets are authored with player 0 at +X; rotate into layout space
        /// where player 0 sits at <see cref="FirstPlayerAngleRadians"/>.
        /// </summary>
        public static Vector3 RotateAuthoredToLayout(Vector3 authored) =>
            RotateYaw(authored, FirstPlayerAngleRadians);

        public static Vector3 RotateLayoutToAuthored(Vector3 layout) =>
            RotateYaw(layout, -FirstPlayerAngleRadians);

        public static MatchArenaLayout Generate(
            int playerCount,
            float arenaRadius = DefaultArenaRadius,
            float mainToTowerDistance = DefaultMainToTowerDistance)
        {
            if (playerCount is < 2 or > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Player count must be 2..8.");
            }

            var topology = playerCount == 2 ? TopologyKind.Duel : TopologyKind.Ring;
            var topologyId = topology == TopologyKind.Duel ? GameIds.Topology.Duel : GameIds.Topology.Ring;
            var buildingOffsets = BaseLayoutDefinition.GetLocalOffsets(mainToTowerDistance);

            var slots = new List<PlayerSlotLayout>(playerCount);
            for (var i = 0; i < playerCount; i++)
            {
                var angle = FirstPlayerAngleRadians + 2f * Mathf.PI * i / playerCount;
                var position = new Vector3(Mathf.Cos(angle) * arenaRadius, 0f, Mathf.Sin(angle) * arenaRadius);
                var forward = Vector3.zero - position;
                forward.y = 0f;
                var rotation = forward.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                    : Quaternion.identity;

                slots.Add(new PlayerSlotLayout
                {
                    SlotIndex = i,
                    BasePosition = position,
                    BaseRotation = rotation,
                    LeftOpponentSlot = Mod(i - 1, playerCount),
                    RightOpponentSlot = Mod(i + 1, playerCount),
                    CenterPrimaryTargetSlot = Mod(i + playerCount / 2, playerCount),
                    BuildingLocalOffsets = buildingOffsets,
                });
            }

            var lanes = BuildLaneConnections(slots);
            return new MatchArenaLayout
            {
                PlayerCount = playerCount,
                Topology = topology,
                TopologyId = topologyId,
                ArenaRadius = arenaRadius,
                MainToTowerDistance = mainToTowerDistance,
                Slots = slots,
                Lanes = lanes,
            };
        }

        public static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

        static Vector3 RotateYaw(Vector3 point, float yawRadians)
        {
            var cos = Mathf.Cos(yawRadians);
            var sin = Mathf.Sin(yawRadians);
            return new Vector3(
                point.x * cos - point.z * sin,
                point.y,
                point.x * sin + point.z * cos);
        }

        static List<LaneConnection> BuildLaneConnections(IReadOnlyList<PlayerSlotLayout> slots)
        {
            var lanes = new List<LaneConnection>(slots.Count * BaseLayoutDefinition.LanesPerPlayer);
            foreach (var slot in slots)
            {
                lanes.Add(new LaneConnection
                {
                    OwnerSlot = slot.SlotIndex,
                    LaneId = GameIds.Lanes.Left,
                    OriginBarracksId = GameIds.Buildings.BarracksLeft,
                    OpponentSlot = slot.LeftOpponentSlot,
                    IsCenterLane = false,
                });
                lanes.Add(new LaneConnection
                {
                    OwnerSlot = slot.SlotIndex,
                    LaneId = GameIds.Lanes.Center,
                    OriginBarracksId = GameIds.Buildings.BarracksCenter,
                    OpponentSlot = slot.CenterPrimaryTargetSlot,
                    IsCenterLane = true,
                });
                lanes.Add(new LaneConnection
                {
                    OwnerSlot = slot.SlotIndex,
                    LaneId = GameIds.Lanes.Right,
                    OriginBarracksId = GameIds.Buildings.BarracksRight,
                    OpponentSlot = slot.RightOpponentSlot,
                    IsCenterLane = false,
                });
            }

            return lanes;
        }
    }
}
