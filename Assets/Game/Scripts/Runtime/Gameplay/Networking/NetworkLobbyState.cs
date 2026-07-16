using System;
using Game.Core;
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
        public FixedString64Bytes DisplayName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref IsOccupied);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref DisplayName);
        }

        public bool Equals(NetworkLobbySlot other) =>
            ClientId == other.ClientId
            && IsOccupied == other.IsOccupied
            && IsReady == other.IsReady
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestReadyServerRpc(bool isReady, RpcParams rpcParams = default)
        {
            SetReady(FindSlotByClientId(rpcParams.Receive.SenderClientId), isReady);
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
            _matchStarted.Value = false;

            for (var i = 0; i < _playerCount.Value; i++)
            {
                _slots.Add(default);
            }

            // Dedicated / listen server: no phantom Host in slot 0 — first real client is host.
            BumpRevision();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId || FindSlotByClientId(clientId) >= 0)
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
                NetworkManager.DisconnectClient(clientId);
                return;
            }

            OccupySlot(slot, clientId, $"Player {slot + 1}");
            BumpRevision();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var slot = FindSlotByClientId(clientId);
            if (slot < 0)
            {
                return;
            }

            _slots[slot] = default;
            BumpRevision();
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

        private void OccupySlot(int slot, ulong clientId, string displayName)
        {
            _slots[slot] = new NetworkLobbySlot
            {
                ClientId = clientId,
                IsOccupied = true,
                IsReady = false,
                DisplayName = new FixedString64Bytes(displayName),
            };
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
