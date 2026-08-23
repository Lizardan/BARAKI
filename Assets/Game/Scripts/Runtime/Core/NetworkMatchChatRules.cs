namespace Game.Core
{
    /// <summary>Server-side validation for in-match NGO chat.</summary>
    public static class NetworkMatchChatRules
    {
        public const int MaxMessageLength = GameChatRules.MaxMessageLength;
        public const float MinSendIntervalSeconds = 0.75f;

        public static bool TryNormalize(string raw, out string message)
        {
            message = GameChatRules.SanitizeMessage(raw);
            return message.Length > 0;
        }

        public static bool CanSend(float nowSeconds, float lastSendSeconds) =>
            nowSeconds - lastSendSeconds >= MinSendIntervalSeconds;
    }
}
