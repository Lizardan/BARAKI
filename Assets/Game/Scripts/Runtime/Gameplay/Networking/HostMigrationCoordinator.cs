using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Coordinates mid-match host migration (post-MVP).
    /// MVP: host disconnect ends the match; this type holds the pause/elect/resume contract.
    /// </summary>
    public sealed class HostMigrationCoordinator : MonoBehaviour
    {
        public static HostMigrationCoordinator Instance { get; private set; }

        public HostMigrationRules.MigrationPhase Phase { get; private set; } =
            HostMigrationRules.MigrationPhase.Playing;

        public bool IsPaused =>
            Phase is HostMigrationRules.MigrationPhase.PausedAwaitingHost
                or HostMigrationRules.MigrationPhase.TransferringState
                or HostMigrationRules.MigrationPhase.RebindingRelay
                or HostMigrationRules.MigrationPhase.Resuming;

        public int DesignatedHostSlot { get; private set; }
        public string ReconnectMatchId { get; private set; } = string.Empty;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BeginHostLost(int previousHostSlot, bool[] occupiedSlots, bool matchInProgress)
        {
            if (!HostMigrationRules.ShouldPauseMatch(true, matchInProgress))
            {
                Phase = HostMigrationRules.MigrationPhase.Aborted;
                Time.timeScale = 1f;
                return;
            }

            DesignatedHostSlot = HostMigrationRules.ElectNewHostSlot(previousHostSlot, occupiedSlots);
            if (DesignatedHostSlot < 0)
            {
                Phase = HostMigrationRules.MigrationPhase.Aborted;
                Time.timeScale = 1f;
                return;
            }

            Phase = HostMigrationRules.NextPhase(HostMigrationRules.MigrationPhase.Playing, true);
            Time.timeScale = 0f;
            ReconnectMatchId = MatchNetworkSession.RoomCode;
            Debug.Log(
                $"HostMigration: paused, elect slot={DesignatedHostSlot} match={ReconnectMatchId}");
        }

        public void AdvanceAfterStateTransfer(bool success)
        {
            Phase = HostMigrationRules.NextPhase(Phase, success);
            if (Phase == HostMigrationRules.MigrationPhase.Aborted)
            {
                Time.timeScale = 1f;
            }
        }

        public void TryResume(bool newHostReady, bool allClientsReconnected, bool stateApplied)
        {
            if (Phase != HostMigrationRules.MigrationPhase.Resuming
                && Phase != HostMigrationRules.MigrationPhase.RebindingRelay)
            {
                return;
            }

            if (Phase == HostMigrationRules.MigrationPhase.RebindingRelay)
            {
                Phase = HostMigrationRules.NextPhase(Phase, true);
            }

            if (!HostMigrationRules.CanResume(
                    Phase,
                    newHostReady,
                    allClientsReconnected,
                    stateApplied))
            {
                return;
            }

            Phase = HostMigrationRules.MigrationPhase.Playing;
            Time.timeScale = 1f;
            Debug.Log("HostMigration: resumed");
        }

        public bool TryBuildReconnectToken(int slot, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(ReconnectMatchId) || slot < 0)
            {
                return false;
            }

            token = PlayerReconnectRules.BuildSessionToken(ReconnectMatchId, slot);
            return true;
        }
    }
}
