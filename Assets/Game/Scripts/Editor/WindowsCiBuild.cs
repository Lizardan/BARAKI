using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Headless Windows Standalone build for game-ci (GitHub Releases pipeline).</summary>
    public static class WindowsCiBuild
    {
        const string PlayerExecutableFileName = "BARAKI.exe";

        public static void Build()
        {
            // game-ci does not reliably forward custom env vars into the Unity process on Windows.
            // Prefer Tooling/BuildSupport/ci-version.txt written by the workflow, then env fallback.
            var projectRoot = Directory.GetCurrentDirectory();
            var version = BuildVersionStampRules.ResolveFromFileThenEnv(
                projectRoot,
                System.Environment.GetEnvironmentVariable("BARAKI_BUILD_VERSION"));

            if (!string.IsNullOrWhiteSpace(version))
            {
                PlayerSettings.bundleVersion = version;
            }

            PlayerSettings.productName = "BARAKI";
            WarnIfGitHubPlaytestMissing();

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            BuildWindowsPlayer(
                "WindowsCiBuild",
                scenes,
                "build/Windows",
                PlayerExecutableFileName,
                null,
                version);
        }

        private static void WarnIfGitHubPlaytestMissing()
        {
            if (GitHubPlaytestSettings.TryResolveCredentials(out _, out _, out var source) && source == "Embedded")
            {
                Debug.Log("WindowsCiBuild: GitHub playtest token embedded OK.");
                return;
            }

            if (GitHubPlaytestSettings.TryResolveCredentials(out _, out _, out source))
            {
                Debug.LogWarning(
                    $"WindowsCiBuild: GitHub playtest resolved via {source} (Editor/Resources). " +
                    "CI player builds should use XOR embed from Stamp-GitHubPlaytestEmbedded.ps1.");
                return;
            }

            Debug.LogWarning(
                "WindowsCiBuild: GitHub playtest token not embedded. " +
                "CI must run Tooling/BuildSupport/Stamp-GitHubPlaytestEmbedded.ps1 before Unity. " +
                "Playtest «Отправить лог» will report GitHub not configured.");
        }

        private static void BuildWindowsPlayer(
            string label,
            string[] scenes,
            string outDir,
            string exeName,
            string[] extraScriptingDefines,
            string stamp)
        {
            if (scenes.Length == 0)
            {
                Debug.LogError($"{label}: no scenes to build.");
                EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory(outDir);
            var exePath = Path.Combine(outDir, exeName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            if (extraScriptingDefines is { Length: > 0 })
            {
                options.extraScriptingDefines = extraScriptingDefines;
            }

            Debug.Log(
                $"{label}: version={PlayerSettings.bundleVersion} " +
                $"(stamp={(stamp ?? "none")}) → {exePath}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"{label}: result={summary.result} size={summary.totalSize}");

            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
                return;
            }

            var helperSrc = Path.Combine("Tooling", "BuildSupport", "ApplyUpdate.bat");
            var helperDst = Path.Combine(outDir, "ApplyUpdate.bat");
            if (File.Exists(helperSrc))
            {
                File.Copy(helperSrc, helperDst, overwrite: true);
            }

            EditorApplication.Exit(0);
        }
    }
}
