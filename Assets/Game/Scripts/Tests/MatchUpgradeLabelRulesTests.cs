using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchUpgradeLabelRulesTests
    {
        [Test]
        public void FormatHeroDeployButton_UsesSlotAndCost()
        {
            Assert.AreEqual("Герой 2\n1000g", MatchUpgradeLabelRules.FormatHeroDeployButton(2, 1000));
        }

        [Test]
        public void FormatHeroDeployCooldownButton_UsesCeilSeconds()
        {
            Assert.AreEqual("Герой 1\n287с", MatchUpgradeLabelRules.FormatHeroDeployCooldownButton(1, 287));
        }

        [Test]
        public void FormatHeroDeployTooltip_IncludesNameAndCost()
        {
            Assert.AreEqual(
                "Выпуск TT_King\n1000g · мгновенно",
                MatchUpgradeLabelRules.FormatHeroDeployTooltip("TT_King", 1000));
        }

        [Test]
        public void FormatTitanDeployCooldown_UsesSeconds()
        {
            Assert.AreEqual("Титан\n12с", MatchUpgradeLabelRules.FormatTitanDeployCooldownButton(12));
            Assert.AreEqual(
                "Титан — перезарядка казарм\n12с",
                MatchUpgradeLabelRules.FormatTitanDeployCooldownTooltip(12));
        }

        [Test]
        public void CeilRemainingSeconds_RoundsUp()
        {
            Assert.AreEqual(0, MatchUpgradeLabelRules.CeilRemainingSeconds(0f));
            Assert.AreEqual(0, MatchUpgradeLabelRules.CeilRemainingSeconds(-1f));
            Assert.AreEqual(1, MatchUpgradeLabelRules.CeilRemainingSeconds(0.01f));
            Assert.AreEqual(300, MatchUpgradeLabelRules.CeilRemainingSeconds(300f));
            Assert.AreEqual(300, MatchUpgradeLabelRules.CeilRemainingSeconds(299.1f));
        }
    }
}
