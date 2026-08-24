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
        public string PreviousHostDisplayName { get; private set; } = string.Empty;

        float _hostLossDetectedAtRealtime = -1f;
        bool _isolationCheckStarted;

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
            TryWatchIsolatedHost();

            // Listen-server host drop: remaining clients detect lost connection and elect.
            if (Phase != HostMigrationRules.MigrationPhase.Playing)
            {
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.IsServer || nm.IsConnectedClient)
            {
                if (_hostLossDetectedAtRealtime >= 0f && nm != null && nm.IsConnectedClient)
                {
                    _hostLossDetectedAtRealtime = -1f;
                    MatchPauseGate.SetDisconnectHoldPaused(false);
                }
                else
                {
                    _hostLossDetectedAtRealtime = -1f;
                }

                return;
            }

            if (MatchNetworkSession.LocalSlot < 0 && !MatchNetworkSession.HasHandle)
            {
                return;
            }

            // Pause immediately so a host drop does not keep simulating on clients.
            if (_hostLossDetectedAtRealtime < 0f)
            {
                _hostLossDetectedAtRealtime = Time.realtimeSinceStartup;
                PreviousHostSlot = MatchNetworkSession.ListenHostSlot;
                PreviousHostDisplayName = MatchNetworkSession.GetCachedSlotName(PreviousHostSlot);
                MatchPauseGate.SetDisconnectHoldPaused(true);
                return;
            }

            if (!HostMigrationRules.ShouldBeginMigrationAfterGrace(
                    Time.realtimeSinceStartup - _hostLossDetectedAtRealtime))
            {
                return;
            }

            var occupied = MatchNetworkSession.GetEligibleHostSlots();
            if (occupied.Length == 0)
            {
                var lobby = NetworkLobbyState.Instance;
                var count = lobby != null ? lobby.SlotCount : MatchNetworkSession.PlayerCount;
                if (count <= 0)
                {
                    count = MatchSetup.DefaultPlayerCount;
                }

                occupied = new bool[count];
                for (var i = 0; i < count; i++)
                {
                    var info = lobby?.GetSlotInfo(i) ?? default;
                    occupied[i] = lobby == null || info.IsEligibleHost;
                }
            }

            // Previous listen-host may no longer be slot 0 after a prior migration.
            var matchInProgress = MatchRematchRules.IsMatchInProgressForHostMigration(
                MatchRuntime.Current?.Controller?.Phase ?? MatchPhase.Lobby);
            BeginHostLost(MatchNetworkSession.ListenHostSlot, occupied, matchInProgress);
            if (Phase != HostMigrationRules.MigrationPhase.Aborted)
            {
                BeginStateTransferFromMatch();
            }
            else if (!matchInProgress)
            {
                MatchNetworkSession.LoadLobbyPreservingNetwork();
            }
        }

        void TryWatchIsolatedHost()
        {
            if (_isolationCheckStarted || Phase != HostMigrationRules.MigrationPhase.Playing)
            {
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost || !MatchNetworkSession.MatchStarted)
            {
                return;
            }

            if (nm.ConnectedClientsList.Count > 1)
            {
                _isolationCheckStarted = false;
                return;
            }

            var lobby = NetworkLobbyState.Instance;
            if (lobby == null || lobby.ReservedSlotCount <= 0)
            {
                return;
            }

            _isolationCheckStarted = true;
            HostIsolationWatch.Ensure().BeginCheck();
        }

        void SyncPauseGate() => MatchPauseGate.SetMigrationPaused(IsPaused);

        public void BeginHostLost(int previousHostSlot, bool[] occupiedSlots, bool matchInProgress)
        {
            var slotCount = occupiedSlots?.Length ?? 0;
            if (!HostMigrationRules.ShouldPauseMatch(true, matchInProgress)
                || !HostMigrationRules.IsValidHostSlot(previousHostSlot, slotCount))
            {
                Phase = HostMigrationRules.MigrationPhase.Aborted;
                SyncPauseGate();
                return;
            }

            PreviousHostSlot = previousHostSlot;
            DesignatedHostSlot = HostMigrationRules.ElectNewHostSlot(previousHostSlot, occupiedSlots);
            if (DesignatedHostSlot < 0)
            {
                Phase = HostMigrationRules.MigrationPhase.Aborted;
                SyncPauseGate();
                return;
            }

            Phase = HostMigrationRules.NextPhase(HostMigrationRules.MigrationPhase.Playing, true);
            SyncPauseGate();
            ReconnectMatchId = MatchNetworkSession.RoomCode;
            if (string.IsNullOrEmpty(PreviousHostDisplayName))
            {
                PreviousHostDisplayName = MatchNetworkSession.GetCachedSlotName(previousHostSlot);
            }

            PlaytestLog.Info(
                "Migration",
                "Paused",
                ("prevHost", previousHostSlot),
                ("nextHost", DesignatedHostSlot),
                ("match", ReconnectMatchId));
        }

        public void BeginStateTransferFromMatch(MatchRuntime runtime = null)
        {
            if (Phase != HostMigrationRules.MigrationPhase.PausedAwaitingHost)
            {
                return;
            }

            var activeRuntime = runtime ?? MatchRuntime.Current;
            if (HostMigrationApplyRules.TryCaptureState(
                    activeRuntime?.LastNetworkSnapshotBytes,
                    activeRuntime?.Controller,
                    out var captured))
            {
                CapturedStateBytes = captured;
                AdvanceAfterStateTransfer(true);
                TryBeginRebindDriver();
                return;
            }

            AdvanceAfterStateTransfer(false);
        }

        void TryBeginRebindDriver()
        {
            // Edit Mode unit tests exercise capture without a live session handle.
            if (!Application.isPlaying || !MatchNetworkSession.HasHandle)
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
                PreviousHostSlot,
                eliminatePreviousHost: false,
                wireContext: MatchNetworkAuthority.TryGetSharedWireContext(out var wire)
                    ? wire
                    : null);
        }

        public void AdvanceAfterStateTransfer(bool success)
        {
            Phase = HostMigrationRules.NextPhase(Phase, success);
            if (Phase == HostMigrationRules.MigrationPhase.Aborted)
            {
                SyncPauseGate();
                HostMigrationSession.Clear();
                PlaytestLog.Warn("Migration", "Abort", ("phase", "transfer"));
            }
            else
            {
                PlaytestLog.Info("Migration", "Transfer", ("ok", success));
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

            if (MatchNetworkSession.PlayerCount > 0
                && !HostMigrationRules.IsValidHostSlot(DesignatedHostSlot, MatchNetworkSession.PlayerCount))
            {
                Phase = HostMigrationRules.MigrationPhase.Aborted;
                SyncPauseGate();
                HostMigrationSession.Clear();
                PlaytestLog.Warn("Migration", "Abort", ("phase", "resume-slot"));
                return;
            }

            Phase = HostMigrationRules.MigrationPhase.Playing;
            MatchNetworkSession.ListenHostSlot = DesignatedHostSlot;
            HostMigrationSession.Clear();
            SyncPauseGate();
            PlaytestLog.Info(
                "Migration",
                "Resume",
                ("host", DesignatedHostSlot),
                ("prevHost", PreviousHostSlot));
            SessionFlowTracker.NotifyChanged();
        }

        public bool TryBuildReconnectToken(int slot, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(ReconnectMatchId) || slot < 0)
            {
                return false;
            }

            token = PlayerReconnectRules.BuildSessionToken(
                ReconnectMatchId,
                slot,
                UnityServicesBootstrap.PlayerId);
            return true;
        }
    }
}
