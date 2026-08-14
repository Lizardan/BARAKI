using Game.Gameplay.Data;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchBarracksCallSlotRulesTests
    {
        [Test]
        public void TryGetCommandSlot_SiegeRow_OccupiesSlotsThreeToFive()
        {
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Siege, out var siege));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Flying, out var flying));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Super, out var super));

            Assert.AreEqual(3, siege);
            Assert.AreEqual(4, flying);
            Assert.AreEqual(5, super);
        }

        [Test]
        public void TryGetCommandSlot_MeleeRow_OccupiesSlotsSixToEight()
        {
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Melee, out var melee));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Ranged, out var ranged));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Caster, out var caster));

            Assert.AreEqual(6, melee);
            Assert.AreEqual(7, ranged);
            Assert.AreEqual(8, caster);
        }

        [Test]
        public void TryGetCommandSlot_Hero_ReturnsFalse()
        {
            Assert.IsFalse(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Hero, out _));
        }

        [Test]
        public void TryGetHeroDeploySlot_MapsSlotsNineThroughEleven()
        {
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetHeroDeploySlot(1, out var one));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetHeroDeploySlot(2, out var two));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetHeroDeploySlot(3, out var three));
            Assert.AreEqual(9, one);
            Assert.AreEqual(10, two);
            Assert.AreEqual(11, three);
            Assert.IsFalse(MatchBarracksCallSlotRules.TryGetHeroDeploySlot(0, out _));
            Assert.IsFalse(MatchBarracksCallSlotRules.TryGetHeroDeploySlot(4, out _));
        }

        [Test]
        public void TryGetTitanDeploySlot_UsesSlotTwo()
        {
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetTitanDeploySlot(out var titan));
            Assert.AreEqual(2, titan);
            Assert.AreEqual(0, MatchBarracksCallSlotRules.BarracksUpgradeSlot);
        }
    }
}
