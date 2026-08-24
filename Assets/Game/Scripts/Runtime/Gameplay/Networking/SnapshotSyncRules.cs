namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Snapshot wire version handshake. The host publishes its codec version into the
    /// lobby state; peers compare before the match can start. Mixed builds are blocked
    /// in the lobby instead of failing mid-match with unreadable snapshots.
    /// </summary>
    public static class SnapshotVersionGate
    {
        /// <summary>Lobby version before the host initializes it (legacy lobby objects).</summary>
        public const int Unknown = 0;

        public static bool IsCompatible(int localVersion, int lobbyVersion) =>
            lobbyVersion == Unknown || localVersion == lobbyVersion;

        /// <summary>Server-side final gate inside TryStart.</summary>
        public static bool CanStartMatch(int lobbyVersion) =>
            lobbyVersion == MatchSnapshotCodec.CurrentVersion;
    }

    /// <summary>
    /// Policy for checksum-mismatch reports coming back from clients.
    /// First mismatches trigger an immediate full re-publish (transient corruption);
    /// persistent reporters are kicked instead of freezing silently mid-match.
    /// </summary>
    public static class SnapshotDesyncRules
    {
        public const int ResyncAfterReports = 3;
        public const int KickAfterReports = 8;
        public const float WindowSeconds = 60f;

        public static bool ShouldResync(int reportCount) =>
            reportCount >= ResyncAfterReports && reportCount < KickAfterReports;

        public static bool ShouldKick(int reportCount) => reportCount >= KickAfterReports;

        /// <summary>Sliding-window counter: reports older than <see cref="WindowSeconds"/> reset the streak.</summary>
        public static int NextReportCount(int previousCount, float secondsSincePreviousReport)
        {
            if (previousCount <= 0 || secondsSincePreviousReport > WindowSeconds)
            {
                return 1;
            }

            return previousCount + 1;
        }
    }
}
