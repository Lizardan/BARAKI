using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class FriendsHubRulesTests
    {
        [Test]
        public void TryGetJoinableLobbyCode_InGameWithCode_ReturnsTrue()
        {
            Assert.IsTrue(
                FriendsHubRules.TryGetJoinableLobbyCode(FriendsHubRules.StatusInGame, "abcd", out var code));
            Assert.AreEqual("ABCD", code);
        }

        [Test]
        public void TryGetJoinableLobbyCode_InLauncher_ReturnsFalse()
        {
            Assert.IsFalse(
                FriendsHubRules.TryGetJoinableLobbyCode(FriendsHubRules.StatusInLauncher, "abcd", out _));
        }

        [Test]
        public void FormatFriendLine_InGame_ShowsLobbyCode()
        {
            var line = FriendsHubRules.FormatFriendLine("Alpha", FriendsHubRules.StatusInGame, true, "wxyz");

            Assert.That(line, Does.Contain("Alpha"));
            Assert.That(line, Does.Contain("WXYZ"));
        }

        [TestCase("", false)]
        [TestCase("short", false)]
        [TestCase("12345678", true)]
        public void IsValidPlayerId_RequiresMinimumLength(string value, bool expected)
        {
            Assert.AreEqual(expected, FriendsHubRules.IsValidPlayerId(value));
        }

        [TestCase("Alpha#1234", true)]
        [TestCase("Alpha", false)]
        [TestCase("Alpha#12", false)]
        [TestCase("#1234", false)]
        public void IsValidUgsPlayerName_RequiresSuffix(string value, bool expected)
        {
            Assert.AreEqual(expected, FriendsHubRules.IsValidUgsPlayerName(value));
        }

        [Test]
        public void SanitizeUgsNameBase_StripsSuffix()
        {
            Assert.AreEqual("Alpha", FriendsHubRules.SanitizeUgsNameBase("Alpha#1234"));
        }

        [Test]
        public void UgsNameBaseMatches_IgnoresSuffix()
        {
            Assert.IsTrue(FriendsHubRules.UgsNameBaseMatches("Alpha#4821", "Alpha"));
        }

        [Test]
        public void TrySplitUgsPlayerName_SplitsSuffix()
        {
            Assert.IsTrue(FriendsHubRules.TrySplitUgsPlayerName("Alpha#4821", out var baseName, out var suffix));
            Assert.AreEqual("Alpha", baseName);
            Assert.AreEqual("#4821", suffix);
        }

        [Test]
        public void FormatProfileNameRichText_AppendsSmallerSuffix()
        {
            var rich = FriendsHubRules.FormatProfileNameRichText("Lizardan", "Lizardan#4821");

            Assert.That(rich, Does.StartWith("Lizardan"));
            Assert.That(rich, Does.Contain("#4821"));
            Assert.That(rich, Does.Contain("<size=17>"));
        }

        [Test]
        public void TryGetProfileDisplayParts_UsesDisplayBaseAndUgsSuffix()
        {
            Assert.IsTrue(FriendsHubRules.TryGetProfileDisplayParts(
                "Alpha",
                "Alpha#4821",
                out var baseName,
                out var suffix));

            Assert.AreEqual("Alpha", baseName);
            Assert.AreEqual("#4821", suffix);
        }

        [TestCase("Player", false)]
        [TestCase("Alpha", true)]
        public void ShouldSyncDisplayNameToUgs_SkipsDefaultPlayer(string displayName, bool expected)
        {
            Assert.AreEqual(
                expected,
                FriendsHubRules.ShouldSyncDisplayNameToUgs("Other#1234", displayName));
        }
    }
}
