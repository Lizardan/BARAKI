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
        static bool s_resolvedFromGit;

        public static string Resolve(string fallbackBundleVersion)
        {
            if (s_resolvedFromGit && !string.IsNullOrEmpty(s_cached))
            {
                return s_cached;
            }

            var latestTag = TryReadLatestVersionTag(ProjectRoot);
            var resolved = EditorLocalVersionRules.Resolve(latestTag, fallbackBundleVersion);

            // Cache only successful git-based bumps so a transient git failure can retry.
            if (!string.IsNullOrWhiteSpace(latestTag)
                && EditorLocalVersionRules.TryBumpPatch(latestTag, out _))
            {
                s_cached = resolved;
                s_resolvedFromGit = true;
            }

            return resolved;
        }

        /// <summary>Test / refresh hook.</summary>
        public static void ResetCache()
        {
            s_cached = null;
            s_resolvedFromGit = false;
        }

        static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string TryReadLatestVersionTag(string projectRoot)
        {
            var fromDescribe = RunGit(projectRoot, "describe --tags --abbrev=0");
            if (!string.IsNullOrWhiteSpace(fromDescribe)
                && fromDescribe.Trim().StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return fromDescribe.Trim();
            }

            var fromList = RunGit(projectRoot, "tag -l v* --sort=-v:refname");
            if (string.IsNullOrWhiteSpace(fromList))
            {
                return null;
            }

            using var reader = new StringReader(fromList);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return null;
        }

        static string RunGit(string projectRoot, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
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
                if (!process.WaitForExit(5000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    return null;
                }

                return process.ExitCode == 0 ? stdout : null;
            }
            catch (Exception)
            {
                // Git missing / not a repo — fall back to PlayerSettings.
                return null;
            }
        }
    }
}
#endif
