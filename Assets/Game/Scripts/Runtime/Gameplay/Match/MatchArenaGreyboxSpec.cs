namespace Game.Gameplay.Match
{
    public readonly struct MatchArenaGreyboxSpec
    {
        public const int LegacyCenterRingSegments = 32;

        public MatchArenaGreyboxSpec(int playerCount, int buildingMarkerCount, int laneLineCount, int centerRingMarkerCount)
        {
            PlayerCount = playerCount;
            BuildingMarkerCount = buildingMarkerCount;
            LaneLineCount = laneLineCount;
            CenterRingMarkerCount = centerRingMarkerCount;
        }

        public int PlayerCount { get; }
        public int BuildingMarkerCount { get; }
        public int LaneLineCount { get; }
        public int CenterRingMarkerCount { get; }
    }
}
