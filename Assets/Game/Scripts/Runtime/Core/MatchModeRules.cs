namespace Game.Core
{
    /// <summary>
    /// Which player-count modes are selectable in Match Entry.
    /// </summary>
    public static class MatchModeRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 8;

        public static bool IsValidPlayerCount(int playerCount) =>
            playerCount is >= MinPlayers and <= MaxPlayers;

        /// <summary>Playable create modes (others shown greyed out).</summary>
        public static bool IsModeSelectable(int playerCount) =>
            playerCount is 2 or 3 or 4;

        public static string GetModeTitle(int playerCount) =>
            playerCount switch
            {
                2 => "1 vs 1",
                3 => "FFA 3",
                4 => "FFA 4",
                _ => $"FFA {playerCount}",
            };
    }
}
