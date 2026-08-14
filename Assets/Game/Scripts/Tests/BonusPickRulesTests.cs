using System;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BonusPickRulesTests
    {
        [Test]
        public void SlotCount_IsTwelve()
        {
            Assert.AreEqual(12, BonusPickRules.SlotCount);
        }

        [Test]
        public void OverlayDuration_IsSixtySeconds()
        {
            Assert.AreEqual(60f, BonusPickRules.OverlayDurationSeconds);
        }

        [Test]
        public void MaxPicks_IsOne()
        {
            Assert.AreEqual(1, BonusPickRules.MaxPicksPerPlayer);
        }

        [Test]
        public void IsValidSlot_AcceptsOneThroughTwelve()
        {
            for (var slot = 1; slot <= BonusPickRules.SlotCount; slot++)
            {
                Assert.IsTrue(BonusPickRules.IsValidSlot(slot), $"slot {slot}");
            }

            Assert.IsFalse(BonusPickRules.IsValidSlot(0));
            Assert.IsFalse(BonusPickRules.IsValidSlot(-1));
            Assert.IsFalse(BonusPickRules.IsValidSlot(13));
            Assert.IsFalse(BonusPickRules.IsValidSlot(int.MaxValue));
        }

        [Test]
        public void GetSlotId_ReturnsStableIdsPerSlot()
        {
            Assert.AreEqual("BONUS_SLOT_MELEE", BonusPickRules.GetSlotId(1));
            Assert.AreEqual("BONUS_SLOT_RANGED", BonusPickRules.GetSlotId(2));
            Assert.AreEqual("BONUS_SLOT_CASTER", BonusPickRules.GetSlotId(3));
            Assert.AreEqual("BONUS_SLOT_SIEGE", BonusPickRules.GetSlotId(4));
            Assert.AreEqual("BONUS_SLOT_FLYING", BonusPickRules.GetSlotId(5));
            Assert.AreEqual("BONUS_SLOT_SUPER", BonusPickRules.GetSlotId(6));
            Assert.AreEqual("BONUS_SLOT_HERO_1", BonusPickRules.GetSlotId(7));
            Assert.AreEqual("BONUS_SLOT_HERO_2", BonusPickRules.GetSlotId(8));
            Assert.AreEqual("BONUS_SLOT_HERO_3", BonusPickRules.GetSlotId(9));
            Assert.AreEqual("BONUS_SLOT_TITAN", BonusPickRules.GetSlotId(10));
            Assert.AreEqual("BONUS_SLOT_RACE_UNIQUE_1", BonusPickRules.GetSlotId(11));
            Assert.AreEqual("BONUS_SLOT_RACE_UNIQUE_2", BonusPickRules.GetSlotId(12));
            Assert.AreEqual(string.Empty, BonusPickRules.GetSlotId(0));
            Assert.AreEqual(string.Empty, BonusPickRules.GetSlotId(13));
        }

        [Test]
        public void GetSlotDisplayName_ReturnsNameForEverySlot()
        {
            for (var slot = 1; slot <= BonusPickRules.SlotCount; slot++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(BonusPickRules.GetSlotDisplayName(slot)));
            }

            Assert.AreEqual(string.Empty, BonusPickRules.GetSlotDisplayName(0));
            Assert.AreEqual(string.Empty, BonusPickRules.GetSlotDisplayName(13));
        }

        [Test]
        public void DisplayOrderSlots_StartsWithTitanThenRaceUnique()
        {
            Assert.AreEqual(12, BonusPickRules.DisplayOrderSlots.Length);
            Assert.AreEqual(10, BonusPickRules.DisplayOrderSlots[0]);
            Assert.AreEqual(11, BonusPickRules.DisplayOrderSlots[1]);
        }

        [Test]
        public void DisplayOrderSlots_IsPermutationOfAllSlots()
        {
            var seen = new bool[BonusPickRules.SlotCount + 1];
            foreach (var slot in BonusPickRules.DisplayOrderSlots)
            {
                Assert.IsTrue(BonusPickRules.IsValidSlot(slot), $"invalid slot {slot}");
                Assert.IsFalse(seen[slot], $"duplicate slot {slot}");
                seen[slot] = true;
            }
        }

        [Test]
        public void DisplayOrderSlots_EndsWithRaceUnique2()
        {
            Assert.AreEqual(12, BonusPickRules.DisplayOrderSlots[^1]);
        }

        [Test]
        public void GetRandomSlot_StaysWithinValidRange()
        {
            var random = new Random(42);
            for (var i = 0; i < 1000; i++)
            {
                var slot = BonusPickRules.GetRandomSlot(random);
                Assert.IsTrue(BonusPickRules.IsValidSlot(slot));
            }
        }

        [Test]
        public void GetRandomSlot_ThrowsOnNullRandom()
        {
            Assert.Throws<ArgumentNullException>(() => BonusPickRules.GetRandomSlot(null));
        }

        [Test]
        public void TryApplyPick_AcceptsFirstValidPick()
        {
            var picks = new int[4];
            Assert.IsTrue(BonusPickNetworkRules.TryApplyPick(picks, playerSlot: 1, bonusSlot: 5));
            Assert.AreEqual(5, picks[1]);
        }

        [Test]
        public void TryApplyPick_RejectsSecondPickForSamePlayer()
        {
            var picks = new int[4];
            Assert.IsTrue(BonusPickNetworkRules.TryApplyPick(picks, playerSlot: 0, bonusSlot: 3));
            Assert.IsFalse(BonusPickNetworkRules.TryApplyPick(picks, playerSlot: 0, bonusSlot: 7));
            Assert.AreEqual(3, picks[0]);
        }

        [Test]
        public void TryApplyPick_RejectsInvalidSlot()
        {
            var picks = new int[4];
            Assert.IsFalse(BonusPickNetworkRules.TryApplyPick(picks, playerSlot: 0, bonusSlot: 0));
            Assert.IsFalse(BonusPickNetworkRules.TryApplyPick(picks, playerSlot: 0, bonusSlot: 13));
            Assert.AreEqual(0, picks[0]);
        }

        [Test]
        public void TryApplyPick_RejectsOutOfRangePlayer()
        {
            var picks = new int[2];
            Assert.IsFalse(BonusPickNetworkRules.TryApplyPick(picks, playerSlot: -1, bonusSlot: 1));
            Assert.IsFalse(BonusPickNetworkRules.TryApplyPick(picks, playerSlot: 2, bonusSlot: 1));
        }

        [Test]
        public void FillTimeoutPicks_FillsOnlyUnpickedSlots()
        {
            var picks = new int[4];
            picks[1] = 5;

            BonusPickNetworkRules.FillTimeoutPicks(picks, new Random(7));

            Assert.AreEqual(5, picks[1]);
            Assert.IsTrue(BonusPickRules.IsValidSlot(picks[0]));
            Assert.IsTrue(BonusPickRules.IsValidSlot(picks[2]));
            Assert.IsTrue(BonusPickRules.IsValidSlot(picks[3]));
        }

        [Test]
        public void FillTimeoutPicks_ThrowsOnNullRandom()
        {
            Assert.Throws<ArgumentNullException>(
                () => BonusPickNetworkRules.FillTimeoutPicks(new int[2], null));
        }
    }
}
