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
        public void FormatFriendLine_InGame_OmitsLobbyCode()
        {
            var line = FriendsHubRules.FormatFriendLine("Alpha", FriendsHubRules.StatusInGame, true, "wxyz");

            Assert.AreEqual("Alpha: в лобби", line);
            Assert.That(line, Does.Not.Contain("WXYZ"));
        }

        [Test]
        public void FormatFriendLine_InGame_ShowsOccupiedSlots()
        {
            var line = FriendsHubRules.FormatFriendLine(
                "Alpha",
                FriendsHubRules.StatusInGame,
                true,
                "wxyz",
                occupiedSlots: 2,
                maxSlots: 4);

            Assert.AreEqual("Alpha: в лобби · 2/4", line);
            Assert.That(line, Does.Not.Contain("WXYZ"));
        }

        [TestCase(2, 4, "2/4")]
        [TestCase(4, 4, "4/4")]
        [TestCase(0, 0, "")]
        public void FormatLobbySlots_FormatsFill(int occupied, int max, string expected)
        {
            Assert.AreEqual(expected, FriendsHubRules.FormatLobbySlots(occupied, max));
        }

        [Test]
        public void CanJoinFriendLobby_False_WhenFull()
        {
            Assert.IsFalse(
                FriendsHubRules.CanJoinFriendLobby(
                    FriendsHubRules.StatusInGame,
                    "abcd",
                    occupiedSlots: 4,
                    maxSlots: 4,
                    out _));
        }

        [Test]
        public void CanJoinFriendLobby_True_WhenHasFreeSlot()
        {
            Assert.IsTrue(
                FriendsHubRules.CanJoinFriendLobby(
                    FriendsHubRules.StatusInGame,
                    "abcd",
                    occupiedSlots: 2,
                    maxSlots: 4,
                    out var code));
            Assert.AreEqual("ABCD", code);
        }

        [Test]
        public void IsLobbyFull_RequiresKnownCapacity()
        {
            Assert.IsFalse(FriendsHubRules.IsLobbyFull(4, 0));
            Assert.IsTrue(FriendsHubRules.IsLobbyFull(4, 4));
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

        [Test]
        public void FormatInvitesTabLabel_WithoutPending_ReturnsBase()
        {
            Assert.AreEqual(FriendsHubRules.InvitesTabLabel, FriendsHubRules.FormatInvitesTabLabel(0));
            Assert.AreEqual(FriendsHubRules.InvitesTabLabel, FriendsHubRules.FormatInvitesTabLabel(-1));
        }

        [Test]
        public void FormatInvitesTabLabel_WithPending_AppendsCount()
        {
            Assert.AreEqual("ПРИГЛАШЕНИЯ (2)", FriendsHubRules.FormatInvitesTabLabel(2));
        }

        [Test]
        public void IsInvitesTab_MatchesEnum()
        {
            Assert.IsFalse(FriendsHubRules.IsInvitesTab(FriendsHubTab.Friends));
            Assert.IsTrue(FriendsHubRules.IsInvitesTab(FriendsHubTab.Invites));
        }

        [Test]
        public void FriendRequestActionGlyphs_AreCheckAndBallotX()
        {
            Assert.AreEqual("✓", FriendsHubRules.AcceptRequestGlyph);
            Assert.AreEqual("✕", FriendsHubRules.DeclineRequestGlyph);
        }
    }
}
