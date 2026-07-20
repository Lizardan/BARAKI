namespace Game.Core
{
    /// <summary>Lobby/match disconnect policy: reserve slot during grace, then eliminate.</summary>
    public static class DisconnectGraceRules
    {
        public const float GraceSeconds = PlayerReconnectRules.DefaultGraceSeconds;

        public static bool ShouldClearSlotImmediately(bool matchStarted) => !matchStarted;

        public static bool ShouldReserveSlot(bool matchStarted) => matchStarted;

        public static bool ShouldEliminateAfterGrace(
            float secondsSinceDisconnect,
            float graceSeconds = GraceSeconds) =>
            secondsSinceDisconnect >= 0f && secondsSinceDisconnect >= graceSeconds;

        public static bool IsHostSlotDisconnect(int slot) =>
            NetworkLobbySlotRules.IsHostSlot(slot);
    }
}
