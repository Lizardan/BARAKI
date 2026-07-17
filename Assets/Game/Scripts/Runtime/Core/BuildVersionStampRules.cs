using System;
using System.IO;

namespace Game.Core
{
    /// <summary>Pure helpers for stamping PlayerSettings.bundleVersion in CI.</summary>
    public static class BuildVersionStampRules
    {
        public const string CiVersionFileRelativePath = "BuildSupport/ci-version.txt";

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        public static string ResolveFromFileThenEnv(string projectRoot, string envValue)
        {
            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                var path = Path.Combine(projectRoot, CiVersionFileRelativePath);
                if (File.Exists(path))
                {
                    var fromFile = Normalize(File.ReadAllText(path));
                    if (fromFile != null)
                    {
                        return fromFile;
                    }
                }
            }

            return Normalize(envValue);
        }
    }
}
