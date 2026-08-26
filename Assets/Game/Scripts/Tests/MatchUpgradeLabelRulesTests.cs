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

        [Test]
        public void FormatDivineBlessing_ShowsLevelGateWhenLocked()
        {
            Assert.AreEqual(
                "Благословение\nУр. 2",
                MatchUpgradeLabelRules.FormatDivineBlessingLockedButton(2));
            StringAssert.Contains(
                "Нужен ур. главного здания 2",
                MatchUpgradeLabelRules.FormatDivineBlessingTooltip(1000, 45f, mainLevel: 1));
            StringAssert.DoesNotContain(
                "Нужен ур. главного здания",
                MatchUpgradeLabelRules.FormatDivineBlessingTooltip(1000, 45f, mainLevel: 2));
        }

        [Test]
        public void TowerTracks_HaveUniqueTitlesAndRussianEffects()
        {
            var titles = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < TowerTrackRules.TrackCount; i++)
            {
                Assert.IsTrue(titles.Add(MatchUpgradeLabelRules.GetTowerTrackTitle(i)));
                Assert.IsNotEmpty(MatchUpgradeLabelRules.GetTowerTrackEffect(i));
            }
        }

        [Test]
        public void FormatTowerTrackButton_ShowsLevelAndCost()
        {
            Assert.AreEqual(
                "Flaming Arrows\nУр. 2\n800g",
                MatchUpgradeLabelRules.FormatTowerTrackButton(0, 2, 800));
            StringAssert.Contains(
                "поджигают",
                MatchUpgradeLabelRules.FormatTowerTrackTooltip(0, 1, 500, 45f));
            StringAssert.Contains(
                "500g · 45с",
                MatchUpgradeLabelRules.FormatTowerTrackTooltip(0, 1, 500, 45f));
        }
    }
}
