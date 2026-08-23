using Game.Gameplay.Match;

namespace Game.Gameplay.Networking
{
    /// <summary>Return-to-lobby / rematch helpers shared by HUD, authority, and host migration.</summary>
    public static class MatchRematchRules
    {
        /// <summary>
        /// Host migration only runs while a match is still in progress.
        /// <see cref="MatchPhase.End"/> (results screen) must not start a migration.
        /// </summary>
        public static bool IsMatchInProgressForHostMigration(MatchPhase phase) =>
            phase != MatchPhase.End;

        /// <summary>
        /// Networked rematch keeps NGO up and returns every peer to Lobby.
        /// Offline rematch may leave the match locally.
        /// </summary>
        public static bool ShouldReturnEveryoneToLobby(bool isNetworked) => isNetworked;
    }
}
