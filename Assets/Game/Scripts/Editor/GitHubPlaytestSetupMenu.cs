using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class GitHubPlaytestSetupMenu
    {
        private const string AssetPath = "Assets/Game/Resources/Debug/GitHubPlaytestSettings.asset";

        [MenuItem("Game/Debug/Open GitHub Playtest Settings")]
        public static void OpenSettings()
        {
            EnsureFolders();
            var settings = AssetDatabase.LoadAssetAtPath<GitHubPlaytestSettings>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GitHubPlaytestSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"GitHubPlaytestSetup: created {AssetPath}. Insert fine-grained PAT (Issues: write).");
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Game/Debug/Validate GitHub Playtest")]
        public static void Validate()
        {
            if (GitHubPlaytestSettings.TryResolveCredentials(out var token, out var repo, out var source))
            {
                Debug.Log(
                    $"GitHub playtest OK via {source}, repo={repo}, tokenLen={token.Length}, " +
                    $"prefix={token.Substring(0, Mathf.Min(10, token.Length))}...");
                return;
            }

            Debug.LogError(
                $"GitHub playtest NOT configured ({source}). " +
                "Editor: Open GitHub Playtest Settings. " +
                "CI: secret PLAYTEST_LOGS_TOKEN + Stamp-GitHubPlaytestEmbedded.ps1. " +
                "Create label playtest-log on the repo.");
        }

        [MenuItem("Game/Debug/Stamp GitHub Playtest Embed From Settings")]
        public static void StampEmbedFromSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GitHubPlaytestSettings>(AssetPath);
            var token = settings != null ? settings.PersonalAccessToken : string.Empty;
            if (GitHubPlaytestStampRules.NormalizeToken(token) == null)
            {
                Debug.LogError("GitHubPlaytestSetup: Settings token invalid. Need ghp_ / github_pat_ …");
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            GitHubPlaytestStampRules.WriteEmbeddedDataFile(projectRoot, token);
            AssetDatabase.Refresh();
            Debug.Log($"GitHubPlaytestSetup: stamped XOR embed → {GitHubPlaytestStampRules.EmbeddedDataRelativePath}");
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
