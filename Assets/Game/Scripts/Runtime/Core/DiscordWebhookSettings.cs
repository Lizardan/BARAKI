using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(
        fileName = "DiscordWebhookSettings",
        menuName = "Game/Debug/Discord Webhook Settings")]
    public sealed class DiscordWebhookSettings : ScriptableObject
    {
        [SerializeField] private string _webhookUrl = string.Empty;

        public string WebhookUrl => _webhookUrl != null ? _webhookUrl.Trim() : string.Empty;

        public static DiscordWebhookSettings Load() =>
            Resources.Load<DiscordWebhookSettings>("Debug/DiscordWebhookSettings");
    }
}
