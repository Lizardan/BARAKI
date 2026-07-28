using System.Text;

namespace Game.Core
{
    /// <summary>Webhook URL XOR-embedded at CI/player build time (see DiscordWebhookEmbedded.Data.cs).</summary>
    internal static partial class DiscordWebhookEmbedded
    {
        public static bool TryGetUrl(out string webhookUrl)
        {
            webhookUrl = null;
            if (Payload == null || Payload.Length == 0 || Key == null || Key.Length == 0)
            {
                return false;
            }

            var decoded = DiscordWebhookStampRules.Xor(Payload, Key);
            var text = Encoding.UTF8.GetString(decoded);
            webhookUrl = DiscordWebhookStampRules.Normalize(text);
            return webhookUrl != null;
        }
    }
}
