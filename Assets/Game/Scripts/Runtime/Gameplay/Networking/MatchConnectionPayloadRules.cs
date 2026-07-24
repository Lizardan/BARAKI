using System;
using System.Text;

namespace Game.Gameplay.Networking
{
    public static class MatchConnectionPayloadRules
    {
        public const string ReconnectPrefix = "reconnect:";
        private const char Separator = '|';
        private const int MaxDisplayNameChars = 32;

        public static string BuildInitial(bool isHost, string displayName)
        {
            var role = isHost ? "Host" : "Guest";
            var normalizedName = NormalizeDisplayName(displayName);
            var encodedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedName));
            return $"{role}{Separator}{encodedName}";
        }

        public static string BuildReconnect(string sessionToken) =>
            ReconnectPrefix + (sessionToken ?? string.Empty);

        public static bool TryReadDisplayName(byte[] payload, out string displayName)
        {
            displayName = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            var text = Encoding.UTF8.GetString(payload);
            if (string.IsNullOrWhiteSpace(text) ||
                text.StartsWith(ReconnectPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var separatorIndex = text.IndexOf(Separator);
            if (separatorIndex < 0 || separatorIndex == text.Length - 1)
            {
                return false;
            }

            try
            {
                var encodedName = text[(separatorIndex + 1)..];
                var decodedName = Encoding.UTF8.GetString(Convert.FromBase64String(encodedName));
                displayName = NormalizeDisplayName(decodedName);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
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
