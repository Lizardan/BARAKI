using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GitHubReleaseUpdateRulesTests
    {
        const string SampleReleaseJson =
            "{" +
            "\"tag_name\":\"v0.3.1\"," +
            "\"assets\":[" +
            "{" +
            "\"name\":\"version.json\"," +
            "\"browser_download_url\":\"https://github.com/Lizardan/BARAKI/releases/download/v0.3.1/version.json\"" +
            "}," +
            "{" +
            "\"name\":\"baraki-windows-v0.3.1.zip\"," +
            "\"browser_download_url\":\"https://github.com/Lizardan/BARAKI/releases/download/v0.3.1/baraki-windows-v0.3.1.zip\"" +
            "}" +
            "]" +
            "}";

        [Test]
        public void TryGetTagName_ReadsTag()
        {
            Assert.IsTrue(GitHubReleaseUpdateRules.TryGetTagName(SampleReleaseJson, out var tag));
            Assert.AreEqual("v0.3.1", tag);
        }

        [Test]
        public void TryGetAssetDownloadUrl_FindsManifest()
        {
            Assert.IsTrue(GitHubReleaseUpdateRules.TryGetAssetDownloadUrl(
                SampleReleaseJson,
                GitHubReleaseUpdateRules.ManifestAssetName,
                out var url));
            StringAssert.Contains("version.json", url);
        }

        [Test]
        public void TryGetAssetDownloadUrl_FindsManifest_WhenUploaderBlockIsLarge()
        {
            // Real GitHub assets put a huge "uploader" object between name and browser_download_url.
            var padding = new string('x', 2000);
            var releaseJson =
                "{" +
                "\"tag_name\":\"v0.1.1\"," +
                "\"assets\":[{" +
                "\"name\":\"version.json\"," +
                "\"uploader\":{\"login\":\"" + padding + "\"}," +
                "\"browser_download_url\":\"https://github.com/Lizardan/BARAKI/releases/download/v0.1.1/version.json\"" +
                "}]}";

            Assert.IsTrue(GitHubReleaseUpdateRules.TryGetAssetDownloadUrl(
                releaseJson,
                GitHubReleaseUpdateRules.ManifestAssetName,
                out var url));
            StringAssert.EndsWith("/version.json", url);
        }

        [Test]
        public void LooksLikeHtmlErrorPage_DetectsUnicorn()
        {
            Assert.IsTrue(GitHubReleaseUpdateRules.LooksLikeHtmlErrorPage(
                "<!DOCTYPE html><title>Unicorn! · GitHub</title>"));
            Assert.IsFalse(GitHubReleaseUpdateRules.LooksLikeHtmlErrorPage(
                "{\"tag_name\":\"v0.1.1\"}"));
        }

        [Test]
        public void LatestReleaseApiUrl_UsesDefaultRepo()
        {
            Assert.AreEqual(
                "https://api.github.com/repos/Lizardan/BARAKI/releases/latest",
                GitHubReleaseUpdateRules.LatestReleaseApiUrl());
        }
    }
}
