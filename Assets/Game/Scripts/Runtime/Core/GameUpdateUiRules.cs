using System;

namespace Game.Core
{
    /// <summary>Pure helpers for main-menu update strip copy.</summary>
    public static class GameUpdateUiRules
    {
        public static string FormatVersionLabel(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return "v0.0.0";
            }

            var trimmed = version.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return "v" + trimmed.Substring(1);
            }

            return "v" + trimmed;
        }

        public static int ProgressPercent(float progress01)
        {
            if (float.IsNaN(progress01) || progress01 <= 0f)
            {
                return 0;
            }

            if (progress01 >= 1f)
            {
                return 100;
            }

            return (int)(progress01 * 100f + 0.5f);
        }

        public static string FormatProgressLabel(float progress01) =>
            ProgressPercent(progress01) + "%";
    }
}
