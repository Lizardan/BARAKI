using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchEconomyRulesTests
    {
        [Test]
        public void TrySpendGold_RejectsInsufficientAndNegativeSpend()
        {
            Assert.IsFalse(MatchEconomyRules.TrySpendGold(100, 200, out _));
            Assert.IsFalse(MatchEconomyRules.TrySpendGold(100, -1, out _));
            Assert.IsTrue(MatchEconomyRules.TrySpendGold(250, 200, out var remaining));
            Assert.AreEqual(50, remaining);
        }

        [Test]
        public void TryGetBarracksLevelUpgrade_ReturnsCostAndTime()
        {
            Assert.IsTrue(MatchEconomyRules.TryGetBarracksLevelUpgrade(1, out var cost, out var time));
            Assert.AreEqual(1000, cost);
            Assert.AreEqual(3f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetBarracksLevelUpgrade(3, out cost, out time));
            Assert.AreEqual(2500, cost);
            Assert.AreEqual(3f, time);

            Assert.IsFalse(MatchEconomyRules.TryGetBarracksLevelUpgrade(4, out _, out _));
        }

        [Test]
        public void PassiveGold_CapAndTickFollowGdd()
        {
            Assert.AreEqual(3, MatchEconomyRules.GetPassiveGoldCap(1));
            Assert.AreEqual(9, MatchEconomyRules.GetPassiveGoldCap(3));
            Assert.AreEqual(0, MatchEconomyRules.GetPassiveGoldPerTick(0));
            Assert.AreEqual(25, MatchEconomyRules.GetPassiveGoldPerTick(1));
            Assert.AreEqual(225, MatchEconomyRules.GetPassiveGoldPerTick(9));
            Assert.AreEqual(200, MatchEconomyRules.PassiveGoldUpgradeCost);
            Assert.AreEqual(25f, MatchEconomyRules.PassiveGoldUpgradeSeconds);
            Assert.AreEqual(30f, MatchEconomyRules.PassiveGoldTickIntervalSeconds);
        }

        [Test]
        public void CanPurchasePassiveGold_RespectsCap()
        {
            Assert.IsTrue(MatchEconomyRules.CanPurchasePassiveGold(currentLevel: 2, mainLevel: 1));
            Assert.IsFalse(MatchEconomyRules.CanPurchasePassiveGold(currentLevel: 3, mainLevel: 1));
            Assert.IsFalse(MatchEconomyRules.CanPurchasePassiveGold(currentLevel: 9, mainLevel: 3));
        }
    }
}
