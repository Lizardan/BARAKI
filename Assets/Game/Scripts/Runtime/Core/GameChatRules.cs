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
        public const string DefaultApiBaseUrl = "https://baraki-chat.lizard268.workers.dev";

        /// <summary>Optional shared key (must match Worker secret CHAT_API_KEY). PlayerPrefs baraki.chat.apiKey.</summary>
        public const string DefaultApiKey = "b95b8df8bb6d03634ee88c6feffda2523ad1f1e3fb5a38c2b145c6670bf64ae7";

        public const string GlobalChannelName = "baraki-global";
        public const string FriendsFeedChannelPrefix = "friends-feed-";
        public const int MaxMessageLength = 280;
        public const int MaxChannelHistory = 80;
        public const int MaxDirectHistory = 50;
        public const int HistoryRetentionHours = 36;
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
            return trimmed.Length <= MaxMessageLength ? trimmed : trimmed[..MaxMessageLength];
        }

        /// <summary>
        /// Minimal JSON string escaping. Escapes quotes, backslash and ALL control
        /// characters below 0x20 (as \u00XX) — otherwise a crafted message produces
        /// invalid JSON on the Worker / receiving clients.
        /// </summary>
        public static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var sb = new System.Text.StringBuilder(value.Length + 8);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
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

        public static bool IsWithinRetention(System.DateTime receivedAt)
        {
            var local = receivedAt.Kind == System.DateTimeKind.Utc
                ? receivedAt.ToLocalTime()
                : receivedAt;
            return (System.DateTime.Now - local).TotalHours <= HistoryRetentionHours;
        }

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

        /// <summary>
        /// UI Toolkit + Input System often deliver Enter as character \n with keyCode None.
        /// </summary>
        public static bool IsComposerSubmit(UnityEngine.KeyCode keyCode, char character) =>
            keyCode is UnityEngine.KeyCode.Return or UnityEngine.KeyCode.KeypadEnter
            || character is '\n' or '\r';
    }

    /// <summary>WebSocket chat transport rules: reconnect policy, outbox limits, wire frames.</summary>
    public static class GameChatSocketRules
    {
        public const float MaxReconnectDelaySeconds = 30f;
        /// <summary>Pending sends buffered while the socket is down; oldest are dropped beyond this.</summary>
        public const int MaxOutboxMessages = 20;
        public const float HeartbeatIntervalSeconds = 30f;
        /// <summary>No inbound traffic for this long → force reconnect.</summary>
        public const float DeadConnectionSeconds = 75f;

        /// <summary>1, 2, 4, … capped at <see cref="MaxReconnectDelaySeconds"/>.</summary>
        public static float NextReconnectDelaySeconds(int attempt)
        {
            var exponent = attempt < 0 ? 0 : attempt;
            if (exponent > 16)
            {
                exponent = 16; // pow overflow guard; result is capped anyway
            }

            return System.Math.Min(MaxReconnectDelaySeconds, System.MathF.Pow(2f, exponent));
        }

        public static int OutboxCountAfterEnqueue(int currentCount) =>
            System.Math.Min(currentCount + 1, MaxOutboxMessages);

        public static string BuildSendFrame(string channel, string text, string peerId = null) =>
            peerId is { Length: > 0 }
                ? $"{{\"type\":\"send\",\"channel\":\"{channel}\",\"text\":{GameChatRules.JsonEscape(text)},\"peerId\":\"{peerId}\"}}"
                : $"{{\"type\":\"send\",\"channel\":\"{channel}\",\"text\":{GameChatRules.JsonEscape(text)}}}";

        public static string BuildSyncFrame(string globalAfter, string friendsAfter, string dmAfterJson)
        {
            var dm = string.IsNullOrEmpty(dmAfterJson) || dmAfterJson == "{}" ? "{}" : dmAfterJson;
            return $"{{\"type\":\"sync\",\"globalAfter\":\"{globalAfter ?? string.Empty}\","
                   + $"\"friendsAfter\":\"{friendsAfter ?? string.Empty}\",\"dm\":{dm}}}";
        }

        public enum FrameKind
        {
            Unknown,
            Message,
            Ack,
            Error,
        }

        [System.Serializable]
        public class WireMessage
        {
            public string id;
            public string ts;
            public string playerId;
            public string displayName;
            public string text;
            public string channel;
            public string peerId;
        }

        [System.Serializable]
        public class WireFrame
        {
            public string type;
            public WireMessage message;
            public string code;
        }

        /// <summary>Parse a server frame; unknown/broken payloads yield Unknown.</summary>
        public static (FrameKind Kind, WireFrame Frame) ParseServerFrame(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "pong")
            {
                return (FrameKind.Unknown, null);
            }

            WireFrame frame;
            try
            {
                frame = UnityEngine.JsonUtility.FromJson<WireFrame>(json);
            }
            catch (System.ArgumentException)
            {
                return (FrameKind.Unknown, null);
            }

            if (frame?.type == null)
            {
                return (FrameKind.Unknown, null);
            }

            return frame.type switch
            {
                // JsonUtility instantiates nested class fields even when absent from JSON,
                // so an empty message must be rejected by its required payload (id).
                "msg" when !string.IsNullOrEmpty(frame.message?.id) => (FrameKind.Message, frame),
                "ack" when !string.IsNullOrEmpty(frame.message?.id) => (FrameKind.Ack, frame),
                "error" => (FrameKind.Error, frame),
                _ => (FrameKind.Unknown, frame),
            };
        }
    }
}
