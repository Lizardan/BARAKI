using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchModeRulesTests
    {
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(8, false)]
        public void IsModeSelectable_AllowsOpenedModes(int n, bool expected)
        {
            Assert.AreEqual(expected, MatchModeRules.IsModeSelectable(n));
        }

        [Test]
        public void IsValidPlayerCount_Range()
        {
            Assert.IsFalse(MatchModeRules.IsValidPlayerCount(1));
            Assert.IsTrue(MatchModeRules.IsValidPlayerCount(2));
            Assert.IsTrue(MatchModeRules.IsValidPlayerCount(8));
            Assert.IsFalse(MatchModeRules.IsValidPlayerCount(9));
        }
    }
}
