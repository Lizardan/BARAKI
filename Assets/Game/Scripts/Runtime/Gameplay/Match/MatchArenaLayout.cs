using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public enum TopologyKind
    {
        Duel,
        Ring,
    }

    public sealed class PlayerSlotLayout
    {
        public int SlotIndex { get; set; }
        public Vector3 BasePosition { get; set; }
        public Quaternion BaseRotation { get; set; }
        public int LeftOpponentSlot { get; set; }
        public int RightOpponentSlot { get; set; }
        public int CenterPrimaryTargetSlot { get; set; }
        public IReadOnlyDictionary<string, Vector3> BuildingLocalOffsets { get; set; }

        public Vector3 GetBuildingWorldPosition(string buildingId)
        {
            if (!BuildingLocalOffsets.TryGetValue(buildingId, out var local))
            {
                return BasePosition;
            }

            return BasePosition + BaseRotation * local;
        }
    }

    public sealed class LaneConnection
    {
        public int OwnerSlot { get; set; }
        public string LaneId { get; set; }
        public string OriginBarracksId { get; set; }
        public int OpponentSlot { get; set; }
        public bool IsCenterLane { get; set; }
    }

    public sealed class MatchArenaLayout
    {
        public int PlayerCount { get; set; }
        public TopologyKind Topology { get; set; }
        public string TopologyId { get; set; }
        public float ArenaRadius { get; set; }
        public float MainToTowerDistance { get; set; }
        public IReadOnlyList<PlayerSlotLayout> Slots { get; set; }
        public IReadOnlyList<LaneConnection> Lanes { get; set; }
    }
}
