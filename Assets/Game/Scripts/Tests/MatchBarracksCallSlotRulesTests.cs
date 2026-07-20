using Game.Gameplay.Data;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchBarracksCallSlotRulesTests
    {
        [Test]
        public void TryGetCommandSlot_FirstThreeRoles_OccupyLastRow()
        {
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Melee, out var melee));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Ranged, out var ranged));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Caster, out var caster));

            Assert.AreEqual(9, melee);
            Assert.AreEqual(10, ranged);
            Assert.AreEqual(11, caster);
        }

        [Test]
        public void TryGetCommandSlot_NextThreeRoles_OccupySecondToLastRow()
        {
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Siege, out var siege));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Flying, out var flying));
            Assert.IsTrue(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Super, out var super));

            Assert.AreEqual(6, siege);
            Assert.AreEqual(7, flying);
            Assert.AreEqual(8, super);
        }

        [Test]
        public void TryGetCommandSlot_Hero_ReturnsFalse()
        {
            Assert.IsFalse(MatchBarracksCallSlotRules.TryGetCommandSlot(UnitRole.Hero, out _));
        }
    }
}
