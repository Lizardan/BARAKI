using System;
using System.IO;

namespace Game.Core
{
    /// <summary>Path safety and byte progress helpers for zip extract into staging.</summary>
    public static class GameUpdateZipExtractRules
    {
        public static float ProgressForBytes(long done, long total)
        {
            if (total <= 0L || done <= 0L)
            {
                return 0f;
            }

            if (done >= total)
            {
                return 1f;
            }

            return (float)done / total;
        }

        public static bool TryResolveSafeExtractPath(
            string destinationRoot,
            string entryFullName,
            out string fullPath,
            out string error)
        {
            fullPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(destinationRoot))
            {
                error = "Destination root is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entryFullName))
            {
                error = "Zip entry name is empty.";
                return false;
            }

            var rootFull = Path.GetFullPath(destinationRoot);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !rootFull.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                rootFull += Path.DirectorySeparatorChar;
            }

            var combined = Path.GetFullPath(Path.Combine(rootFull, entryFullName));
            if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                error = "Zip entry escapes destination root: " + entryFullName;
                return false;
            }

            fullPath = combined;
            return true;
        }
    }
}
