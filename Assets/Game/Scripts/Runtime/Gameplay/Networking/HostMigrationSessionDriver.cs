using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Runs Relay/NGO rebind after <see cref="HostMigrationCoordinator"/> captures last-good state.
    /// </summary>
    public sealed class HostMigrationSessionDriver : MonoBehaviour
    {
        static HostMigrationSessionDriver s_instance;
        bool _isRunning;

        public static HostMigrationSessionDriver Ensure()
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            var existing = FindAnyObjectByType<HostMigrationSessionDriver>();
            if (existing != null)
            {
                s_instance = existing;
                return existing;
            }

            var go = new GameObject(nameof(HostMigrationSessionDriver));
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<HostMigrationSessionDriver>();
            return s_instance;
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }

        void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        public void BeginRebind()
        {
            if (_isRunning)
            {
                return;
            }

            RunRebindAsync().Forget();
        }

        async UniTaskVoid RunRebindAsync()
        {
            _isRunning = true;
            var coordinator = HostMigrationCoordinator.Instance;
            if (coordinator == null)
            {
                _isRunning = false;
                return;
            }

            try
            {
                if (coordinator.Phase != HostMigrationRules.MigrationPhase.TransferringState
                    && coordinator.Phase != HostMigrationRules.MigrationPhase.RebindingRelay)
                {
                    return;
                }

                if (coordinator.Phase == HostMigrationRules.MigrationPhase.TransferringState)
                {
                    coordinator.AdvanceAfterStateTransfer(true);
                }

                var previousRelay = MatchNetworkSession.CurrentHandle.RelayJoinCode;
                HostMigrationSession.Begin(
                    coordinator.PreviousHostSlot,
                    coordinator.DesignatedHostSlot,
                    coordinator.CapturedStateBytes,
                    previousRelay);

                var isDesignated = MatchNetworkSession.LocalSlot == coordinator.DesignatedHostSlot;
                MatchNetworkSession.ShutdownTransportKeepingSession();

                bool transportOk;
                if (isDesignated)
                {
                    transportOk = await MatchNetworkSession.TryMigrateAsListenHostAsync();
                }
                else
                {
                    transportOk = await MatchNetworkSession.TryRejoinMigratedHostAsync();
                }

                if (!transportOk)
                {
                    Debug.LogError("HostMigration: transport rebind failed.");
                    coordinator.AdvanceAfterStateTransfer(false);
                    return;
                }

                coordinator.NotifyRelayRebound();

                var runtime = FindAnyObjectByType<MatchRuntime>();
                var stateApplied = false;
                if (isDesignated && runtime?.Controller != null)
                {
                    stateApplied = coordinator.TryApplyCapturedState(runtime.Controller);
                    runtime.StoreLastNetworkSnapshot(
                        MatchSnapshotCodec.Deserialize(coordinator.CapturedStateBytes),
                        coordinator.CapturedStateBytes);
                }
                else if (runtime != null && coordinator.CapturedStateBytes is { Length: > 0 })
                {
                    var snapshot = MatchSnapshotCodec.Deserialize(coordinator.CapturedStateBytes);
                    runtime.ApplyNetworkSnapshot(snapshot, coordinator.CapturedStateBytes);
                    stateApplied = true;
                }

                // Short grace for peers to finish NGO reconnect before unpausing.
                await UniTask.Delay(500, ignoreTimeScale: true);
                coordinator.TryResume(
                    newHostReady: transportOk,
                    allClientsReconnected: true,
                    stateApplied: stateApplied || coordinator.CapturedStateBytes is { Length: > 0 });
            }
            finally
            {
                _isRunning = false;
            }
        }
    }
}
