using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class NetworkLobbySlotRules
    {
        public const int MaxSlots = 8;
        public const int HostSlot = 0;

        public static int FindNextFreeSlot(IReadOnlyList<bool> occupied, int playerCount)
        {
            if (occupied == null)
            {
                throw new ArgumentNullException(nameof(occupied));
            }

            if (playerCount < 0 || playerCount > occupied.Count || playerCount > MaxSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            for (var slot = 0; slot < playerCount; slot++)
            {
                if (!occupied[slot])
                {
                    return slot;
                }
            }

            return -1;
        }

        public static bool IsHostSlot(int slot) => slot == HostSlot;
    }
}
