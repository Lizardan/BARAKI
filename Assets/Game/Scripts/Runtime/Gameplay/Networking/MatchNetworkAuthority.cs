using System;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Server-only bridge between NGO and <see cref="MatchRuntime"/>.
    /// Offline: MatchRuntime ticks. Networked server: this ticks + publishes snapshots.
    /// Clients send validated commands via ServerRpc.
    /// </summary>
    public sealed class MatchNetworkAuthority : NetworkBehaviour
    {
        const float SnapshotHz = 15f;

        [SerializeField] private MatchRuntime _matchRuntime;

        float _simAccumulator;
        float _snapshotAccumulator;
        MatchTickMode _tickMode = MatchTickMode.Offline;

        public static MatchNetworkAuthority Instance { get; private set; }

        public static event Action<MatchCommandResult> CommandResultReceived;

        public MatchTickMode TickMode => _tickMode;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _tickMode = IsServer ? MatchTickMode.Server : MatchTickMode.Client;
            EnsureRuntime();
            EnsureHostMigrationCoordinator();
        }

        static void EnsureHostMigrationCoordinator()
        {
            if (HostMigrationCoordinator.Instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(HostMigrationCoordinator));
            DontDestroyOnLoad(go);
            go.AddComponent<HostMigrationCoordinator>();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _tickMode = MatchTickMode.Offline;
            if (_matchRuntime != null)
            {
                _matchRuntime.SetNetworkTickMode(MatchTickMode.Offline);
            }
        }

        public void RequestStartResearch(int buildingInstanceId, string upgradeId)
        {
            if (IsCommandsBlocked())
            {
                PublishCommandResult(MatchCommandResult.HostMigrating);
                return;
            }

            if (IsServer)
            {
                var result = TryStartResearchLocal(MatchNetworkSession.LocalSlot, buildingInstanceId, upgradeId);
                PublishCommandResult(result);
                return;
            }

            RequestStartResearchServerRpc(buildingInstanceId, upgradeId ?? string.Empty);
        }

        public void RequestHireHero(int heroSlot)
        {
            if (IsCommandsBlocked())
            {
                PublishCommandResult(MatchCommandResult.HostMigrating);
                return;
            }

            if (IsServer)
            {
                PublishCommandResult(TryHireHeroLocal(MatchNetworkSession.LocalSlot, heroSlot));
                return;
            }

            RequestHireHeroServerRpc(heroSlot);
        }

        public void RequestDeployHero(int buildingInstanceId, int heroSlot)
        {
            if (IsCommandsBlocked())
            {
                PublishCommandResult(MatchCommandResult.HostMigrating);
                return;
            }

            if (IsServer)
            {
                PublishCommandResult(
                    TryDeployHeroLocal(MatchNetworkSession.LocalSlot, buildingInstanceId, heroSlot));
                return;
            }

            RequestDeployHeroServerRpc(buildingInstanceId, heroSlot);
        }

        public void RequestSetTowerTarget(int towerInstanceId, int unitId)
        {
            if (IsCommandsBlocked())
            {
                PublishCommandResult(MatchCommandResult.HostMigrating);
                return;
            }

            if (IsServer)
            {
                PublishCommandResult(
                    TrySetTowerTargetLocal(MatchNetworkSession.LocalSlot, towerInstanceId, unitId));
                return;
            }

            RequestSetTowerTargetServerRpc(towerInstanceId, unitId);
        }

        public void RequestManualCall(int barracksBuildingInstanceId, UnitRole role)
        {
            if (IsCommandsBlocked())
            {
                PublishCommandResult(MatchCommandResult.HostMigrating);
                return;
            }

            if (IsServer)
            {
                PublishCommandResult(
                    TryManualCallLocal(MatchNetworkSession.LocalSlot, barracksBuildingInstanceId, role));
                return;
            }

            RequestManualCallServerRpc(barracksBuildingInstanceId, (byte)role);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestStartResearchServerRpc(
            int buildingInstanceId,
            string upgradeId,
            RpcParams rpcParams = default)
        {
            var slot = ResolveSenderSlot(rpcParams);
            var result = TryStartResearchLocal(slot, buildingInstanceId, upgradeId);
            SendCommandResult(result, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestHireHeroServerRpc(int heroSlot, RpcParams rpcParams = default)
        {
            var result = TryHireHeroLocal(ResolveSenderSlot(rpcParams), heroSlot);
            SendCommandResult(result, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestDeployHeroServerRpc(
            int buildingInstanceId,
            int heroSlot,
            RpcParams rpcParams = default)
        {
            var result = TryDeployHeroLocal(
                ResolveSenderSlot(rpcParams),
                buildingInstanceId,
                heroSlot);
            SendCommandResult(result, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestSetTowerTargetServerRpc(
            int towerInstanceId,
            int unitId,
            RpcParams rpcParams = default)
        {
            var result = TrySetTowerTargetLocal(
                ResolveSenderSlot(rpcParams),
                towerInstanceId,
                unitId);
            SendCommandResult(result, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestManualCallServerRpc(
            int barracksBuildingInstanceId,
            byte role,
            RpcParams rpcParams = default)
        {
            var result = TryManualCallLocal(
                ResolveSenderSlot(rpcParams),
                barracksBuildingInstanceId,
                (UnitRole)role);
            SendCommandResult(result, rpcParams.Receive.SenderClientId);
        }

        private void Update()
        {
            EnsureRuntime();
            if (!IsSpawned || !IsServer || _matchRuntime == null)
            {
                return;
            }

            if (HostMigrationCoordinator.Instance != null
                && HostMigrationCoordinator.Instance.IsPaused)
            {
                return;
            }

            if (!MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Server))
            {
                return;
            }

            if (!_matchRuntime.IsMatchStarted || _matchRuntime.Controller == null)
            {
                return;
            }

            var steps = MatchNetworkSimTickRules.ConsumeSteps(ref _simAccumulator, Time.deltaTime);
            for (var i = 0; i < steps; i++)
            {
                _matchRuntime.Controller.Tick(MatchNetworkSimTickRules.FixedDeltaSeconds);
                _matchRuntime.NotifyServerTick();
            }

            if (steps <= 0)
            {
                return;
            }

            _snapshotAccumulator += steps * MatchNetworkSimTickRules.FixedDeltaSeconds;
            if (_snapshotAccumulator >= 1f / SnapshotHz)
            {
                _snapshotAccumulator = 0f;
                var snapshot = MatchSnapshotCodec.Capture(_matchRuntime.Controller);
                var bytes = MatchSnapshotCodec.Serialize(snapshot);
                _matchRuntime.StoreLastNetworkSnapshot(snapshot, bytes);
                ApplySnapshotClientRpc(bytes);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!MatchSnapshotChecksum.Matches(snapshot, snapshot.Checksum))
            {
                Debug.LogWarning(
                    $"MatchNetworkAuthority: snapshot checksum mismatch " +
                    $"(got={snapshot.Checksum}, local={MatchSnapshotChecksum.Compute(snapshot)}).");
            }
#endif
            _matchRuntime?.ApplyNetworkSnapshot(snapshot, bytes);
        }

        [ClientRpc]
        void CommandResultClientRpc(byte result, ClientRpcParams rpcParams = default)
        {
            PublishCommandResult((MatchCommandResult)result);
        }

        void SendCommandResult(MatchCommandResult result, ulong targetClientId)
        {
            if (NetworkManager != null && targetClientId == NetworkManager.LocalClientId)
            {
                PublishCommandResult(result);
                return;
            }

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId },
                },
            };
            CommandResultClientRpc((byte)result, rpcParams);
        }

        static void PublishCommandResult(MatchCommandResult result)
        {
            if (result == MatchCommandResult.Ok)
            {
                return;
            }

            CommandResultReceived?.Invoke(result);
        }

        static bool IsCommandsBlocked() =>
            HostMigrationCoordinator.Instance != null
            && HostMigrationCoordinator.Instance.IsPaused;

        int ResolveSenderSlot(RpcParams rpcParams)
        {
            var lobby = NetworkLobbyState.Instance;
            if (lobby == null)
            {
                return -1;
            }

            return lobby.FindClientSlot(rpcParams.Receive.SenderClientId);
        }

        MatchCommandResult TryStartResearchLocal(int slot, int buildingInstanceId, string upgradeId)
        {
            if (IsCommandsBlocked())
            {
                return MatchCommandResult.HostMigrating;
            }

            EnsureRuntime();
            var controller = _matchRuntime?.Controller;
            if (controller == null)
            {
                return MatchCommandResult.NotAllowed;
            }

            var ok = controller.TryStartResearch(slot, buildingInstanceId, upgradeId);
            if (ok)
            {
                return MatchCommandResult.Ok;
            }

            var building = controller.Buildings.GetByInstanceId(buildingInstanceId);
            var buildingValid = building != null
                                && building.IsIntact
                                && building.OwnerSlot == slot;
            var hasSpace = buildingValid && controller.Research.HasSpace(buildingInstanceId);
            var player = slot >= 0 && slot < controller.Players.Count
                ? controller.Players[slot]
                : null;
            var enoughGold = player != null
                             && player.Gold >= MatchEconomyRules.PassiveGoldUpgradeCost;
            return MatchCommandResultRules.ClassifyResearchFailure(buildingValid, hasSpace, enoughGold);
        }

        MatchCommandResult TryHireHeroLocal(int slot, int heroSlot)
        {
            if (IsCommandsBlocked())
            {
                return MatchCommandResult.HostMigrating;
            }

            EnsureRuntime();
            var ok = _matchRuntime?.Controller?.TryHireHero(slot, heroSlot) ?? false;
            return MatchCommandResultRules.FromTrySuccess(ok);
        }

        MatchCommandResult TryDeployHeroLocal(int slot, int buildingInstanceId, int heroSlot)
        {
            if (IsCommandsBlocked())
            {
                return MatchCommandResult.HostMigrating;
            }

            EnsureRuntime();
            var controller = _matchRuntime?.Controller;
            if (controller == null)
            {
                return MatchCommandResult.NotAllowed;
            }

            var player = slot >= 0 && slot < controller.Players.Count
                ? controller.Players[slot]
                : null;
            if (player != null && player.Gold < HeroRules.DeployGold)
            {
                return MatchCommandResult.NotEnoughGold;
            }

            var ok = controller.TryDeployHero(slot, buildingInstanceId, heroSlot);
            return MatchCommandResultRules.FromTrySuccess(ok);
        }

        MatchCommandResult TrySetTowerTargetLocal(int slot, int towerInstanceId, int unitId)
        {
            if (IsCommandsBlocked())
            {
                return MatchCommandResult.HostMigrating;
            }

            EnsureRuntime();
            var ok = _matchRuntime?.Controller?.TrySetTowerTarget(slot, towerInstanceId, unitId) ?? false;
            return ok ? MatchCommandResult.Ok : MatchCommandResult.InvalidTarget;
        }

        MatchCommandResult TryManualCallLocal(int slot, int barracksBuildingInstanceId, UnitRole role)
        {
            if (IsCommandsBlocked())
            {
                return MatchCommandResult.HostMigrating;
            }

            EnsureRuntime();
            var controller = _matchRuntime?.Controller;
            if (controller == null)
            {
                return MatchCommandResult.NotAllowed;
            }

            var player = slot >= 0 && slot < controller.Players.Count
                ? controller.Players[slot]
                : null;
            var cost = BarracksManualCallRules.GetGoldCost(role);
            if (player != null && player.Gold < cost)
            {
                return MatchCommandResult.NotEnoughGold;
            }

            var ok = controller.TryManualCallUnit(slot, barracksBuildingInstanceId, role);
            return MatchCommandResultRules.FromTrySuccess(ok);
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
