using Game.Gameplay.Match;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Server-only bridge between NGO and <see cref="MatchRuntime"/>.
    /// Offline: MatchRuntime ticks. Networked server: this ticks + publishes snapshots.
    /// </summary>
    public sealed class MatchNetworkAuthority : NetworkBehaviour
    {
        const float SnapshotHz = 15f;

        [SerializeField] private MatchRuntime _matchRuntime;

        float _snapshotAccumulator;
        MatchTickMode _tickMode = MatchTickMode.Offline;

        public MatchTickMode TickMode => _tickMode;

        public override void OnNetworkSpawn()
        {
            _tickMode = IsServer ? MatchTickMode.Server : MatchTickMode.Client;
            EnsureRuntime();
        }

        public override void OnNetworkDespawn()
        {
            _tickMode = MatchTickMode.Offline;
            if (_matchRuntime != null)
            {
                _matchRuntime.SetNetworkTickMode(MatchTickMode.Offline);
            }
        }

        private void Update()
        {
            EnsureRuntime();
            if (!IsSpawned || !IsServer || _matchRuntime == null)
            {
                return;
            }

            if (!MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Server))
            {
                return;
            }

            // MatchRuntime is gated off on server; we own the tick.
            if (_matchRuntime.IsMatchStarted && _matchRuntime.Controller != null)
            {
                _matchRuntime.Controller.Tick(Time.deltaTime);
                _matchRuntime.NotifyServerTick();

                _snapshotAccumulator += Time.deltaTime;
                if (_snapshotAccumulator >= 1f / SnapshotHz)
                {
                    _snapshotAccumulator = 0f;
                    var snapshot = MatchSnapshotCodec.Capture(_matchRuntime.Controller);
                    var bytes = MatchSnapshotCodec.Serialize(snapshot);
                    ApplySnapshotClientRpc(bytes);
                }
            }
        }

        [ClientRpc]
        void ApplySnapshotClientRpc(byte[] bytes)
        {
            if (IsServer)
            {
                return;
            }

            var snapshot = MatchSnapshotCodec.Deserialize(bytes);
            _matchRuntime?.ApplyNetworkSnapshot(snapshot);
        }

        private void EnsureRuntime()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }

            if (_matchRuntime == null)
            {
                return;
            }

            _matchRuntime.SetNetworkTickMode(_tickMode);
            if (_tickMode == MatchTickMode.Client
                && _matchRuntime.GetComponent<MatchSnapshotPresenter>() == null)
            {
                _matchRuntime.gameObject.AddComponent<MatchSnapshotPresenter>();
            }
        }
    }
}
