using System;

namespace Game.Core
{
    /// <summary>Launcher/menu "return to match" window for a saved reconnect token.</summary>
    public static class PendingMatchReconnectRules
    {
        public static bool CanShowReturnButton(
            bool hasPending,
            long savedUtcTicks,
            long nowUtcTicks,
            float graceSeconds = PlayerReconnectRules.DefaultGraceSeconds) =>
            hasPending && !IsExpired(savedUtcTicks, nowUtcTicks, graceSeconds);

        public static bool IsExpired(
            long savedUtcTicks,
            long nowUtcTicks,
            float graceSeconds = PlayerReconnectRules.DefaultGraceSeconds)
        {
            if (savedUtcTicks <= 0L || nowUtcTicks < savedUtcTicks || graceSeconds < 0f)
            {
                return true;
            }

            var elapsedSeconds = (nowUtcTicks - savedUtcTicks) / (double)TimeSpan.TicksPerSecond;
            return elapsedSeconds > graceSeconds;
        }

        public static bool CanAttemptRejoin(
            bool hasPending,
            string roomCode,
            int slot,
            string sessionToken,
            long savedUtcTicks,
            long nowUtcTicks,
            float graceSeconds = PlayerReconnectRules.DefaultGraceSeconds)
        {
            if (!CanShowReturnButton(hasPending, savedUtcTicks, nowUtcTicks, graceSeconds))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(roomCode) || slot < 0 || string.IsNullOrWhiteSpace(sessionToken))
            {
                return false;
            }

            return PlayerReconnectRules.TryParseSessionToken(sessionToken, out _, out var tokenSlot, out _)
                   && tokenSlot == slot;
        }
    }
}
