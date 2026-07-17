using System;

namespace Game.Core
{
    /// <summary>
    /// Force-update channel via GitHub Release tags (no version.json / no API).
    /// Latest tag: follow redirects on /releases/latest → /releases/tag/vX.Y.Z
    /// Zip asset: BARAKI-vX.Y.Z.zip
    /// </summary>
    public static class GitHubReleaseUpdateRules
    {
        public const string DefaultOwner = "Lizardan";
        public const string DefaultRepo = "BARAKI";
        public const string ProductName = "BARAKI";

        public static string LatestReleasePageUrl(string owner = DefaultOwner, string repo = DefaultRepo) =>
            $"https://github.com/{owner}/{repo}/releases/latest";

        public static string ZipFileNameForTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            var trimmed = tag.Trim();
            return $"{ProductName}-{trimmed}.zip";
        }

        public static string ReleaseZipDownloadUrl(
            string tag,
            string owner = DefaultOwner,
            string repo = DefaultRepo)
        {
            var fileName = ZipFileNameForTag(tag);
            if (fileName == null)
            {
                return null;
            }

            return $"https://github.com/{owner}/{repo}/releases/download/{tag.Trim()}/{fileName}";
        }

        /// <summary>
        /// Parses tag from a GitHub release URL after redirects, e.g.
        /// https://github.com/Lizardan/BARAKI/releases/tag/v0.1.3
        /// </summary>
        public static bool TryParseTagFromReleaseUrl(string url, out string tag)
        {
            tag = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            const string marker = "/releases/tag/";
            var index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var start = index + marker.Length;
            if (start >= url.Length)
            {
                return false;
            }

            var end = start;
            while (end < url.Length)
            {
                var c = url[end];
                if (c == '/' || c == '?' || c == '#' || c == '&')
                {
                    break;
                }

                end++;
            }

            if (end <= start)
            {
                return false;
            }

            tag = url.Substring(start, end - start);
            return !string.IsNullOrWhiteSpace(tag);
        }

        public static bool LooksLikeHtmlErrorPage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            var trimmed = body.TrimStart();
            return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        }

        public static bool LooksLikeGitHubApiError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            return body.IndexOf("\"message\"", StringComparison.Ordinal) >= 0
                   && (body.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0
                       || body.IndexOf("API rate limit exceeded", StringComparison.OrdinalIgnoreCase) >= 0
                       || body.IndexOf("Not Found", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
