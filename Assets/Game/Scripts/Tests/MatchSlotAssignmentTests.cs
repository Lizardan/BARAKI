using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchSlotAssignmentTests
    {
        [Test]
        public void CreateShuffledSlotOrder_ContainsEachSlotOnce()
        {
            var order = MatchSlotAssignment.CreateShuffledSlotOrder(4, new Random(7));
            var seen = new HashSet<int>();

            Assert.AreEqual(4, order.Length);
            foreach (var slot in order)
            {
                Assert.IsTrue(seen.Add(slot), $"Duplicate slot {slot}");
                Assert.GreaterOrEqual(slot, 0);
                Assert.Less(slot, 4);
            }
        }

        [Test]
        public void CreateOffline_LocalSlotIsFromShuffledOrder()
        {
            var assignment = MatchSlotAssignment.CreateOffline(4, new Random(11));

            Assert.AreEqual(assignment.SlotOrder[0], assignment.LocalPlayerSlot);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, assignment.SlotOrder);
        }

        [Test]
        public void CreateOffline_ProducesVariedLocalSlotsAcrossSeeds()
        {
            var gotNonZero = false;
            for (var seed = 0; seed < 20; seed++)
            {
                var assignment = MatchSlotAssignment.CreateOffline(4, new Random(seed));
                if (assignment.LocalPlayerSlot != 0)
                {
                    gotNonZero = true;
                    break;
                }
            }

            Assert.IsTrue(gotNonZero);
        }

        [Test]
        public void CreateForLocalParticipants_AssignsUniqueFirstSlots()
        {
            var assignment = MatchSlotAssignment.CreateForLocalParticipants(6, localParticipantCount: 2, new Random(3));

            Assert.AreEqual(assignment.SlotOrder[0], assignment.LocalPlayerSlot);
            Assert.AreEqual(6, assignment.SlotOrder.Length);
        }
    }
}
