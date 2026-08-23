namespace Game.Core
{
    public enum MenuChatChannel
    {
        Global = 0,
        FriendsFeed = 1,
    }

    /// <summary>Pure helpers for menu/match chat payload limits and channel ids.</summary>
    public static class GameChatRules
    {
        /// <summary>Cloudflare Worker origin. Override with PlayerPrefs key baraki.chat.apiBase.</summary>
        public const string DefaultApiBaseUrl = "";

        /// <summary>Optional shared key (must match Worker secret CHAT_API_KEY). PlayerPrefs baraki.chat.apiKey.</summary>
        public const string DefaultApiKey = "";

        public const string GlobalChannelName = "baraki-global";
        public const string FriendsFeedChannelPrefix = "friends-feed-";
        public const int MaxMessageLength = 280;
        public const float MatchMessageVisibleSeconds = 3.5f;
        public const float MatchMessageFadeSeconds = 1.25f;
        public const string GlobalTabLabel = "ОБЩИЙ";
        public const string FriendsFeedTabLabel = "СРЕДИ ДРУЗЕЙ";
        public const string WarmingStatusLabel = "Чат";
        public const string ChatUnavailableHint = "Чат недоступен. Проверьте соединение.";

        public static string FriendsFeedChannelName(string playerId)
        {
            var id = NormalizePlayerId(playerId);
            return string.IsNullOrEmpty(id) ? string.Empty : FriendsFeedChannelPrefix + id;
        }

        public static string NormalizePlayerId(string playerId) =>
            string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim();

        public static string SanitizeMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var trimmed = text.Trim();
            if (trimmed.Length <= MaxMessageLength)
            {
                return trimmed;
            }

            return trimmed[..MaxMessageLength];
        }

        public static bool TrySanitizeMessage(string text, out string sanitized)
        {
            sanitized = SanitizeMessage(text);
            return sanitized.Length > 0;
        }

        public static bool IsFriendsFeedChannel(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            return channelName.StartsWith(FriendsFeedChannelPrefix);
        }

        public static bool IsGlobalChannel(string channelName) =>
            string.Equals(channelName, GlobalChannelName, System.StringComparison.Ordinal);

        public static string FormatDisplayName(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick))
            {
                return "Игрок";
            }

            var trimmed = nick.Trim();
            var hash = trimmed.LastIndexOf('#');
            return hash > 0 ? trimmed[..hash] : trimmed;
        }

        public static string FormatMessageTime(System.DateTime utcOrLocal) =>
            utcOrLocal.ToLocalTime().ToString("HH:mm");

        public static float MatchMessageAlpha(float ageSeconds)
        {
            if (ageSeconds <= 0f)
            {
                return 1f;
            }

            var fadeStart = MatchMessageVisibleSeconds - MatchMessageFadeSeconds;
            if (ageSeconds <= fadeStart)
            {
                return 1f;
            }

            if (ageSeconds >= MatchMessageVisibleSeconds)
            {
                return 0f;
            }

            var t = (ageSeconds - fadeStart) / MatchMessageFadeSeconds;
            return 1f - t;
        }

        public static bool ShouldRemoveMatchMessage(float ageSeconds) =>
            ageSeconds >= MatchMessageVisibleSeconds;
    }
}
