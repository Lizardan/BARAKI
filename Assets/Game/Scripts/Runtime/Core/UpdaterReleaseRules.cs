using System;

namespace Game.Core
{
    /// <summary>Constants and helpers for the isolated updater release channel.</summary>
    public static class UpdaterReleaseRules
    {
        public const string BranchName = "release/baraki-updater";
        public const string TagPrefix = "updater-v";
        public const string InstallerFileName = "BARAKI-Setup.exe";
        public const string InstalledExecutableFileName = "BARAKI.exe";
        public const string UpdaterBuildVersion = "0.0.0";

        public static string TagForVersion(string versionOrTag)
        {
            if (string.IsNullOrWhiteSpace(versionOrTag))
            {
                return null;
            }

            var trimmed = versionOrTag.Trim();
            if (IsUpdaterTag(trimmed))
            {
                return trimmed;
            }

            var normalized = BuildVersionStampRules.Normalize(trimmed);
            return normalized == null ? null : TagPrefix + normalized;
        }

        public static bool IsUpdaterTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var trimmed = tag.Trim();
            if (!trimmed.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var versionPart = trimmed.Substring("updater-".Length);
            return GameUpdateVersionRules.TryParseSemVer(versionPart, out _);
        }
    }
}
