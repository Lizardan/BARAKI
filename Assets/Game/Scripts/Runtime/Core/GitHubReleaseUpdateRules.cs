using System;

namespace Game.Core
{
    /// <summary>Pure helpers for GitHub Releases force-update channel (no Cloudflare R2).</summary>
    public static class GitHubReleaseUpdateRules
    {
        public const string DefaultOwner = "Lizardan";
        public const string DefaultRepo = "BARAKI";
        public const string ManifestAssetName = "version.json";
        public const string ZipAssetPrefix = "baraki-windows-";

        public static string LatestReleaseApiUrl(string owner = DefaultOwner, string repo = DefaultRepo) =>
            $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        public static string ReleaseDownloadUrl(
            string tag,
            string assetFileName,
            string owner = DefaultOwner,
            string repo = DefaultRepo) =>
            $"https://github.com/{owner}/{repo}/releases/download/{tag}/{assetFileName}";

        public static bool TryGetAssetDownloadUrl(
            string releaseJson,
            string assetFileName,
            out string downloadUrl)
        {
            downloadUrl = null;
            if (string.IsNullOrWhiteSpace(releaseJson) || string.IsNullOrWhiteSpace(assetFileName))
            {
                return false;
            }

            // Minimal parse without Json.NET: look for "name":"<asset>" then nearby browser_download_url.
            var nameToken = "\"name\":\"" + assetFileName + "\"";
            var nameIndex = releaseJson.IndexOf(nameToken, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                // Assets order may put url before name — search loose.
                nameToken = "\"name\": \"" + assetFileName + "\"";
                nameIndex = releaseJson.IndexOf(nameToken, StringComparison.Ordinal);
            }

            if (nameIndex < 0)
            {
                return false;
            }

            // Search backward and forward in a window for browser_download_url.
            var windowStart = Math.Max(0, nameIndex - 800);
            var windowLength = Math.Min(1600, releaseJson.Length - windowStart);
            var window = releaseJson.Substring(windowStart, windowLength);
            const string urlKey = "\"browser_download_url\":\"";
            var urlKeyAlt = "\"browser_download_url\": \"";
            var urlIndex = window.IndexOf(urlKey, StringComparison.Ordinal);
            var keyLen = urlKey.Length;
            if (urlIndex < 0)
            {
                urlIndex = window.IndexOf(urlKeyAlt, StringComparison.Ordinal);
                keyLen = urlKeyAlt.Length;
            }

            if (urlIndex < 0)
            {
                return false;
            }

            var start = urlIndex + keyLen;
            var end = window.IndexOf('"', start);
            if (end <= start)
            {
                return false;
            }

            downloadUrl = window.Substring(start, end - start);
            return !string.IsNullOrWhiteSpace(downloadUrl);
        }

        public static bool TryGetTagName(string releaseJson, out string tagName)
        {
            tagName = null;
            if (string.IsNullOrWhiteSpace(releaseJson))
            {
                return false;
            }

            const string key = "\"tag_name\":\"";
            var keyAlt = "\"tag_name\": \"";
            var index = releaseJson.IndexOf(key, StringComparison.Ordinal);
            var keyLen = key.Length;
            if (index < 0)
            {
                index = releaseJson.IndexOf(keyAlt, StringComparison.Ordinal);
                keyLen = keyAlt.Length;
            }

            if (index < 0)
            {
                return false;
            }

            var start = index + keyLen;
            var end = releaseJson.IndexOf('"', start);
            if (end <= start)
            {
                return false;
            }

            tagName = releaseJson.Substring(start, end - start);
            return !string.IsNullOrWhiteSpace(tagName);
        }
    }
}
