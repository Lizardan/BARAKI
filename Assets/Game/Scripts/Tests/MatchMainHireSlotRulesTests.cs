using Game.UI;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchMainHireSlotRulesTests
    {
        [Test]
        public void TryGetHeroHireSlot_MapsSlotsSixThroughEight()
        {
            Assert.IsTrue(MatchMainHireSlotRules.TryGetHeroHireSlot(1, out var one));
            Assert.IsTrue(MatchMainHireSlotRules.TryGetHeroHireSlot(2, out var two));
            Assert.IsTrue(MatchMainHireSlotRules.TryGetHeroHireSlot(3, out var three));
            Assert.AreEqual(6, one);
            Assert.AreEqual(7, two);
            Assert.AreEqual(8, three);
            Assert.AreEqual(6, MatchMainHireSlotRules.HeroHireSlotStart);
            Assert.IsFalse(MatchMainHireSlotRules.TryGetHeroHireSlot(0, out _));
            Assert.IsFalse(MatchMainHireSlotRules.TryGetHeroHireSlot(4, out _));
        }
    }
}
