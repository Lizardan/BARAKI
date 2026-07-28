namespace Game.Core
{
    /// <summary>Player-facing session flow shared by Friends Hub and playtest reports.</summary>
    public enum SessionFlowState
    {
        Offline = 0,
        Bootstrap = 1,
        MainMenu = 2,
        Connecting = 3,
        Lobby = 4,
        RacePick = 5,
        Match = 6,
    }

    public static class SessionFlowRules
    {
        public const string ElapsedLt5 = "lt5";
        public const float MatchElapsedBucketSeconds = 5f * 60f;

        public static string ToFlowId(SessionFlowState state) => state.ToString();

        public static bool TryParseFlowId(string flow, out SessionFlowState state)
        {
            if (string.IsNullOrWhiteSpace(flow))
            {
                state = SessionFlowState.Offline;
                return false;
            }

            return System.Enum.TryParse(flow.Trim(), ignoreCase: true, out state);
        }

        /// <summary>
        /// Network flags beat scene name. Connecting wins over MainMenu while transport is up but lobby is not.
        /// </summary>
        public static SessionFlowState Resolve(
            string activeScene,
            bool isNetworked,
            bool hasNetworkLobby,
            bool matchStarted,
            bool matchSimStarted)
        {
            if (isNetworked && !hasNetworkLobby)
            {
                return SessionFlowState.Connecting;
            }

            if (hasNetworkLobby && !matchStarted)
            {
                return SessionFlowState.Lobby;
            }

            if (matchStarted && !matchSimStarted)
            {
                return SessionFlowState.RacePick;
            }

            if (matchSimStarted)
            {
                return SessionFlowState.Match;
            }

            if (string.Equals(activeScene, GameSceneNames.Bootstrap, System.StringComparison.Ordinal))
            {
                return SessionFlowState.Bootstrap;
            }

            if (string.Equals(activeScene, GameSceneNames.MainMenu, System.StringComparison.Ordinal))
            {
                return SessionFlowState.MainMenu;
            }

            if (string.Equals(activeScene, GameSceneNames.Lobby, System.StringComparison.Ordinal))
            {
                return SessionFlowState.Lobby;
            }

            if (string.Equals(activeScene, GameSceneNames.Game, System.StringComparison.Ordinal))
            {
                return SessionFlowState.RacePick;
            }

            return SessionFlowState.Offline;
        }

        public static string ResolveElapsedBucket(float matchTimeSeconds)
        {
            if (matchTimeSeconds < MatchElapsedBucketSeconds)
            {
                return ElapsedLt5;
            }

            var minutes = (int)(matchTimeSeconds / 60f);
            var bucketMinutes = (minutes / 5) * 5;
            if (bucketMinutes < 5)
            {
                bucketMinutes = 5;
            }

            return bucketMinutes + "+";
        }

        public static string FormatElapsedBucketForUi(string elapsedBucket)
        {
            if (string.IsNullOrEmpty(elapsedBucket) || elapsedBucket == ElapsedLt5)
            {
                return "меньше 5 мин";
            }

            return elapsedBucket.EndsWith("+", System.StringComparison.Ordinal)
                ? elapsedBucket + " мин"
                : elapsedBucket + " мин";
        }
    }
}
