using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class DiscordWebhookSetupMenu
    {
        private const string AssetPath = "Assets/Game/Resources/Debug/DiscordWebhookSettings.asset";

        [MenuItem("Game/Debug/Open Discord Webhook Settings")]
        public static void OpenSettings()
        {
            EnsureFolders();
            var settings = AssetDatabase.LoadAssetAtPath<DiscordWebhookSettings>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DiscordWebhookSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"DiscordWebhookSetup: created {AssetPath}. Insert webhook URL in Inspector.");
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Game/Debug/Validate Discord Webhook")]
        public static void Validate()
        {
            if (DiscordWebhookSettings.TryResolveWebhookUrl(out var url, out var source))
            {
                Debug.Log($"Discord webhook OK via {source}, length={url.Length}, prefix={url.Substring(0, Mathf.Min(40, url.Length))}...");
                return;
            }

            Debug.LogError(
                $"Discord webhook NOT configured ({source}). " +
                $"Editor: Open Discord Webhook Settings. " +
                $"CI: secret DISCORD_PLAYTEST_WEBHOOK_URL + Stamp-DiscordWebhookEmbedded.ps1");
        }

        [MenuItem("Game/Debug/Stamp Discord Webhook Embed From Settings")]
        public static void StampEmbedFromSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DiscordWebhookSettings>(AssetPath);
            var url = settings != null ? settings.WebhookUrl : string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogError("DiscordWebhookSetup: Settings URL is empty. Open settings and paste webhook first.");
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            DiscordWebhookStampRules.WriteEmbeddedDataFile(projectRoot, url);
            AssetDatabase.Refresh();
            Debug.Log($"DiscordWebhookSetup: stamped XOR embed → {DiscordWebhookStampRules.EmbeddedDataRelativePath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/Game", "Resources");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/Debug"))
            {
                AssetDatabase.CreateFolder("Assets/Game/Resources", "Debug");
            }
        }
    }
}
