using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GitHubReleaseUpdateRulesTests
    {
        [Test]
        public void TryParseTagFromReleaseUrl_ReadsTag()
        {
            Assert.IsTrue(GitHubReleaseUpdateRules.TryParseTagFromReleaseUrl(
                "https://github.com/Lizardan/BARAKI/releases/tag/v0.1.3",
                out var tag));
            Assert.AreEqual("v0.1.3", tag);
        }

        [Test]
        public void TryParseTagFromReleaseUrl_IgnoresQueryAndFragment()
        {
            Assert.IsTrue(GitHubReleaseUpdateRules.TryParseTagFromReleaseUrl(
                "https://github.com/Lizardan/BARAKI/releases/tag/v1.2.3?foo=1#bar",
                out var tag));
            Assert.AreEqual("v1.2.3", tag);
        }

        [Test]
        public void TryParseTagFromReleaseUrl_RejectsNonTagUrls()
        {
            Assert.IsFalse(GitHubReleaseUpdateRules.TryParseTagFromReleaseUrl(
                "https://github.com/Lizardan/BARAKI/releases/latest",
                out _));
        }

        [Test]
        public void ZipFileNameForTag_UsesProductAndTag()
        {
            Assert.AreEqual("BARAKI-v0.1.3.zip", GitHubReleaseUpdateRules.ZipFileNameForTag("v0.1.3"));
        }

        [Test]
        public void ReleaseZipDownloadUrl_MatchesConvention()
        {
            Assert.AreEqual(
                "https://github.com/Lizardan/BARAKI/releases/download/v0.1.3/BARAKI-v0.1.3.zip",
                GitHubReleaseUpdateRules.ReleaseZipDownloadUrl("v0.1.3"));
        }

        [Test]
        public void LatestReleasePageUrl_UsesDefaultRepo()
        {
            Assert.AreEqual(
                "https://github.com/Lizardan/BARAKI/releases/latest",
                GitHubReleaseUpdateRules.LatestReleasePageUrl());
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
        public void LooksLikeGitHubApiError_DetectsRateLimit()
        {
            Assert.IsTrue(GitHubReleaseUpdateRules.LooksLikeGitHubApiError(
                "{\"message\":\"API rate limit exceeded for 1.2.3.4.\"}"));
            Assert.IsFalse(GitHubReleaseUpdateRules.LooksLikeGitHubApiError(
                "{\"version\":\"0.1.3\",\"minVersion\":\"0.1.3\"}"));
        }
    }
}
