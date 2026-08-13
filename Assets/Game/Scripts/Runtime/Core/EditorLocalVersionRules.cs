namespace Game.Core
{
    /// <summary>
    /// Pure helpers for Editor-only local version display: latest git tag + patch bump
    /// (mirrors CI next-release resolution). Does not touch PlayerSettings.
    /// Editor bundleVersion is always one patch ahead of the latest GitHub release;
    /// a newer fallback wins so a manual series bump (0.1.* → 0.2.*) shows immediately.
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

        /// <summary>
        /// First GitHub tag of the series implied by Editor bundleVersion (GitHub + 1).
        /// <c>0.2.1</c> → <c>0.2.0</c>.
        /// </summary>
        public static bool TrySeriesStart(string editorBundleVersion, out string seriesStart)
        {
            seriesStart = null;
            var normalized = BuildVersionStampRules.Normalize(editorBundleVersion);
            if (normalized == null || !GameUpdateVersionRules.TryParseSemVer(normalized, out var version))
            {
                return false;
            }

            seriesStart = $"{version.Major}.{version.Minor}.0";
            return true;
        }

        public static string Resolve(string latestTagOrVersion, string fallbackBundleVersion)
        {
            var fallback = BuildVersionStampRules.Normalize(fallbackBundleVersion) ?? fallbackBundleVersion;
            if (!TryBumpPatch(latestTagOrVersion, out var next))
            {
                return fallback;
            }

            if (GameUpdateVersionRules.TryParseSemVer(next, out var nextVersion)
                && GameUpdateVersionRules.TryParseSemVer(fallback, out var fallbackVersion)
                && fallbackVersion > nextVersion)
            {
                return fallback;
            }

            return next;
        }
    }
}
