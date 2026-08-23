namespace Game.Core
{
        /// <summary>
        /// Which player-count modes are selectable on the main-menu ИГРА tab.
        /// </summary>
    public static class MatchModeRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 5;

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
                5 => "FFA 5",
                _ => $"FFA {playerCount}",
            };

        public const string ModeMapNote =
            "Нажмите край схемы — с этой стороны экрана будет ваша база.";

        public static string GetModeSummary(int playerCount) =>
            playerCount switch
            {
                2 => "Две базы напротив. Три коридора — фланг, центр, фланг — все ведут к одному сопернику.",
                3 => "Три базы треугольником. Фланги бьют соседей, центр сходится в арене.",
                4 => "Четыре базы крестом. Фланги на соседей, центр — к базе напротив через арену.",
                5 => "Пять баз по кругу. Фланги по кольцу, все центры встречаются в арене.",
                _ => "Базы на периметре. Три исходящие дороги у каждого игрока.",
            };
    }
}
