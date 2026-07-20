using Game.Core;
using Game.Gameplay.Match;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Coordinates mid-match host migration: pause → elect → capture state → rebind → resume.
    /// Relay rebind is driven by <see cref="HostMigrationSessionDriver"/> after election.
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

        public int DesignatedHostSlot { get; private set; } = -1;
        public int PreviousHostSlot { get; private set; } = -1;
        public string ReconnectMatchId { get; private set; } = string.Empty;
        public byte[] CapturedStateBytes { get; private set; }

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

        void Update()
        {
            // Listen-server host drop: remaining clients detect lost connection and elect.
            if (Phase != HostMigrationRules.MigrationPhase.Playing)
            {
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.IsServer || nm.IsConnectedClient)
            {
                return;
            }

            if (MatchNetworkSession.LocalSlot < 0 && !MatchNetworkSession.HasHandle)
            {
                return;
            }

            var lobby = NetworkLobbyState.Instance;
            var count = lobby != null ? lobby.SlotCount : MatchNetworkSession.PlayerCount;
            if (count <= 0)
            {
                count = MatchSetup.DefaultPlayerCount;
            }

            var occupied = new bool[count];
            for (var i = 0; i < count; i++)
            {
                occupied[i] = lobby == null || lobby.GetSlotInfo(i).IsOccupied;
            }

            // Previous listen-host may no longer be slot 0 after a prior migration.
            BeginHostLost(MatchNetworkSession.ListenHostSlot, occupied, matchInProgress: true);
            if (Phase != HostMigrationRules.MigrationPhase.Aborted)
            {
                BeginStateTransferFromMatch();
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

            PreviousHostSlot = previousHostSlot;
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

        public void BeginStateTransferFromMatch()
        {
            if (Phase != HostMigrationRules.MigrationPhase.PausedAwaitingHost)
            {
                return;
            }

            var runtime = FindAnyObjectByType<MatchRuntime>();
            var lastGood = runtime?.LastNetworkSnapshotBytes;
            if (lastGood is { Length: > 0 })
            {
                CapturedStateBytes = lastGood;
                AdvanceAfterStateTransfer(true);
                TryBeginRebindDriver();
                return;
            }

            // Clients normally have empty local sim — live capture is only a fallback for host.
            if (runtime?.Controller != null && MatchNetworkSession.LocalSlot == PreviousHostSlot)
            {
                var snapshot = MatchSnapshotCodec.Capture(runtime.Controller);
                CapturedStateBytes = MatchSnapshotCodec.Serialize(snapshot);
                AdvanceAfterStateTransfer(CapturedStateBytes is { Length: > 0 });
                if (Phase != HostMigrationRules.MigrationPhase.Aborted)
                {
                    TryBeginRebindDriver();
                }

                return;
            }

            AdvanceAfterStateTransfer(false);
        }

        void TryBeginRebindDriver()
        {
            // Edit Mode unit tests exercise capture without a live session handle.
            if (!MatchNetworkSession.HasHandle)
            {
                return;
            }

            HostMigrationSessionDriver.Ensure().BeginRebind();
        }

        public bool TryApplyCapturedState(MatchController controller)
        {
            if (controller == null || CapturedStateBytes == null || CapturedStateBytes.Length == 0)
            {
                return false;
            }

            return HostMigrationApplyRules.TryApplyLastGood(
                controller,
                CapturedStateBytes,
                PreviousHostSlot);
        }

        public void AdvanceAfterStateTransfer(bool success)
        {
            Phase = HostMigrationRules.NextPhase(Phase, success);
            if (Phase == HostMigrationRules.MigrationPhase.Aborted)
            {
                Time.timeScale = 1f;
                HostMigrationSession.Clear();
            }
        }

        public void NotifyRelayRebound()
        {
            if (Phase != HostMigrationRules.MigrationPhase.RebindingRelay)
            {
                return;
            }

            Phase = HostMigrationRules.NextPhase(Phase, true);
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
            MatchNetworkSession.ListenHostSlot = DesignatedHostSlot;
            HostMigrationSession.Clear();
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
