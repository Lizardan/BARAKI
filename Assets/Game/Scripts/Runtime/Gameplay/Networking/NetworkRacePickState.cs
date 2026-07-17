using System;
using System.Text;
using Game.Core;
using Game.Gameplay.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Server-authoritative replicated race picks before match simulation starts.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRacePickState : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _playerCount = new();
        private readonly NetworkVariable<bool> _matchSimStarted = new();
        private readonly NetworkList<FixedString32Bytes> _racePicks = new();

        public static NetworkRacePickState Instance { get; private set; }

        public event Action Changed;

        public int PlayerCount => _playerCount.Value;
        public bool MatchSimStarted => _matchSimStarted.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _racePicks.OnListChanged += OnRacePicksChanged;
            _matchSimStarted.OnValueChanged += OnMatchSimStartedChanged;
            _playerCount.OnValueChanged += OnPlayerCountChanged;
            NotifyChanged();
        }

        public override void OnNetworkDespawn()
        {
            _racePicks.OnListChanged -= OnRacePicksChanged;
            _matchSimStarted.OnValueChanged -= OnMatchSimStartedChanged;
            _playerCount.OnValueChanged -= OnPlayerCountChanged;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool HasPick(int slot)
        {
            if (slot < 0 || slot >= _racePicks.Count)
            {
                return false;
            }

            return !string.IsNullOrEmpty(_racePicks[slot].ToString());
        }

        public void EnsureSession(int playerCount)
        {
            if (!IsServer || playerCount < MatchModeRules.MinPlayers || _playerCount.Value > 0)
            {
                return;
            }

            _playerCount.Value = playerCount;
            _matchSimStarted.Value = false;
            _racePicks.Clear();
            for (var slot = 0; slot < playerCount; slot++)
            {
                _racePicks.Add(default);
            }

            NotifyChanged();
        }

        public void RequestPick(string raceId)
        {
            if (IsServer)
            {
                ApplyPick(ResolveLocalSlot(), raceId);
                return;
            }

            RequestPickServerRpc(raceId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPickServerRpc(string raceId, RpcParams rpcParams = default)
        {
            var lobby = NetworkLobbyState.Instance;
            if (lobby == null)
            {
                return;
            }

            var slot = lobby.FindClientSlot(rpcParams.Receive.SenderClientId);
            ApplyPick(slot, raceId);
        }

        private void ApplyPick(int slot, string raceId)
        {
            if (!IsServer || _matchSimStarted.Value || slot < 0 || slot >= _racePicks.Count)
            {
                return;
            }

            var picks = ToMutablePickArray();
            if (!RacePickNetworkRules.TryApplyPick(picks, slot, raceId))
            {
                return;
            }

            _racePicks[slot] = new FixedString32Bytes(picks[slot]);
            NotifyChanged();

            if (RacePickNetworkRules.IsComplete(picks))
            {
                BeginMatchOnServer(picks);
            }
        }

        private void BeginMatchOnServer(string[] picks)
        {
            if (!IsServer || _matchSimStarted.Value)
            {
                return;
            }

            _matchSimStarted.Value = true;
            var raceIds = RacePickNetworkRules.ToRaceIdsArray(picks);
            var localSlot = ResolveLocalSlot();
            var setup = new MatchSetup(_playerCount.Value, localSlot, raceIds);
            GameSession.UpdateActiveSetup(setup);

            var runtime = FindAnyObjectByType<MatchRuntime>();
            runtime?.StartMatch(raceIds, localSlot);

            BeginMatchClientRpc(EncodeRaceIds(raceIds));
            NotifyChanged();
        }

        [ClientRpc]
        public void BeginMatchClientRpc(byte[] payload)
        {
            if (IsServer)
            {
                return;
            }

            var raceIds = DecodeRaceIds(payload);
            var localSlot = ResolveLocalSlot();
            var setup = new MatchSetup(_playerCount.Value, localSlot, raceIds);
            GameSession.UpdateActiveSetup(setup);

            var runtime = FindAnyObjectByType<MatchRuntime>();
            runtime?.StartMatch(raceIds, localSlot);
            NotifyChanged();
        }

        private static int ResolveLocalSlot()
        {
            var slot = MatchNetworkSession.LocalSlot;
            return slot < 0 ? 0 : slot;
        }

        private string[] ToMutablePickArray()
        {
            var picks = new string[_racePicks.Count];
            for (var i = 0; i < _racePicks.Count; i++)
            {
                picks[i] = _racePicks[i].ToString();
            }

            return picks;
        }

        private static byte[] EncodeRaceIds(string[] raceIds)
        {
            return Encoding.UTF8.GetBytes(string.Join('\n', raceIds));
        }

        private static string[] DecodeRaceIds(byte[] payload)
        {
            var text = Encoding.UTF8.GetString(payload);
            return text.Split('\n');
        }

        private void OnRacePicksChanged(NetworkListEvent<FixedString32Bytes> changeEvent) =>
            NotifyChanged();

        private void OnMatchSimStartedChanged(bool previous, bool current) =>
            NotifyChanged();

        private void OnPlayerCountChanged(int previous, int current) =>
            NotifyChanged();

        private void NotifyChanged()
        {
            Changed?.Invoke();
            MatchNetworkSession.NotifyRacePickChanged();
        }
    }
}
