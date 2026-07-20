using System;
using Game.Core;
using Game.Gameplay.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    public struct NetworkLobbySlot : INetworkSerializable, IEquatable<NetworkLobbySlot>
    {
        public ulong ClientId;
        public bool IsOccupied;
        public bool IsReady;
        public bool IsReserved;
        public FixedString64Bytes DisplayName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref IsOccupied);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsReserved);
            serializer.SerializeValue(ref DisplayName);
        }

        public bool Equals(NetworkLobbySlot other) =>
            ClientId == other.ClientId
            && IsOccupied == other.IsOccupied
            && IsReady == other.IsReady
            && IsReserved == other.IsReserved
            && DisplayName.Equals(other.DisplayName);
    }

    /// <summary>Server-authoritative fixed-size lobby replicated through NGO.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkLobbyState : NetworkBehaviour, IReadOnlyLobbySlots
    {
        private readonly NetworkVariable<int> _playerCount = new();
        private readonly NetworkVariable<FixedString64Bytes> _roomCode = new();
        private readonly NetworkVariable<bool> _matchStarted = new();
        private readonly NetworkVariable<int> _revision = new();
        private readonly NetworkList<NetworkLobbySlot> _slots = new();
        private readonly float[] _disconnectAtRealtime = new float[NetworkLobbySlotRules.MaxSlots];
        private readonly bool[] _hasDisconnectTimer = new bool[NetworkLobbySlotRules.MaxSlots];

        public static NetworkLobbyState Instance { get; private set; }

        public event Action Changed;

        public int PlayerCount => _playerCount.Value;
        public int SlotCount => _slots.Count;
        public int Revision => _revision.Value;
        public string RoomCodeValue => _roomCode.Value.ToString();
        public bool MatchStartedValue => _matchStarted.Value;
        /// <summary>
        /// Designated host is lobby slot 0 (listen-server host). NGO host
        /// does not occupy a player slot, so Start is client-side via ServerRpc.
        /// </summary>
        public bool CanLocalStart =>
            NetworkLobbySlotRules.CanDesignatedHostStart(
                MatchNetworkSession.LocalSlot,
                _matchStarted.Value,
                LobbyReadyRules.CanHostStart(this));

        public override void OnNetworkSpawn()
        {
            Instance = this;
            SubscribeToChanges();

            if (IsServer)
            {
                InitializeServerState();
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }

            ResolveLocalSlot();
            if (IsClient && _matchStarted.Value)
            {
                MatchNetworkSession.ClaimReconnectIfNeeded();
            }

            NotifyChanged();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromChanges();
            if (NetworkManager != null && IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public LobbySlotInfo GetSlot(int index) => GetSlotInfo(index);

        public LobbySlotInfo GetSlotInfo(int index)
        {
            if (index < 0 || index >= _slots.Count)
            {
                return default;
            }

            var slot = _slots[index];
            return new LobbySlotInfo(slot.IsOccupied, slot.IsReady, slot.DisplayName.ToString());
        }

        public int FindClientSlot(ulong clientId) => FindSlotByClientId(clientId);

        public MatchSetup ToMatchSetup(int localSlot) =>
            new(PlayerCount, localSlot);

        public void RequestReady(bool isReady)
        {
            if (IsServer)
            {
                SetReady(MatchNetworkSession.LocalSlot, isReady);
                return;
            }

            RequestReadyServerRpc(isReady);
        }

        public void RequestFillLocal()
        {
            if (IsServer)
            {
                FillLocal(MatchNetworkSession.LocalSlot);
                return;
            }

            RequestFillLocalServerRpc();
        }

        public void RequestStart()
        {
            if (IsServer)
            {
                TryStart(MatchNetworkSession.LocalSlot);
                return;
            }

            RequestStartServerRpc();
        }

        public void RequestReconnect(string sessionToken)
        {
            if (IsServer)
            {
                TryClaimReconnect(MatchNetworkSession.LocalSlot >= 0
                        ? MatchNetworkSession.LocalSlot
                        : -1,
                    NetworkManager.LocalClientId,
                    sessionToken);
                return;
            }

            RequestReconnectServerRpc(sessionToken ?? string.Empty);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestReadyServerRpc(bool isReady, RpcParams rpcParams = default)
        {
            SetReady(FindSlotByClientId(rpcParams.Receive.SenderClientId), isReady);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestReconnectServerRpc(string sessionToken, RpcParams rpcParams = default)
        {
            TryClaimReconnect(-1, rpcParams.Receive.SenderClientId, sessionToken);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestFillLocalServerRpc(RpcParams rpcParams = default)
        {
            FillLocal(FindSlotByClientId(rpcParams.Receive.SenderClientId));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestStartServerRpc(RpcParams rpcParams = default)
        {
            TryStart(FindSlotByClientId(rpcParams.Receive.SenderClientId));
        }

        private void InitializeServerState()
        {
            if (_slots.Count > 0)
            {
                return;
            }

            var requestedCount = MatchNetworkSession.PlayerCount;
            _playerCount.Value = MatchModeRules.IsValidPlayerCount(requestedCount)
                ? requestedCount
                : MatchSetup.DefaultPlayerCount;
            _roomCode.Value = new FixedString64Bytes(
                string.IsNullOrWhiteSpace(MatchNetworkSession.CurrentHandle.RoomCode)
                    ? "SERVER"
                    : MatchNetworkSession.CurrentHandle.RoomCode);
            _matchStarted.Value = HostMigrationSession.IsRebinding;

            for (var i = 0; i < _playerCount.Value; i++)
            {
                _slots.Add(default);
            }

            if (HostMigrationSession.IsRebinding && NetworkManager.IsHost)
            {
                SeatMigratedListenHost();
                MatchNetworkBootstrap.Ensure().EnsureMatchAuthority();
            }
            else if (NetworkLobbySlotRules.ShouldSeatListenHostOnServerInit(NetworkManager.IsHost))
            {
                SeatListenHost();
            }

            BumpRevision();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (FindSlotByClientId(clientId) >= 0)
            {
                return;
            }

            var occupied = new bool[_slots.Count];
            for (var i = 0; i < _slots.Count; i++)
            {
                occupied[i] = _slots[i].IsOccupied;
            }

            var slot = NetworkLobbySlotRules.FindNextFreeSlot(occupied, _playerCount.Value);
            if (slot < 0)
            {
                // Mid-match: keep connection so client can ClaimReconnect with session token.
                if (_matchStarted.Value)
                {
                    return;
                }

                NetworkManager.DisconnectClient(clientId);
                return;
            }

            OccupySlot(slot, clientId, ResolveClientDisplayName(clientId));
            BumpRevision();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var slot = FindSlotByClientId(clientId);
            if (slot < 0)
            {
                return;
            }

            if (DisconnectGraceRules.ShouldClearSlotImmediately(_matchStarted.Value))
            {
                ClearSlot(slot);
                BumpRevision();
                return;
            }

            ReserveSlotForGrace(slot);
            if (DisconnectGraceRules.IsHostSlotDisconnect(slot))
            {
                BeginHostMigrationIfNeeded(slot);
            }

            BumpRevision();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !_matchStarted.Value)
            {
                return;
            }

            TickDisconnectGrace();
        }

        private void SetReady(int slot, bool isReady)
        {
            if (_matchStarted.Value || slot < 0 || slot >= _slots.Count || !_slots[slot].IsOccupied)
            {
                return;
            }

            var value = _slots[slot];
            value.IsReady = isReady;
            _slots[slot] = value;
            BumpRevision();
        }

        private void FillLocal(int senderSlot)
        {
            if (!NetworkLobbySlotRules.IsHostSlot(senderSlot) || _matchStarted.Value)
            {
                return;
            }

            for (var slot = 0; slot < _slots.Count; slot++)
            {
                if (_slots[slot].IsOccupied)
                {
                    continue;
                }

                OccupySlot(slot, ulong.MaxValue - (ulong)slot, $"Player {slot + 1}");
                var value = _slots[slot];
                value.IsReady = true;
                _slots[slot] = value;
            }

            BumpRevision();
        }

        private void TryStart(int senderSlot)
        {
            if (!NetworkLobbySlotRules.IsHostSlot(senderSlot)
                || _matchStarted.Value
                || !LobbyReadyRules.CanHostStart(this))
            {
                return;
            }

            _matchStarted.Value = true;
            BumpRevision();
            MatchNetworkBootstrap.Ensure().EnsureMatchAuthority();
        }

        private void SeatListenHost()
        {
            if (NetworkManager == null
                || !NetworkManager.IsHost
                || _slots.Count == 0
                || _slots[NetworkLobbySlotRules.HostSlot].IsOccupied)
            {
                return;
            }

            var displayName = ResolveClientDisplayName(NetworkManager.LocalClientId);
            OccupySlot(NetworkLobbySlotRules.HostSlot, NetworkManager.LocalClientId, displayName);
            MatchNetworkSession.LocalSlot = NetworkLobbySlotRules.HostSlot;
        }

        /// <summary>
        /// After host migration the new NGO host keeps their original player slot
        /// (may be ≠ 0). Previous host slot stays reserved for eliminate-on-resume.
        /// </summary>
        void SeatMigratedListenHost()
        {
            if (NetworkManager == null || !NetworkManager.IsHost || _slots.Count == 0)
            {
                return;
            }

            var slot = HostMigrationSession.DesignatedHostSlot;
            if (slot < 0 || slot >= _slots.Count)
            {
                slot = MatchNetworkSession.LocalSlot;
            }

            if (slot < 0 || slot >= _slots.Count)
            {
                return;
            }

            var displayName = ResolveClientDisplayName(NetworkManager.LocalClientId);
            OccupySlot(slot, NetworkManager.LocalClientId, displayName);
            MatchNetworkSession.LocalSlot = slot;
            MatchNetworkSession.ListenHostSlot = slot;
        }

        private void OccupySlot(int slot, ulong clientId, string displayName)
        {
            _slots[slot] = new NetworkLobbySlot
            {
                ClientId = clientId,
                IsOccupied = true,
                IsReady = false,
                IsReserved = false,
                DisplayName = new FixedString64Bytes(displayName),
            };
            _hasDisconnectTimer[slot] = false;
            _disconnectAtRealtime[slot] = 0f;
        }

        void ClearSlot(int slot)
        {
            _slots[slot] = default;
            _hasDisconnectTimer[slot] = false;
            _disconnectAtRealtime[slot] = 0f;
        }

        void ReserveSlotForGrace(int slot)
        {
            var value = _slots[slot];
            value.IsReserved = true;
            value.IsReady = false;
            value.ClientId = ulong.MaxValue;
            _slots[slot] = value;
            _hasDisconnectTimer[slot] = true;
            _disconnectAtRealtime[slot] = Time.realtimeSinceStartup;
        }

        void TryClaimReconnect(int preferredSlot, ulong clientId, string sessionToken)
        {
            if (!_matchStarted.Value
                || !PlayerReconnectRules.TryParseSessionToken(sessionToken, out var matchId, out var slot))
            {
                return;
            }

            if (!string.Equals(matchId, RoomCodeValue, StringComparison.Ordinal)
                && !string.Equals(matchId, MatchNetworkSession.RoomCode, StringComparison.Ordinal))
            {
                return;
            }

            if (slot < 0 || slot >= _slots.Count || !_slots[slot].IsReserved)
            {
                return;
            }

            if (preferredSlot >= 0 && preferredSlot != slot)
            {
                return;
            }

            var seconds = _hasDisconnectTimer[slot]
                ? Time.realtimeSinceStartup - _disconnectAtRealtime[slot]
                : 0f;
            if (!PlayerReconnectRules.CanReconnect(true, true, seconds))
            {
                return;
            }

            var displayName = _slots[slot].DisplayName.ToString();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = ResolveClientDisplayName(clientId);
            }

            OccupySlot(slot, clientId, displayName);
            BumpRevision();
            NotifyChanged();
        }

        void TickDisconnectGrace()
        {
            for (var slot = 0; slot < _slots.Count; slot++)
            {
                if (!_hasDisconnectTimer[slot] || !_slots[slot].IsReserved)
                {
                    continue;
                }

                var elapsed = Time.realtimeSinceStartup - _disconnectAtRealtime[slot];
                if (!DisconnectGraceRules.ShouldEliminateAfterGrace(elapsed))
                {
                    continue;
                }

                var runtime = FindAnyObjectByType<MatchRuntime>();
                runtime?.Controller?.TryEliminateForDisconnect(slot);
                ClearSlot(slot);
                BumpRevision();
            }
        }

        void BeginHostMigrationIfNeeded(int previousHostSlot)
        {
            var coordinator = HostMigrationCoordinator.Instance;
            if (coordinator == null)
            {
                EnsureHostMigrationCoordinator();
                coordinator = HostMigrationCoordinator.Instance;
            }

            if (coordinator == null)
            {
                return;
            }

            var occupied = new bool[_slots.Count];
            for (var i = 0; i < _slots.Count; i++)
            {
                occupied[i] = _slots[i].IsOccupied;
            }

            coordinator.BeginHostLost(previousHostSlot, occupied, matchInProgress: true);
            if (coordinator.Phase == HostMigrationRules.MigrationPhase.Aborted)
            {
                return;
            }

            coordinator.BeginStateTransferFromMatch();
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

        private static string ResolveClientDisplayName(ulong clientId)
        {
            if (NetworkManager.Singleton != null
                && clientId == NetworkManager.Singleton.LocalClientId
                && !string.IsNullOrWhiteSpace(PlayerProfileService.DisplayName))
            {
                return PlayerProfileService.DisplayName;
            }

            return $"Player {clientId}";
        }

        private int FindSlotByClientId(ulong clientId)
        {
            for (var slot = 0; slot < _slots.Count; slot++)
            {
                if (_slots[slot].IsOccupied && _slots[slot].ClientId == clientId)
                {
                    return slot;
                }
            }

            return -1;
        }

        private void ResolveLocalSlot()
        {
            if (NetworkManager == null || !NetworkManager.IsClient)
            {
                return;
            }

            var slot = FindSlotByClientId(NetworkManager.LocalClientId);
            if (slot >= 0)
            {
                MatchNetworkSession.LocalSlot = slot;
                MatchNetworkSession.PlayerCount = _playerCount.Value;
            }
        }

        private void SubscribeToChanges()
        {
            _slots.OnListChanged += OnSlotsChanged;
            _playerCount.OnValueChanged += OnPlayerCountChanged;
            _roomCode.OnValueChanged += OnRoomCodeChanged;
            _matchStarted.OnValueChanged += OnMatchStartedChanged;
            _revision.OnValueChanged += OnRevisionChanged;
        }

        private void UnsubscribeFromChanges()
        {
            _slots.OnListChanged -= OnSlotsChanged;
            _playerCount.OnValueChanged -= OnPlayerCountChanged;
            _roomCode.OnValueChanged -= OnRoomCodeChanged;
            _matchStarted.OnValueChanged -= OnMatchStartedChanged;
            _revision.OnValueChanged -= OnRevisionChanged;
        }

        private void OnSlotsChanged(NetworkListEvent<NetworkLobbySlot> changeEvent)
        {
            ResolveLocalSlot();
            NotifyChanged();
        }

        private void OnPlayerCountChanged(int previous, int current)
        {
            MatchNetworkSession.PlayerCount = current;
            NotifyChanged();
        }

        private void OnRoomCodeChanged(FixedString64Bytes previous, FixedString64Bytes current) =>
            NotifyChanged();

        private void OnMatchStartedChanged(bool previous, bool current) =>
            NotifyChanged();

        private void OnRevisionChanged(int previous, int current) =>
            NotifyChanged();

        private void BumpRevision()
        {
            _revision.Value++;
            NotifyChanged();
        }

        private void NotifyChanged() => Changed?.Invoke();
    }
}
