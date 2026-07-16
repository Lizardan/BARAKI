using System;

namespace Game.Core
{
    /// <summary>Pure rules for force-update gate against R2 version.json.</summary>
    public static class GameUpdateVersionRules
    {
        public static bool TryParseSemVer(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            return Version.TryParse(trimmed, out version);
        }

        public static bool IsUpdateRequired(string localVersion, string remoteMinVersion)
        {
            if (!TryParseSemVer(localVersion, out var local)
                || !TryParseSemVer(remoteMinVersion, out var min))
            {
                return false;
            }

            return local < min;
        }

        public static bool CanPlay(string localVersion, string remoteMinVersion) =>
            !IsUpdateRequired(localVersion, remoteMinVersion);
    }
}
