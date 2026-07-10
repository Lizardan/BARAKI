namespace Game.Gameplay.Match
{
    public readonly struct BarracksWaveFired
    {
        public BarracksWaveFired(
            int ownerSlot,
            string barracksId,
            string laneId,
            string ownerRaceId,
            int squadLevel,
            string squadId)
        {
            OwnerSlot = ownerSlot;
            BarracksId = barracksId;
            LaneId = laneId;
            OwnerRaceId = ownerRaceId;
            SquadLevel = squadLevel;
            SquadId = squadId;
        }

        public int OwnerSlot { get; }
        public string BarracksId { get; }
        public string LaneId { get; }
        public string OwnerRaceId { get; }
        public int SquadLevel { get; }
        public string SquadId { get; }
    }
}
