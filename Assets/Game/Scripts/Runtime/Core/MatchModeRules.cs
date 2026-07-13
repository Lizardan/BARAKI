namespace Game.Core
{
    /// <summary>
    /// Which player-count modes are selectable in Match Entry (MVP: 2 and 4).
    /// </summary>
    public static class MatchModeRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 8;

        public static bool IsValidPlayerCount(int playerCount) =>
            playerCount is >= MinPlayers and <= MaxPlayers;

        /// <summary>MVP playable create modes (others shown greyed out).</summary>
        public static bool IsModeSelectable(int playerCount) =>
            playerCount is 2 or 4;

        public static string GetModeTitle(int playerCount) =>
            playerCount switch
            {
                2 => "ДУЭЛЬ",
                4 => "FFA 4",
                _ => $"FFA {playerCount}",
            };
    }
}
