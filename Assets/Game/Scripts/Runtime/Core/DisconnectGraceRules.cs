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

        /// <summary>
        /// Listen-host may leave slot 0 after migration; compare against the current listen-host slot.
        /// </summary>
        public static bool IsHostSlotDisconnect(int slot, int listenHostSlot) =>
            slot >= 0 && slot == listenHostSlot;

        /// <summary>Legacy lobby host is slot 0 before the first migration.</summary>
        public static bool IsHostSlotDisconnect(int slot) =>
            IsHostSlotDisconnect(slot, NetworkLobbySlotRules.HostSlot);
    }
}
