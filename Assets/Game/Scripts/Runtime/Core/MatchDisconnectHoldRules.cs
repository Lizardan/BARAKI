namespace Game.Core
{
    /// <summary>
    /// Mid-match disconnect overlay: pause until kick or reconnect, then a short read delay.
    /// </summary>
    public static class MatchDisconnectHoldRules
    {
        public const float OverlayReadDelaySeconds = 1f;
        public const float LocalPersistIntervalSeconds = 15f;

        public enum OverlayPhase : byte
        {
            None = 0,
            Waiting = 1,
            Returned = 2,
            Kicked = 3,
            Migrating = 4,
        }

        public static bool ShouldPauseMatch(
            bool matchInProgress,
            int reservedSlotCount,
            OverlayPhase phase,
            bool migrationPaused) =>
            matchInProgress
            && (migrationPaused
                || reservedSlotCount > 0
                || phase is OverlayPhase.Waiting
                    or OverlayPhase.Returned
                    or OverlayPhase.Kicked
                    or OverlayPhase.Migrating);

        public static bool CanUnpause(
            bool matchInProgress,
            int reservedSlotCount,
            OverlayPhase phase,
            bool migrationPaused,
            bool readDelayElapsed) =>
            matchInProgress
            && !migrationPaused
            && reservedSlotCount <= 0
            && phase is OverlayPhase.None or OverlayPhase.Returned or OverlayPhase.Kicked
            && (phase == OverlayPhase.None || readDelayElapsed);

        public static OverlayPhase NextPhaseAfterDisconnect(bool isListenHost) =>
            isListenHost ? OverlayPhase.Migrating : OverlayPhase.Waiting;

        public static OverlayPhase NextPhaseAfterReconnect() => OverlayPhase.Returned;

        public static OverlayPhase NextPhaseAfterKick(bool requiresHostMigration) =>
            requiresHostMigration ? OverlayPhase.Migrating : OverlayPhase.Kicked;

        public static OverlayPhase NextPhaseAfterMigration(int remainingReservedSlots) =>
            remainingReservedSlots > 0 ? OverlayPhase.Waiting : OverlayPhase.Kicked;

        public static bool ShouldShowKickButton(OverlayPhase phase, int reservedSlotCount) =>
            reservedSlotCount > 0
            && phase is OverlayPhase.None or OverlayPhase.Waiting;

        public static string FormatWaiting(string displayName) =>
            $"{NormalizeName(displayName)} завис";

        public static string FormatReturned(string displayName) =>
            $"{NormalizeName(displayName)} вернулся";

        public static string FormatKicked(string displayName) =>
            $"{NormalizeName(displayName)} исключён";

        public static string FormatMigrating() => "Смена хоста…";

        public static string FormatStatus(OverlayPhase phase, string displayName) =>
            phase switch
            {
                OverlayPhase.Returned => FormatReturned(displayName),
                OverlayPhase.Kicked => FormatKicked(displayName),
                OverlayPhase.Migrating => FormatMigrating(),
                OverlayPhase.Waiting => FormatWaiting(displayName),
                _ => string.Empty,
            };

        static string NormalizeName(string displayName) =>
            string.IsNullOrWhiteSpace(displayName) ? "Игрок" : displayName.Trim();
    }
}
