using System;

namespace Game.Core
{
    /// <summary>
    /// Mid-match host migration: pause → elect new host → state transfer → unpause.
    /// MVP may abort match; these rules define the post-MVP contract.
    /// </summary>
    public static class HostMigrationRules
    {
        public enum MigrationPhase
        {
            Playing = 0,
            PausedAwaitingHost = 1,
            TransferringState = 2,
            RebindingRelay = 3,
            Resuming = 4,
            Aborted = 5,
        }

        public static int ElectNewHostSlot(int previousHostSlot, bool[] slotOccupied)
        {
            if (slotOccupied == null || slotOccupied.Length == 0)
            {
                return -1;
            }

            if (!IsValidHostSlot(previousHostSlot, slotOccupied.Length))
            {
                return -1;
            }

            for (var offset = 1; offset <= slotOccupied.Length; offset++)
            {
                var candidate = (previousHostSlot + offset) % slotOccupied.Length;
                if (candidate == previousHostSlot)
                {
                    continue;
                }

                if (slotOccupied[candidate])
                {
                    return candidate;
                }
            }

            return -1;
        }

        public static bool IsValidHostSlot(int slot, int slotCount) =>
            slot >= 0 && slotCount > 0 && slot < slotCount;

        /// <summary>
        /// Reserved (disconnected) slots must not be elected; only live occupants can host.
        /// </summary>
        public static bool[] BuildEligibleOccupied(bool[] occupied, bool[] reserved)
        {
            if (occupied == null)
            {
                return Array.Empty<bool>();
            }

            var eligible = new bool[occupied.Length];
            for (var i = 0; i < occupied.Length; i++)
            {
                var isReserved = reserved != null && i < reserved.Length && reserved[i];
                eligible[i] = occupied[i] && !isReserved;
            }

            return eligible;
        }

        /// <summary>
        /// After Relay rebind the match stays paused while the previous host (or any
        /// reserved player) can still reconnect or be kicked.
        /// </summary>
        public static bool ShouldHoldPauseAfterMigration(int reservedSlotCount) =>
            reservedSlotCount > 0;

        public static bool ShouldPauseMatch(bool hostDisconnected, bool matchInProgress) =>
            hostDisconnected && matchInProgress;

        public const float HostLossGraceSeconds = 1.5f;

        public const float ClientRejoinTimeoutSeconds = 5f;

        public const float MinClientRejoinWaitSeconds = 0.5f;

        /// <summary>
        /// Host-loss detection is debounced: only begin migration after the loss
        /// has persisted past <paramref name="graceSeconds"/> (avoids panic on brief hiccups).
        /// </summary>
        public static bool ShouldBeginMigrationAfterGrace(
            float elapsedSinceLoss,
            float graceSeconds = HostLossGraceSeconds) =>
            elapsedSinceLoss >= 0f && elapsedSinceLoss >= graceSeconds;

        /// <summary>New listen-host resumes once enough peers have rejoined.</summary>
        public static bool HasEnoughClientsRejoined(int connectedClients, int expectedClients) =>
            connectedClients >= expectedClients;

        public static bool HasClientWaitTimedOut(
            float elapsedSinceRebind,
            float timeoutSeconds = ClientRejoinTimeoutSeconds) =>
            elapsedSinceRebind >= timeoutSeconds;

        public static bool CanResume(
            MigrationPhase phase,
            bool newHostReady,
            bool allClientsReconnected,
            bool stateApplied) =>
            phase == MigrationPhase.Resuming
            && newHostReady
            && allClientsReconnected
            && stateApplied;

        public static MigrationPhase NextPhase(MigrationPhase current, bool success)
        {
            if (!success)
            {
                return MigrationPhase.Aborted;
            }

            return current switch
            {
                MigrationPhase.Playing => MigrationPhase.PausedAwaitingHost,
                MigrationPhase.PausedAwaitingHost => MigrationPhase.TransferringState,
                MigrationPhase.TransferringState => MigrationPhase.RebindingRelay,
                MigrationPhase.RebindingRelay => MigrationPhase.Resuming,
                MigrationPhase.Resuming => MigrationPhase.Playing,
                _ => MigrationPhase.Aborted,
            };
        }
    }
}
