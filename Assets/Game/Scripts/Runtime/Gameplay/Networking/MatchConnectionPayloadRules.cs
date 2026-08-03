using System;
using System.Text;

namespace Game.Gameplay.Networking
{
    public static class MatchConnectionPayloadRules
    {
        public const string ReconnectPrefix = "reconnect:";
        private const char Separator = '|';
        private const int MaxDisplayNameChars = 32;

        public static string BuildInitial(bool isHost, string displayName) =>
            BuildInitial(isHost, displayName, string.Empty);

        /// <summary>
        /// Format: <c>Role|base64(displayName)|base64(playerId)</c>. Player id binds a
        /// reconnect token to its owner and is empty for legacy/stand-in connections.
        /// </summary>
        public static string BuildInitial(bool isHost, string displayName, string playerId)
        {
            var role = isHost ? "Host" : "Guest";
            var normalizedName = NormalizeDisplayName(displayName);
            var encodedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedName));
            var encodedPlayerId = string.IsNullOrEmpty(playerId)
                ? string.Empty
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(playerId));
            return $"{role}{Separator}{encodedName}{Separator}{encodedPlayerId}";
        }

        public static string BuildReconnect(string sessionToken) =>
            ReconnectPrefix + (sessionToken ?? string.Empty);

        public static bool TryReadDisplayName(byte[] payload, out string displayName)
        {
            displayName = string.Empty;
            var parts = SplitPayload(payload);
            if (parts == null)
            {
                return false;
            }

            var encodedName = parts.Length >= 2 ? parts[1] : null;
            if (string.IsNullOrEmpty(encodedName))
            {
                return false;
            }

            try
            {
                var decodedName = Encoding.UTF8.GetString(Convert.FromBase64String(encodedName));
                displayName = NormalizeDisplayName(decodedName);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool TryReadPlayerId(byte[] payload, out string playerId)
        {
            playerId = string.Empty;
            var parts = SplitPayload(payload);
            if (parts == null || parts.Length < 3 || string.IsNullOrEmpty(parts[2]))
            {
                return false;
            }

            try
            {
                playerId = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static string[] SplitPayload(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return null;
            }

            var text = Encoding.UTF8.GetString(payload);
            if (string.IsNullOrWhiteSpace(text)
                || text.StartsWith(ReconnectPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var separatorIndex = text.IndexOf(Separator);
            if (separatorIndex < 0 || separatorIndex == text.Length - 1)
            {
                return null;
            }

            return text.Split(Separator);
        }

        public static string NormalizeDisplayName(string displayName)
        {
            var normalized = string.IsNullOrWhiteSpace(displayName)
                ? "Player"
                : displayName.Trim();

            return normalized.Length <= MaxDisplayNameChars
                ? normalized
                : normalized[..MaxDisplayNameChars];
        }
    }
}
