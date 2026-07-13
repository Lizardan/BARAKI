using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Headless WebGL build entry for game-ci (customBuildMethod).</summary>
    public static class WebGLCiBuild
    {
        public static void Build()
        {
            // Reduce CI crash surface (audio thread abort on Linux headless).
            PlayerSettings.muteOtherAudioSources = true;

            PlayerSettings.SetManagedStrippingLevel(
                NamedBuildTarget.WebGL,
                ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("WebGLCiBuild: no enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            // Last path segment becomes WebGL file prefix on some Unity/game-ci layouts.
            // Keep it BARAKI so artifacts match activity-shell config.js / boot.js.
            PlayerSettings.productName = "BARAKI";

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "build/BARAKI",
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            Debug.Log($"WebGLCiBuild: building {scenes.Length} scenes → {options.locationPathName}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"WebGLCiBuild: result={summary.result} size={summary.totalSize} errors={summary.totalErrors}");

            if (summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type is LogType.Error or LogType.Exception)
                        {
                            Debug.LogError($"[{step.name}] {message.content}");
                        }
                    }
                }

                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }
    }
}
