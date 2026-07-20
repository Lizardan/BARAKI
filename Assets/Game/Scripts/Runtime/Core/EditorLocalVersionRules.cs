namespace Game.Core
{
    /// <summary>
    /// Pure helpers for Editor-only local version display: latest git tag + patch bump
    /// (mirrors CI next-release resolution). Does not touch PlayerSettings.
    /// </summary>
    public static class EditorLocalVersionRules
    {
        public static bool TryBumpPatch(string versionOrTag, out string nextVersion)
        {
            nextVersion = null;
            var normalized = BuildVersionStampRules.Normalize(versionOrTag);
            if (normalized == null || !GameUpdateVersionRules.TryParseSemVer(normalized, out var version))
            {
                return false;
            }

            nextVersion = $"{version.Major}.{version.Minor}.{version.Build + 1}";
            return true;
        }

        public static string Resolve(string latestTagOrVersion, string fallbackBundleVersion)
        {
            if (TryBumpPatch(latestTagOrVersion, out var next))
            {
                return next;
            }

            return BuildVersionStampRules.Normalize(fallbackBundleVersion) ?? fallbackBundleVersion;
        }
    }
}
