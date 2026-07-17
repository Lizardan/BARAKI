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
        public static void Build()
        {
            // game-ci does not reliably forward custom env vars into the Unity process on Windows.
            // Prefer BuildSupport/ci-version.txt written by the workflow, then env fallback.
            var projectRoot = Directory.GetCurrentDirectory();
            var version = BuildVersionStampRules.ResolveFromFileThenEnv(
                projectRoot,
                System.Environment.GetEnvironmentVariable("BARAKI_BUILD_VERSION"));

            if (!string.IsNullOrWhiteSpace(version))
            {
                PlayerSettings.bundleVersion = version;
            }

            PlayerSettings.productName = "BARAKI";

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("WindowsCiBuild: no enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            const string outDir = "build/Windows";
            Directory.CreateDirectory(outDir);
            var exePath = Path.Combine(outDir, "BARAKI.exe");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            Debug.Log(
                $"WindowsCiBuild: version={PlayerSettings.bundleVersion} " +
                $"(stamp={(version ?? "none")}) → {exePath}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"WindowsCiBuild: result={summary.result} size={summary.totalSize}");

            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
                return;
            }

            var helperSrc = Path.Combine("BuildSupport", "ApplyUpdate.bat");
            var helperDst = Path.Combine(outDir, "ApplyUpdate.bat");
            if (File.Exists(helperSrc))
            {
                File.Copy(helperSrc, helperDst, overwrite: true);
            }

            EditorApplication.Exit(0);
        }
    }
}
