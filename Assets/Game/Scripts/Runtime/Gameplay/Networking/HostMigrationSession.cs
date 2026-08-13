using System;

namespace Game.Gameplay.Networking
{
    public readonly struct HostMigrationSlotSnapshot
    {
        public HostMigrationSlotSnapshot(
            bool isOccupied,
            bool isReserved,
            string displayName,
            string playerId,
            float secondsSinceDisconnect)
        {
            IsOccupied = isOccupied;
            IsReserved = isReserved;
            DisplayName = displayName ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            SecondsSinceDisconnect = secondsSinceDisconnect;
        }

        public bool IsOccupied { get; }
        public bool IsReserved { get; }
        public string DisplayName { get; }
        public string PlayerId { get; }
        public float SecondsSinceDisconnect { get; }

        public bool ShouldRestore => IsOccupied || IsReserved;
    }

    /// <summary>Process-wide flags while listen-host rebinds after host loss.</summary>
    public static class HostMigrationSession
    {
        public static bool IsRebinding { get; private set; }
        public static int DesignatedHostSlot { get; private set; } = -1;
        public static int PreviousHostSlot { get; private set; } = -1;
        public static byte[] LastGoodBytes { get; private set; }
        public static string PreviousRelayJoinCode { get; private set; } = string.Empty;
        public static HostMigrationSlotSnapshot[] SlotSnapshot { get; private set; }
        public static int PendingKickSlot { get; private set; } = -1;

        public static void Begin(
            int previousHostSlot,
            int designatedHostSlot,
            byte[] lastGoodBytes,
            string previousRelayJoinCode,
            HostMigrationSlotSnapshot[] slotSnapshot = null)
        {
            IsRebinding = true;
            PreviousHostSlot = previousHostSlot;
            DesignatedHostSlot = designatedHostSlot;
            LastGoodBytes = lastGoodBytes;
            PreviousRelayJoinCode = previousRelayJoinCode ?? string.Empty;
            SlotSnapshot = slotSnapshot;
            // Keep a kick queued before rebind; Begin must not drop it.
        }

        public static void QueueKick(int slot)
        {
            if (slot >= 0)
            {
                PendingKickSlot = slot;
            }
        }

        public static int ConsumePendingKick()
        {
            var slot = PendingKickSlot;
            PendingKickSlot = -1;
            return slot;
        }

        public static int CountReservedSlots()
        {
            if (SlotSnapshot == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < SlotSnapshot.Length; i++)
            {
                if (SlotSnapshot[i].IsReserved
                    || (SlotSnapshot[i].IsOccupied && i == PreviousHostSlot && i != DesignatedHostSlot))
                {
                    count++;
                }
            }

            return count;
        }

        public static void Clear()
        {
            IsRebinding = false;
            DesignatedHostSlot = -1;
            PreviousHostSlot = -1;
            LastGoodBytes = null;
            PreviousRelayJoinCode = string.Empty;
            SlotSnapshot = null;
            PendingKickSlot = -1;
        }
    }
}
