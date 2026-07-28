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
            if (IsServer)
            {
                EnsureSession(MatchNetworkSession.PlayerCount);
            }

            PlaytestLog.Info(
                "RacePick",
                "Spawned",
                ("server", IsServer),
                ("client", IsClient),
                ("players", _playerCount.Value),
                ("clientId", NetworkManager != null ? (long)NetworkManager.LocalClientId : -1L));
            NotifyChanged();
            SessionFlowTracker.NotifyChanged();
        }

        public override void OnNetworkDespawn()
        {
            PlaytestLog.Info(
                "RacePick",
                "Despawned",
                ("server", IsServer),
                ("client", IsClient));
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
            if (!IsServer || !MatchModeRules.IsValidPlayerCount(playerCount))
            {
                return;
            }

            if (_playerCount.Value == playerCount &&
                _racePicks.Count == playerCount &&
                !_matchSimStarted.Value)
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

            FillLocalStandInPicks();
            NotifyChanged();
        }

        public bool RequestPick(string raceId)
        {
            if (IsServer)
            {
                return ApplyPick(ResolveLocalSlot(), raceId);
            }

            RequestPickServerRpc(raceId);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPickServerRpc(string raceId, RpcParams rpcParams = default)
        {
            var lobby = NetworkLobbyState.Instance;
            if (lobby == null)
            {
                PlaytestLog.Warn(
                    "RacePick",
                    "RpcNoLobby",
                    ("race", raceId),
                    ("sender", (long)rpcParams.Receive.SenderClientId));
                return;
            }

            EnsureSession(lobby.PlayerCount);

            var slot = lobby.FindClientSlot(rpcParams.Receive.SenderClientId);
            PlaytestLog.Info(
                "RacePick",
                "Submit",
                ("race", raceId),
                ("sender", (long)rpcParams.Receive.SenderClientId),
                ("slot", slot));
            ApplyPick(slot, raceId);
        }

        private bool ApplyPick(int slot, string raceId)
        {
            var rejectReason = string.Empty;
            if (!IsServer || _matchSimStarted.Value || slot < 0 || slot >= _racePicks.Count)
            {
                rejectReason = !IsServer
                    ? "not-server"
                    : _matchSimStarted.Value
                        ? "match-started"
                        : "slot-out-of-range";
                PlaytestLog.Warn(
                    "RacePick",
                    "Reject",
                    ("slot", slot),
                    ("race", raceId),
                    ("reason", rejectReason));
                return false;
            }

            var picks = ToMutablePickArray();
            if (!RacePickNetworkRules.TryApplyPick(picks, slot, raceId))
            {
                PlaytestLog.Warn(
                    "RacePick",
                    "RejectRules",
                    ("slot", slot),
                    ("race", raceId));
                return false;
            }

            _racePicks[slot] = new FixedString32Bytes(picks[slot]);
            FillLocalStandInPicks();
            NotifyChanged();

            var picksAfterApply = ToMutablePickArray();
            var isComplete = RacePickNetworkRules.IsComplete(picksAfterApply);
            PlaytestLog.Info(
                "RacePick",
                "Applied",
                ("slot", slot),
                ("race", raceId),
                ("complete", isComplete),
                ("picks", string.Join(",", picksAfterApply)));

            if (isComplete)
            {
                BeginMatchOnServer(picksAfterApply);
            }

            return true;
        }

        private void FillLocalStandInPicks()
        {
            var lobby = NetworkLobbyState.Instance;
            if (!IsServer || lobby == null || _racePicks.Count == 0)
            {
                return;
            }

            var localStandInSlots = new bool[_racePicks.Count];
            for (var slot = 0; slot < localStandInSlots.Length; slot++)
            {
                localStandInSlots[slot] = lobby.IsLocalStandInSlot(slot);
            }

            var picks = ToMutablePickArray();
            if (!RacePickNetworkRules.FillLocalStandInPicks(picks, localStandInSlots))
            {
                return;
            }

            for (var slot = 0; slot < picks.Length; slot++)
            {
                _racePicks[slot] = new FixedString32Bytes(picks[slot]);
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
            PlaytestLog.Info(
                "Match",
                "Begin",
                ("players", _playerCount.Value),
                ("slot", localSlot),
                ("races", string.Join(",", raceIds)));

            var runtime = FindAnyObjectByType<MatchRuntime>();
            runtime?.StartMatch(raceIds, localSlot);

            BeginMatchClientRpc(EncodeRaceIds(raceIds));
            NotifyChanged();
            SessionFlowTracker.NotifyChanged();
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
            PlaytestLog.Info(
                "Match",
                "Begin",
                ("slot", localSlot),
                ("players", _playerCount.Value),
                ("races", string.Join(",", raceIds)));
            var setup = new MatchSetup(_playerCount.Value, localSlot, raceIds);
            GameSession.UpdateActiveSetup(setup);

            var runtime = FindAnyObjectByType<MatchRuntime>();
            runtime?.StartMatch(raceIds, localSlot);
            NotifyChanged();
            SessionFlowTracker.NotifyChanged();
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
