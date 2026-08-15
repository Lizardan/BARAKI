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
        public void TryGetMainLevelUpgrade_ReturnsCostAndTime()
        {
            Assert.IsTrue(MatchEconomyRules.TryGetMainLevelUpgrade(1, out var cost, out var time));
            Assert.AreEqual(2000, cost);
            Assert.AreEqual(120f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetMainLevelUpgrade(2, out cost, out time));
            Assert.AreEqual(3000, cost);
            Assert.AreEqual(180f, time);

            Assert.IsFalse(MatchEconomyRules.TryGetMainLevelUpgrade(3, out _, out _));
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

        [Test]
        public void GetStatLevelCap_FollowsMainLevel()
        {
            Assert.AreEqual(3, MatchEconomyRules.GetStatLevelCap(1));
            Assert.AreEqual(6, MatchEconomyRules.GetStatLevelCap(2));
            Assert.AreEqual(9, MatchEconomyRules.GetStatLevelCap(3));
            Assert.AreEqual(9, MatchEconomyRules.GetStatLevelCap(4));
        }

        [Test]
        public void CanPurchaseStatTrack_RespectsMainLevelCap()
        {
            Assert.IsTrue(MatchEconomyRules.CanPurchaseStatTrack(GameIds.Upgrades.MeleeDamage, currentLevel: 2, mainLevel: 1));
            Assert.IsFalse(MatchEconomyRules.CanPurchaseStatTrack(GameIds.Upgrades.MeleeDamage, currentLevel: 3, mainLevel: 1));
            Assert.IsTrue(MatchEconomyRules.CanPurchaseStatTrack(GameIds.Upgrades.Armor, currentLevel: 5, mainLevel: 2));
            Assert.IsFalse(MatchEconomyRules.CanPurchaseStatTrack(GameIds.Upgrades.Armor, currentLevel: 9, mainLevel: 3));
        }

        [Test]
        public void TryGetStatTrackUpgrade_ReturnsGddEconomy()
        {
            Assert.IsTrue(MatchEconomyRules.TryGetStatTrackUpgrade(GameIds.Upgrades.MeleeDamage, 0, out var cost, out var time));
            Assert.AreEqual(75, cost);
            Assert.AreEqual(8f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetStatTrackUpgrade(GameIds.Upgrades.MeleeDamage, 8, out cost, out time));
            Assert.AreEqual(275, cost);
            Assert.AreEqual(24f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetStatTrackUpgrade(GameIds.Upgrades.RangedDamage, 8, out cost, out time));
            Assert.AreEqual(275, cost);
            Assert.AreEqual(24f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetStatTrackUpgrade(GameIds.Upgrades.Armor, 0, out cost, out time));
            Assert.AreEqual(60, cost);
            Assert.AreEqual(6f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetStatTrackUpgrade(GameIds.Upgrades.Armor, 8, out cost, out time));
            Assert.AreEqual(220, cost);
            Assert.AreEqual(22f, time);

            Assert.IsFalse(MatchEconomyRules.TryGetStatTrackUpgrade(GameIds.Upgrades.MeleeDamage, 9, out _, out _));
        }

        [Test]
        public void CanPurchaseMagic_RespectsMainLevel()
        {
            Assert.IsTrue(MatchEconomyRules.CanPurchaseMagic(0, 1));
            Assert.IsFalse(MatchEconomyRules.CanPurchaseMagic(1, 1));
            Assert.IsTrue(MatchEconomyRules.CanPurchaseMagic(1, 2));
            Assert.IsFalse(MatchEconomyRules.CanPurchaseMagic(2, 2));
            Assert.IsFalse(MatchEconomyRules.CanPurchaseMagic(3, 3));
        }

        [Test]
        public void TryGetMagicUpgrade_ReturnsGddEconomy()
        {
            Assert.IsTrue(MatchEconomyRules.TryGetMagicUpgrade(0, out var cost, out var time));
            Assert.AreEqual(500, cost);
            Assert.AreEqual(60f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetMagicUpgrade(1, out cost, out time));
            Assert.AreEqual(750, cost);
            Assert.AreEqual(90f, time);

            Assert.IsTrue(MatchEconomyRules.TryGetMagicUpgrade(2, out cost, out time));
            Assert.AreEqual(1000, cost);
            Assert.AreEqual(135f, time);

            Assert.IsFalse(MatchEconomyRules.TryGetMagicUpgrade(3, out _, out _));
        }

        [Test]
        public void DivineBlessing_EconomyAndGate()
        {
            Assert.AreEqual(1000, MatchEconomyRules.DivineBlessingCost);
            Assert.AreEqual(45f, MatchEconomyRules.DivineBlessingSeconds);
            Assert.IsFalse(MatchEconomyRules.CanPurchaseDivineBlessing(1, alreadyComplete: false));
            Assert.IsTrue(MatchEconomyRules.CanPurchaseDivineBlessing(2, alreadyComplete: false));
            Assert.IsFalse(MatchEconomyRules.CanPurchaseDivineBlessing(2, alreadyComplete: true));
            Assert.IsTrue(MatchEconomyRules.TryGetDivineBlessingUpgrade(2, false, out var cost, out var time));
            Assert.AreEqual(1000, cost);
            Assert.AreEqual(45f, time);
        }
    }
}
