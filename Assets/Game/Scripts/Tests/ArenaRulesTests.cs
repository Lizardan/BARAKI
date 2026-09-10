using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ArenaRulesTests
    {
        [Test]
        public void RewardGold_ScalesWithArenaIndex()
        {
            Assert.AreEqual(1000, ArenaRules.RewardGold(1));
            Assert.AreEqual(2000, ArenaRules.RewardGold(2));
            Assert.AreEqual(3000, ArenaRules.RewardGold(3));
            Assert.AreEqual(4000, ArenaRules.RewardGold(4));
        }

        [Test]
        public void RewardGold_ClampsNonPositiveIndex()
        {
            Assert.AreEqual(1000, ArenaRules.RewardGold(0));
            Assert.AreEqual(1000, ArenaRules.RewardGold(-3));
        }

        [Test]
        public void IsTitanArena_OnlyLastOne()
        {
            Assert.IsFalse(ArenaRules.IsTitanArena(1));
            Assert.IsFalse(ArenaRules.IsTitanArena(3));
            Assert.IsTrue(ArenaRules.IsTitanArena(4));
            Assert.IsTrue(ArenaRules.IsTitanArena(5));
        }

        [Test]
        public void PickPacking_RoundTripsEverySlot()
        {
            var packed = 0;
            for (var slot = 0; slot < ArenaRules.MaxSlots; slot++)
            {
                packed = ArenaRules.WritePick(packed, slot, slot + 1);
            }

            for (var slot = 0; slot < ArenaRules.MaxSlots; slot++)
            {
                Assert.AreEqual(slot + 1, ArenaRules.ReadPick(packed, slot));
            }
        }

        [Test]
        public void PickPacking_SupportsTitanValue()
        {
            var packed = ArenaRules.WritePick(0, 3, ArenaRules.PickValueTitan);
            Assert.AreEqual(ArenaRules.PickValueTitan, ArenaRules.ReadPick(packed, 3));
            Assert.AreEqual(0, ArenaRules.ReadPick(packed, 2));
        }

        [Test]
        public void PickPacking_OutOfRangeSlotIsNoop()
        {
            var packed = ArenaRules.WritePick(0, ArenaRules.MaxSlots, 5);
            Assert.AreEqual(0, packed);
            Assert.AreEqual(0, ArenaRules.ReadPick(packed, ArenaRules.MaxSlots));
        }

        [Test]
        public void Mask_IndependentPerSlot()
        {
            byte mask = 0;
            mask = ArenaRules.SetMask(mask, 0, true);
            mask = ArenaRules.SetMask(mask, 3, true);

            Assert.IsTrue(ArenaRules.HasMask(mask, 0));
            Assert.IsFalse(ArenaRules.HasMask(mask, 1));
            Assert.IsTrue(ArenaRules.HasMask(mask, 3));

            mask = ArenaRules.SetMask(mask, 0, false);
            Assert.IsFalse(ArenaRules.HasMask(mask, 0));
            Assert.IsTrue(ArenaRules.HasMask(mask, 3));
        }

        [Test]
        public void UsedHeroMask_DistinguishesSlotAndHero()
        {
            var mask = ArenaRules.MarkUsedHero(0u, 2, 3);

            Assert.IsTrue(ArenaRules.HasUsedHero(mask, 2, 3));
            Assert.IsFalse(ArenaRules.HasUsedHero(mask, 2, 2));
            Assert.IsFalse(ArenaRules.HasUsedHero(mask, 3, 3));
            Assert.IsFalse(ArenaRules.HasUsedHero(mask, 0, 3));
        }

        [Test]
        public void UsedHeroMask_CoversWholeRoster()
        {
            var mask = 0u;
            for (var slot = 0; slot < ArenaRules.MaxSlots; slot++)
            {
                for (var hero = 1; hero <= ArenaRules.HeroSlots; hero++)
                {
                    mask = ArenaRules.MarkUsedHero(mask, slot, hero);
                }
            }

            for (var slot = 0; slot < ArenaRules.MaxSlots; slot++)
            {
                for (var hero = 1; hero <= ArenaRules.HeroSlots; hero++)
                {
                    Assert.IsTrue(ArenaRules.HasUsedHero(mask, slot, hero));
                }
            }
        }

        [Test]
        public void ArenaCenter_IsFarFromMapBounds()
        {
            var distance = UnityEngine.Vector3.Distance(ArenaRules.Center, UnityEngine.Vector3.zero);
            Assert.Greater(distance, 500f, "Арена должна лежать далеко за пределами карты");
        }
    }
}
