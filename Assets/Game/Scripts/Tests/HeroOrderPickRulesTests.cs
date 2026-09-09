using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HeroOrderPickRulesTests
    {
        [Test]
        public void DefaultOrder_Is123()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, HeroOrderPickRules.DefaultOrder());
        }

        [Test]
        public void IsValidOrder_RequiresPermutationOf123()
        {
            Assert.IsTrue(HeroOrderPickRules.IsValidOrder(new[] { 1, 2, 3 }));
            Assert.IsTrue(HeroOrderPickRules.IsValidOrder(new[] { 3, 1, 2 }));
            Assert.IsFalse(HeroOrderPickRules.IsValidOrder(null));
            Assert.IsFalse(HeroOrderPickRules.IsValidOrder(new[] { 1, 2 }));
            Assert.IsFalse(HeroOrderPickRules.IsValidOrder(new[] { 1, 2, 4 }));
            Assert.IsFalse(HeroOrderPickRules.IsValidOrder(new[] { 1, 1, 2 }));
        }

        [Test]
        public void GetPosition_ReturnsOneBasedPosition()
        {
            Assert.AreEqual(1, HeroOrderPickRules.GetPosition(new[] { 1, 2, 3 }, 1));
            Assert.AreEqual(2, HeroOrderPickRules.GetPosition(new[] { 1, 2, 3 }, 2));
            Assert.AreEqual(3, HeroOrderPickRules.GetPosition(new[] { 3, 1, 2 }, 2));
            Assert.AreEqual(0, HeroOrderPickRules.GetPosition(new[] { 1, 2, 3 }, 0));
            Assert.AreEqual(0, HeroOrderPickRules.GetPosition(new[] { 1, 2, 3 }, 4));
        }

        [Test]
        public void GetPosition_InvalidOrderFallsBackToDefault()
        {
            Assert.AreEqual(2, HeroOrderPickRules.GetPosition(new[] { 9, 9, 9 }, 2));
        }

        [Test]
        public void IsUnlockedAtLevel_PositionMustBeAtOrBelowMainLevel()
        {
            // Default order: slot N unlocks at main level N (legacy behavior).
            Assert.IsTrue(HeroOrderPickRules.IsUnlockedAtLevel(new[] { 1, 2, 3 }, 1, 1));
            Assert.IsFalse(HeroOrderPickRules.IsUnlockedAtLevel(new[] { 1, 2, 3 }, 2, 1));
            Assert.IsTrue(HeroOrderPickRules.IsUnlockedAtLevel(new[] { 1, 2, 3 }, 2, 2));
            Assert.IsTrue(HeroOrderPickRules.IsUnlockedAtLevel(new[] { 1, 2, 3 }, 3, 3));
            Assert.IsFalse(HeroOrderPickRules.IsUnlockedAtLevel(new[] { 1, 2, 3 }, 1, 0));
        }

        [Test]
        public void IsUnlockedAtLevel_FollowsConfirmedOrder()
        {
            var order = new[] { 3, 1, 2 };
            Assert.IsTrue(HeroOrderPickRules.IsUnlockedAtLevel(order, 3, 1));
            Assert.IsTrue(HeroOrderPickRules.IsUnlockedAtLevel(order, 1, 2));
            Assert.IsTrue(HeroOrderPickRules.IsUnlockedAtLevel(order, 2, 3));
            Assert.IsFalse(HeroOrderPickRules.IsUnlockedAtLevel(order, 1, 1));
            Assert.IsFalse(HeroOrderPickRules.IsUnlockedAtLevel(order, 2, 2));
        }
    }
}