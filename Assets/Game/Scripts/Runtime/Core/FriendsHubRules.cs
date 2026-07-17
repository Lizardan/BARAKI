using System;
using System.Linq;

namespace Game.Core
{
    public static class FriendsHubRules
    {
        public const string StatusInLauncher = "InLauncher";
        public const string StatusInGame = "InGame";
        public const int UgsNameSuffixMinLength = 4;

        public static string NormalizePlayerId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static string NormalizeUgsPlayerName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static string SanitizeUgsNameBase(string value)
        {
            var normalized = NormalizeUgsPlayerName(value);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            var hashIndex = normalized.LastIndexOf('#');
            if (hashIndex > 0)
            {
                normalized = normalized[..hashIndex].Trim();
            }

            return normalized;
        }

        public static string GetUgsNameBase(string fullName)
        {
            var normalized = NormalizeUgsPlayerName(fullName);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            var hashIndex = normalized.LastIndexOf('#');
            return hashIndex > 0 ? normalized[..hashIndex] : normalized;
        }

        public static bool UgsNameBaseMatches(string fullUgsName, string displayName)
        {
            return string.Equals(
                GetUgsNameBase(fullUgsName),
                SanitizeUgsNameBase(displayName),
                StringComparison.Ordinal);
        }

        public static bool ShouldSyncDisplayNameToUgs(string fullUgsName, string displayName)
        {
            var baseName = SanitizeUgsNameBase(displayName);
            if (baseName.Length < 3 || string.Equals(baseName, "Player", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !UgsNameBaseMatches(fullUgsName, baseName);
        }

        public static bool IsValidPlayerId(string value)
        {
            var normalized = NormalizePlayerId(value);
            return normalized.Length >= 8;
        }

        public static bool IsValidUgsPlayerName(string value)
        {
            var normalized = NormalizeUgsPlayerName(value);
            var hashIndex = normalized.LastIndexOf('#');
            if (hashIndex <= 0 || hashIndex >= normalized.Length - 1)
            {
                return false;
            }

            var basePart = normalized[..hashIndex];
            var suffix = normalized[(hashIndex + 1)..];
            if (basePart.Length < 1)
            {
                return false;
            }

            return suffix.Length >= UgsNameSuffixMinLength
                   && suffix.All(c => char.IsDigit(c) || char.IsLetter(c));
        }

        public static string FormatShareablePlayerName(string fullUgsName)
        {
            var normalized = NormalizeUgsPlayerName(fullUgsName);
            return string.IsNullOrEmpty(normalized) ? "—" : normalized;
        }

        public static bool TrySplitUgsPlayerName(string fullUgsName, out string baseName, out string suffixWithHash)
        {
            baseName = string.Empty;
            suffixWithHash = string.Empty;
            var normalized = NormalizeUgsPlayerName(fullUgsName);
            var hashIndex = normalized.LastIndexOf('#');
            if (hashIndex <= 0 || hashIndex >= normalized.Length - 1)
            {
                return false;
            }

            baseName = normalized[..hashIndex];
            suffixWithHash = normalized[hashIndex..];
            return true;
        }

        public static string FormatProfileNameRichText(string displayName, string ugsFullName)
        {
            if (!TryGetProfileDisplayParts(displayName, ugsFullName, out var baseName, out var suffix))
            {
                var fallback = SanitizeUgsNameBase(displayName);
                return string.IsNullOrEmpty(fallback) ? "Игрок" : fallback;
            }

            return $"{baseName}<size=17><color=#A89878B8>{suffix}</color></size>";
        }

        public static bool TryGetProfileDisplayParts(
            string displayName,
            string ugsFullName,
            out string baseName,
            out string suffixWithHash)
        {
            baseName = SanitizeUgsNameBase(displayName);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Игрок";
            }

            suffixWithHash = string.Empty;
            if (!TrySplitUgsPlayerName(ugsFullName, out _, out suffixWithHash))
            {
                return false;
            }

            return true;
        }

        public static bool TryGetJoinableLobbyCode(string status, string lobbyCode, out string joinCode)
        {
            joinCode = string.Empty;
            if (!string.Equals(status, StatusInGame, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var normalized = NormalizeLobbyCode(lobbyCode);
            if (normalized.Length < 4)
            {
                return false;
            }

            joinCode = normalized;
            return true;
        }

        public static string FormatFriendLine(string name, string status, bool isOnline, string lobbyCode)
        {
            var displayName = string.IsNullOrWhiteSpace(name) ? "Игрок" : name.Trim();
            if (!isOnline)
            {
                return $"{displayName}: офлайн";
            }

            if (TryGetJoinableLobbyCode(status, lobbyCode, out var code))
            {
                return $"{displayName}: в лобби · {code}";
            }

            return status switch
            {
                StatusInLauncher => $"{displayName}: в меню",
                "Online" => $"{displayName}: онлайн",
                _ => $"{displayName}: {status}",
            };
        }

        public static string FormatIncomingRequestLine(string name, string playerId)
        {
            var displayName = string.IsNullOrWhiteSpace(name) ? ShortPlayerId(playerId) : name.Trim();
            return $"{displayName} ({ShortPlayerId(playerId)})";
        }

        public static string ShortPlayerId(string playerId)
        {
            var normalized = NormalizePlayerId(playerId);
            if (normalized.Length <= 8)
            {
                return normalized;
            }

            return normalized[..8] + "…";
        }

        public static string NormalizeLobbyCode(string lobbyCode) =>
            string.IsNullOrWhiteSpace(lobbyCode) ? string.Empty : lobbyCode.Trim().ToUpperInvariant();
    }
}
