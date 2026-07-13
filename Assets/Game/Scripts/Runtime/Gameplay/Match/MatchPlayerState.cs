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
        public int MainLevel { get; set; } = MatchEconomyRules.DefaultMainLevel;
        public int PassiveGoldLevel { get; set; }
        public float PassiveGoldTickRemainingSeconds { get; set; } =
            MatchEconomyRules.PassiveGoldTickIntervalSeconds;
        public bool IsEliminated { get; set; }
    }
}
