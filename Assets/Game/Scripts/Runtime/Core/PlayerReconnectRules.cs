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

        /// <summary>
        /// Session token binds the slot to the owner's UGS PlayerId so a third party
        /// cannot squat a reserved slot with a guessed room:slot pair.
        /// Legacy two-part tokens (no player id) are still parseable.
        /// </summary>
        public static string BuildSessionToken(string matchId, int slot) =>
            BuildSessionToken(matchId, slot, string.Empty);

        public static string BuildSessionToken(string matchId, int slot, string playerId)
        {
            var baseToken = $"{matchId}:{slot}";
            return string.IsNullOrEmpty(playerId)
                ? baseToken
                : $"{baseToken}:{playerId}";
        }

        public static bool TryParseSessionToken(
            string token,
            out string matchId,
            out int slot) =>
            TryParseSessionToken(token, out matchId, out slot, out _);

        public static bool TryParseSessionToken(
            string token,
            out string matchId,
            out int slot,
            out string playerId)
        {
            matchId = string.Empty;
            slot = -1;
            playerId = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var parts = token.Split(':');
            if (parts.Length is not (2 or 3))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out slot) || slot < 0)
            {
                return false;
            }

            matchId = parts[0];
            if (parts.Length == 3)
            {
                playerId = parts[2];
            }

            return !string.IsNullOrWhiteSpace(matchId);
        }

        /// <summary>
        /// Claim a reserved slot only when the token's player id matches the slot owner.
        /// Slots without a recorded owner (stand-in / legacy) are still claimable.
        /// </summary>
        public static bool CanClaimSlot(string tokenPlayerId, string slotPlayerId)
        {
            if (string.IsNullOrEmpty(slotPlayerId))
            {
                return true;
            }

            return string.Equals(tokenPlayerId, slotPlayerId, System.StringComparison.Ordinal);
        }
    }
}
