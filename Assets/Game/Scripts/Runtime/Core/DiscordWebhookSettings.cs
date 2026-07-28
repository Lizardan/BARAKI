using System;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(
        fileName = "DiscordWebhookSettings",
        menuName = "Game/Debug/Discord Webhook Settings")]
    public sealed class DiscordWebhookSettings : ScriptableObject
    {
        private const string ResourcesPath = "Debug/DiscordWebhookSettings";

        [SerializeField] private string _webhookUrl = string.Empty;

        public string WebhookUrl => _webhookUrl != null ? _webhookUrl.Trim() : string.Empty;

        public static DiscordWebhookSettings Load() =>
            Resources.Load<DiscordWebhookSettings>(ResourcesPath);

        /// <summary>
        /// Player builds: XOR-embedded bytes. Editor: optional Resources Settings asset.
        /// </summary>
        public static bool TryResolveWebhookUrl(out string webhookUrl, out string source)
        {
            if (DiscordWebhookEmbedded.TryGetUrl(out webhookUrl))
            {
                source = "Embedded";
                return true;
            }

            var settings = Load();
            if (settings != null && !string.IsNullOrWhiteSpace(settings.WebhookUrl))
            {
                webhookUrl = settings.WebhookUrl;
                source = "Resources";
                return true;
            }

            webhookUrl = string.Empty;
            source = settings == null ? "missing-embedded-and-resources" : "empty-url";
            return false;
        }
    }
}
