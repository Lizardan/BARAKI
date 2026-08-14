using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UnitGreyboxVisualsTests
    {
        [Test]
        public void GetChampionVisualScale_HeroAndTitanAreLargerThanCreep()
        {
            Assert.AreEqual(1.15f, UnitGreyboxVisuals.GetChampionVisualScale(UnitRole.Hero), 0.001f);
            Assert.AreEqual(1.5f, UnitGreyboxVisuals.GetChampionVisualScale(UnitRole.Titan), 0.001f);
            Assert.AreEqual(1f, UnitGreyboxVisuals.GetChampionVisualScale(UnitRole.Melee), 0.001f);
            Assert.AreEqual(1f, UnitGreyboxVisuals.GetChampionVisualScale(UnitRole.Ranged), 0.001f);
            Assert.AreEqual(1f, UnitGreyboxVisuals.GetChampionVisualScale(UnitRole.Super), 0.001f);
        }

        [Test]
        public void ChampionVsCreepConstants_MatchGddRequest()
        {
            Assert.AreEqual(1.15f, UnitGreyboxVisuals.HeroVsCreepScale, 0.001f);
            Assert.AreEqual(1.5f, UnitGreyboxVisuals.TitanVsCreepScale, 0.001f);
        }
    }
}
