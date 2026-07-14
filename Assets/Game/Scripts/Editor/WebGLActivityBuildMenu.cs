using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Local WebGL build into activity-shell (Discord Pages layout).</summary>
    public static class WebGLActivityBuildMenu
    {
        private const string StagingDir = "Builds/WebGLActivity";
        private const string OutputDir = "web/activity-shell/Build";

        [MenuItem("BARAKI/Build/WebGL → Activity Shell")]
        public static void BuildWebGlToActivityShell()
        {
            // Previous Dedicated Server build can leave Multiplayer Role = Server.
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL);
            }

            PlayerSettings.SetManagedStrippingLevel(
                NamedBuildTarget.WebGL,
                ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.productName = "BARAKI";

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Directory.CreateDirectory(StagingDir);
            var stagingPlayer = Path.Combine(StagingDir, "BARAKI");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = stagingPlayer,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"WebGL activity build failed: {report.summary.result}, "
                    + $"{report.summary.totalErrors} errors.");
                return;
            }

            CopyStagingToActivityShell(stagingPlayer);
            Debug.Log(
                $"WebGL activity build OK → {OutputDir} ({report.summary.totalSize} bytes). "
                + "Deploy: infra/scripts/deploy-pages.ps1");
        }

        private static void CopyStagingToActivityShell(string stagingPlayerDir)
        {
            Directory.CreateDirectory(OutputDir);
            var buildFolder = Path.Combine(stagingPlayerDir, "Build");
            if (!Directory.Exists(buildFolder))
            {
                buildFolder = stagingPlayerDir;
            }

            foreach (var file in Directory.GetFiles(buildFolder))
            {
                var name = Path.GetFileName(file);
                // Unity prefixes files with the last path segment (BARAKI.*).
                var destName = name.StartsWith("BARAKI.")
                    ? name
                    : name.Replace("Build.", "BARAKI.");
                File.Copy(file, Path.Combine(OutputDir, destName), overwrite: true);
            }
        }
    }
}
