using System;
using System.IO;
using System.Text;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GitHubPlaytestStampRulesTests
    {
        [Test]
        public void NormalizeToken_AcceptsGithubPrefixes()
        {
            Assert.AreEqual(
                "ghp_abcdefghijklmnopqrstuvwxyz12",
                GitHubPlaytestStampRules.NormalizeToken("  ghp_abcdefghijklmnopqrstuvwxyz12 \n"));
            Assert.IsNotNull(GitHubPlaytestStampRules.NormalizeToken(
                "github_pat_11AAAAAAA0123456789012345678901234567890"));
        }

        [Test]
        public void NormalizeToken_RejectsJunk()
        {
            Assert.IsNull(GitHubPlaytestStampRules.NormalizeToken(""));
            Assert.IsNull(GitHubPlaytestStampRules.NormalizeToken("not-a-token"));
            Assert.IsNull(GitHubPlaytestStampRules.NormalizeToken("http://x"));
        }

        [Test]
        public void Xor_RoundTripsUtf8Token()
        {
            var token = "ghp_abcdefghijklmnopqrstuvwxyz12";
            var key = GitHubPlaytestStampRules.CreateKey(16);
            var encoded = GitHubPlaytestStampRules.Xor(Encoding.UTF8.GetBytes(token), key);
            var decoded = Encoding.UTF8.GetString(GitHubPlaytestStampRules.Xor(encoded, key));
            Assert.AreEqual(token, decoded);
            Assert.AreNotEqual(Encoding.UTF8.GetBytes(token), encoded);
        }

        [Test]
        public void BuildEmbeddedDataSource_DoesNotContainPlainToken()
        {
            var token = "ghp_SecretTokenValueABCDEFGH123456";
            var source = GitHubPlaytestStampRules.BuildEmbeddedDataSource(
                token,
                GitHubPlaytestStampRules.CreateKey());
            StringAssert.Contains("Payload", source);
            StringAssert.DoesNotContain(token, source);
            StringAssert.DoesNotContain("SecretTokenValue", source);
        }

        [Test]
        public void WriteEmbeddedDataFile_Empty_WritesStub()
        {
            var root = Path.Combine(Path.GetTempPath(), "baraki-gh-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets", "Game", "Scripts", "Runtime", "Core"));
            try
            {
                GitHubPlaytestStampRules.WriteEmbeddedDataFile(root, token: null);
                var path = Path.Combine(
                    root,
                    GitHubPlaytestStampRules.EmbeddedDataRelativePath.Replace('/', Path.DirectorySeparatorChar));
                StringAssert.Contains("Array.Empty", File.ReadAllText(path));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
