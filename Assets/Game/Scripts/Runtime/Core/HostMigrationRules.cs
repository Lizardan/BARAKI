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

        public static bool ShouldPauseMatch(bool hostDisconnected, bool matchInProgress) =>
            hostDisconnected && matchInProgress;

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
