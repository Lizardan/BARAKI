using System;

namespace Game.Core
{
    /// <summary>
    /// Shuffled arena slot order. Each participant gets a unique slot on the ring.
    /// </summary>
    public sealed class MatchSlotAssignment
    {
        public MatchSlotAssignment(int localPlayerSlot, int[] slotOrder)
        {
            if (slotOrder == null)
            {
                throw new ArgumentNullException(nameof(slotOrder));
            }

            LocalPlayerSlot = localPlayerSlot;
            SlotOrder = slotOrder;
        }

        public int LocalPlayerSlot { get; }

        /// <summary>Unique permutation of arena slots. Index = participant, value = arena slot.</summary>
        public int[] SlotOrder { get; }

        public static MatchSlotAssignment CreateOffline(int playerCount, Random random = null)
        {
            return CreateForLocalParticipants(playerCount, localParticipantCount: 1, random);
        }

        public static MatchSlotAssignment CreateForLocalParticipants(
            int playerCount,
            int localParticipantCount,
            Random random = null)
        {
            if (playerCount < 2 || playerCount > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be 2..8.");
            }

            if (localParticipantCount < 1 || localParticipantCount > playerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(localParticipantCount));
            }

            random ??= new Random();
            var slotOrder = CreateShuffledSlotOrder(playerCount, random);
            return new MatchSlotAssignment(slotOrder[0], slotOrder);
        }

        public static int[] CreateShuffledSlotOrder(int playerCount, Random random)
        {
            if (playerCount < 2 || playerCount > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be 2..8.");
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var slots = new int[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                slots[i] = i;
            }

            for (var i = playerCount - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }

            return slots;
        }
    }
}
