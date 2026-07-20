#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Resolves Editor display version from git tags. Never writes PlayerSettings.
    /// </summary>
    public static class EditorLocalVersionResolver
    {
        static string s_cached;
        static bool s_resolved;

        public static string Resolve(string fallbackBundleVersion)
        {
            if (s_resolved)
            {
                return s_cached ?? fallbackBundleVersion;
            }

            s_resolved = true;
            var latestTag = TryReadLatestVersionTag(ProjectRoot);
            s_cached = EditorLocalVersionRules.Resolve(latestTag, fallbackBundleVersion);
            return s_cached;
        }

        /// <summary>Test / refresh hook.</summary>
        public static void ResetCache()
        {
            s_cached = null;
            s_resolved = false;
        }

        static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string TryReadLatestVersionTag(string projectRoot)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "tag -l \"v*\" --sort=-v:refname",
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                var stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);
                if (process.ExitCode != 0)
                {
                    return null;
                }

                using var reader = new StringReader(stdout);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0)
                    {
                        return line;
                    }
                }
            }
            catch (Exception)
            {
                // Git missing / not a repo — fall back to PlayerSettings.
            }

            return null;
        }
    }
}
#endif
