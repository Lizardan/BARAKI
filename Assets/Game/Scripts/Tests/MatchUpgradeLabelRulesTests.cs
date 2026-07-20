using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchUpgradeLabelRulesTests
    {
        [Test]
        public void GetNextLevel_IncludesQueuedCount()
        {
            Assert.AreEqual(1, MatchUpgradeLabelRules.GetNextLevel(0, 0));
            Assert.AreEqual(2, MatchUpgradeLabelRules.GetNextLevel(0, 1));
            Assert.AreEqual(3, MatchUpgradeLabelRules.GetNextLevel(1, 1));
        }

        [Test]
        public void FormatPassiveGoldButton_IncludesLevelAndCost()
        {
            var label = MatchUpgradeLabelRules.FormatPassiveGoldButton(2, 200);
            StringAssert.Contains("Ур. 2", label);
            StringAssert.Contains("200g", label);
        }

        [Test]
        public void FormatQueueSlotShort_KnownUpgrades()
        {
            Assert.AreEqual("PG 3", MatchUpgradeLabelRules.FormatQueueSlotShort(
                Game.Core.GameIds.Upgrades.MainPassiveGold, 3));
            Assert.AreEqual("Бар 2", MatchUpgradeLabelRules.FormatQueueSlotShort(
                Game.Core.GameIds.Upgrades.BarracksLevel, 2));
            Assert.AreEqual("Герой 1", MatchUpgradeLabelRules.FormatQueueSlotShort(
                HeroRules.BuildHireUpgradeId(1), 1));
        }
    }
}
