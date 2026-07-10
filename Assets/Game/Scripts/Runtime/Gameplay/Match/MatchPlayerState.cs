namespace Game.Gameplay.Match
{
    public sealed class MatchPlayerState
    {
        public MatchPlayerState(int slotIndex, string raceId, int startingGold)
        {
            SlotIndex = slotIndex;
            RaceId = raceId;
            Gold = startingGold;
        }

        public int SlotIndex { get; }
        public string RaceId { get; }
        public int Gold { get; set; }
        public bool IsEliminated { get; set; }
    }
}
