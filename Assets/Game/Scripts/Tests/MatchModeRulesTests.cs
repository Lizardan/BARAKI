using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchModeRulesTests
    {
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, false)]
        public void IsModeSelectable_AllowsOpenedModes(int n, bool expected)
        {
            Assert.AreEqual(expected, MatchModeRules.IsModeSelectable(n));
        }

        [Test]
        public void IsValidPlayerCount_Range()
        {
            Assert.IsFalse(MatchModeRules.IsValidPlayerCount(1));
            Assert.IsTrue(MatchModeRules.IsValidPlayerCount(2));
            Assert.IsTrue(MatchModeRules.IsValidPlayerCount(5));
            Assert.IsFalse(MatchModeRules.IsValidPlayerCount(6));
            Assert.IsFalse(MatchModeRules.IsValidPlayerCount(8));
        }

        [Test]
        public void GetModeTitle_N2_IsOneVsOne()
        {
            Assert.AreEqual("1 vs 1", MatchModeRules.GetModeTitle(2));
        }

        [Test]
        public void GetModeTitle_N5_IsFfa5()
        {
            Assert.AreEqual("FFA 5", MatchModeRules.GetModeTitle(5));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void GetModeSummary_KnownModes_AreNonEmpty(int n)
        {
            var summary = MatchModeRules.GetModeSummary(n);
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary));
            Assert.AreNotEqual(MatchModeRules.GetModeSummary(n + 10), summary);
        }

        [Test]
        public void GetModeSummary_N2AndN4_DescribeDifferentMaps()
        {
            Assert.AreNotEqual(MatchModeRules.GetModeSummary(2), MatchModeRules.GetModeSummary(4));
            StringAssert.Contains("коридора", MatchModeRules.GetModeSummary(2));
            StringAssert.Contains("крестом", MatchModeRules.GetModeSummary(4));
        }

        [Test]
        public void ModeMapNote_StatesBalanceIsShared()
        {
            StringAssert.Contains("карта", MatchModeRules.ModeMapNote);
        }
    }
}
