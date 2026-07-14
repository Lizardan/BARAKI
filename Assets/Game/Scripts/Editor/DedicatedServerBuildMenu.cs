using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public static class DedicatedServerBuildMenu
    {
        private const string OutputPath = "Builds/WindowsServer/BARAKI.exe";

        // Example:
        // BARAKI.exe -batchmode -nographics -barakiServer -listenPort 7777 -players 4
        // Environment alternatives: BARAKI_SERVER=1, PORT=7777, PLAYER_COUNT=4.
        // Prefer -listenPort over -port (Unity Player may claim -port).
        [MenuItem("BARAKI/Build/Windows Dedicated Server (Headless)")]
        public static void BuildWindowsDedicatedServer()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Builds");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                extraScriptingDefines = new[] { "BARAKI_DEDICATED_SERVER" },
                options = BuildOptions.CleanBuildCache,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"Dedicated server built: {OutputPath} ({report.summary.totalSize} bytes).");
                return;
            }

            Debug.LogError(
                $"Dedicated server build failed: {report.summary.result}, "
                + $"{report.summary.totalErrors} errors.");
        }
    }
}
