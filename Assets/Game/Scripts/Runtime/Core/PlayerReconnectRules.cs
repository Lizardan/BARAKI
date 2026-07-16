namespace Game.Core
{
    /// <summary>Post-MVP reconnect into an active match within disconnect grace.</summary>
    public static class PlayerReconnectRules
    {
        public const float DefaultGraceSeconds = 90f;

        public static bool CanReconnect(
            bool matchInProgress,
            bool slotReserved,
            float secondsSinceDisconnect,
            float graceSeconds = DefaultGraceSeconds) =>
            matchInProgress
            && slotReserved
            && secondsSinceDisconnect >= 0f
            && secondsSinceDisconnect <= graceSeconds;

        public static string BuildSessionToken(string matchId, int slot) =>
            $"{matchId}:{slot}";

        public static bool TryParseSessionToken(string token, out string matchId, out int slot)
        {
            matchId = string.Empty;
            slot = -1;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var parts = token.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out slot) || slot < 0)
            {
                return false;
            }

            matchId = parts[0];
            return !string.IsNullOrWhiteSpace(matchId);
        }
    }
}
