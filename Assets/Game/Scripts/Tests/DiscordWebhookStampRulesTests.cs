using System;
using System.IO;
using System.Text;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class DiscordWebhookStampRulesTests
    {
        [Test]
        public void Normalize_AcceptsHttpsWebhookUrl()
        {
            var url = "https://discord.com/api/webhooks/1/abc";
            Assert.AreEqual(url, DiscordWebhookStampRules.Normalize("  " + url + " \n"));
        }

        [Test]
        public void Normalize_RejectsNonHttps()
        {
            Assert.IsNull(DiscordWebhookStampRules.Normalize("http://example.com"));
            Assert.IsNull(DiscordWebhookStampRules.Normalize(""));
            Assert.IsNull(DiscordWebhookStampRules.Normalize("not-a-url"));
        }

        [Test]
        public void Xor_RoundTripsUtf8Webhook()
        {
            var url = "https://discord.com/api/webhooks/1/abcDEF";
            var key = DiscordWebhookStampRules.CreateKey(16);
            var encoded = DiscordWebhookStampRules.Xor(Encoding.UTF8.GetBytes(url), key);
            var decoded = Encoding.UTF8.GetString(DiscordWebhookStampRules.Xor(encoded, key));
            Assert.AreEqual(url, decoded);
            Assert.AreNotEqual(Encoding.UTF8.GetBytes(url), encoded);
        }

        [Test]
        public void BuildEmbeddedDataSource_ContainsOnlyHexBytes_NotPlainUrl()
        {
            var url = "https://discord.com/api/webhooks/999/secretTokenValue";
            var source = DiscordWebhookStampRules.BuildEmbeddedDataSource(url, DiscordWebhookStampRules.CreateKey());
            StringAssert.Contains("Payload", source);
            StringAssert.Contains("0x", source);
            StringAssert.DoesNotContain(url, source);
            StringAssert.DoesNotContain("secretTokenValue", source);
        }

        [Test]
        public void WriteEmbeddedDataFile_Empty_WritesStub()
        {
            var root = Path.Combine(Path.GetTempPath(), "baraki-webhook-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets", "Game", "Scripts", "Runtime", "Core"));
            try
            {
                DiscordWebhookStampRules.WriteEmbeddedDataFile(root, webhookUrl: null);
                var path = Path.Combine(root, DiscordWebhookStampRules.EmbeddedDataRelativePath.Replace('/', Path.DirectorySeparatorChar));
                var text = File.ReadAllText(path);
                StringAssert.Contains("Array.Empty", text);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
