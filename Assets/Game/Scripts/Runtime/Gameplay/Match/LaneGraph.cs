using System.Collections.Generic;

namespace Game.Gameplay.Match
{
    public sealed class LaneSpline
    {
        public int OwnerSlot { get; set; }
        public string LaneId { get; set; }
        public string OriginBarracksId { get; set; }
        public int OpponentSlot { get; set; }
        public bool IsCenterLane { get; set; }
        public LanePath Path { get; set; }
    }

    public sealed class LaneGraph
    {
        readonly Dictionary<(int ownerSlot, string laneId), LaneSpline> _lanesByKey = new();

        public string TopologyId { get; set; }
        public int PlayerCount { get; set; }
        public float CenterArenaRadius { get; set; }
        public IReadOnlyList<LaneSpline> Lanes { get; set; }

        public bool TryGetLane(int ownerSlot, string laneId, out LaneSpline lane)
        {
            return _lanesByKey.TryGetValue((ownerSlot, laneId), out lane);
        }

        internal void Register(LaneSpline lane)
        {
            _lanesByKey[(lane.OwnerSlot, lane.LaneId)] = lane;
        }
    }
}
