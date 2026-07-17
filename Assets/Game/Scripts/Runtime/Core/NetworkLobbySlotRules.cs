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

        /// <summary>
        /// Listen-server host sits in HostSlot at server init. Dedicated server: first client
        /// occupies HostSlot on connect and may Start when all slots are ready.
        /// </summary>
        public static bool CanDesignatedHostStart(int localSlot, bool matchStarted, bool lobbyReady) =>
            IsHostSlot(localSlot) && lobbyReady && !matchStarted;

        /// <summary>
        /// Host-as-server (StartAsHost): seat local client in HostSlot when lobby spawns.
        /// </summary>
        public static bool ShouldSeatListenHostOnServerInit(bool isHost) => isHost;
    }
}
