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

            // Real release JSON inserts a large "uploader" object between name and URL —
            // search forward from the matched name (not a tiny fixed window).
            const string urlKey = "\"browser_download_url\":\"";
            var urlKeyAlt = "\"browser_download_url\": \"";
            var urlIndex = releaseJson.IndexOf(urlKey, nameIndex, StringComparison.Ordinal);
            var keyLen = urlKey.Length;
            if (urlIndex < 0)
            {
                urlIndex = releaseJson.IndexOf(urlKeyAlt, nameIndex, StringComparison.Ordinal);
                keyLen = urlKeyAlt.Length;
            }

            if (urlIndex < 0)
            {
                return false;
            }

            var start = urlIndex + keyLen;
            var end = releaseJson.IndexOf('"', start);
            if (end <= start)
            {
                return false;
            }

            downloadUrl = releaseJson.Substring(start, end - start);
            return !string.IsNullOrWhiteSpace(downloadUrl);
        }

        /// <summary>GitHub Unicorn / 5xx pages often return HTML with HTTP 200.</summary>
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
