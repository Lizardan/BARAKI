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
            // #region agent log
            DebugSessionLog.Write(
                "NetworkRacePickState.cs:OnNetworkSpawn",
                "race pick state spawned",
                "H6",
                ("isServer", IsServer),
                ("isClient", IsClient),
                ("isSpawned", IsSpawned),
                ("localClientId", NetworkManager != null ? (long)NetworkManager.LocalClientId : -1L),
                ("playerCount", _playerCount.Value),
                ("pickCount", _racePicks.Count),
                ("activeScene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name),
                ("dontDestroy", gameObject.scene.name == "DontDestroyOnLoad"));
            // #endregion
            if (IsServer)
            {
                EnsureSession(MatchNetworkSession.PlayerCount);
            }

            NotifyChanged();
        }

        public override void OnNetworkDespawn()
        {
            // #region agent log
            DebugSessionLog.Write(
                "NetworkRacePickState.cs:OnNetworkDespawn",
                "race pick state despawned",
                "H6",
                ("isServer", IsServer),
                ("isClient", IsClient),
                ("wasInstance", Instance == this),
                ("activeScene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            // #endregion
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
                // #region agent log
                DebugSessionLog.Write(
                    "NetworkRacePickState.cs:RequestPickServerRpc",
                    "server rpc rejected because lobby state is missing",
                    "H2",
                    ("raceId", raceId),
                    ("senderClientId", (long)rpcParams.Receive.SenderClientId));
                // #endregion
                return;
            }

            EnsureSession(lobby.PlayerCount);

            var slot = lobby.FindClientSlot(rpcParams.Receive.SenderClientId);
            // #region agent log
            DebugSessionLog.Write(
                "NetworkRacePickState.cs:RequestPickServerRpc",
                "server rpc resolved sender slot",
                "H2,H3",
                ("raceId", raceId),
                ("senderClientId", (long)rpcParams.Receive.SenderClientId),
                ("resolvedSlot", slot),
                ("lobbyPlayerCount", lobby.PlayerCount),
                ("pickCountAfterEnsure", _racePicks.Count),
                ("currentPicks", PicksDebugString()));
            // #endregion
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
                // #region agent log
                DebugSessionLog.Write(
                    "NetworkRacePickState.cs:ApplyPick",
                    "server race pick rejected before rules",
                    "H2,H3,H4",
                    ("slot", slot),
                    ("raceId", raceId),
                    ("rejectReason", rejectReason),
                    ("pickCount", _racePicks.Count),
                    ("currentPicks", PicksDebugString()));
                // #endregion
                return false;
            }

            var picks = ToMutablePickArray();
            if (!RacePickNetworkRules.TryApplyPick(picks, slot, raceId))
            {
                // #region agent log
                DebugSessionLog.Write(
                    "NetworkRacePickState.cs:ApplyPick",
                    "server race pick rejected by rules",
                    "H4",
                    ("slot", slot),
                    ("raceId", raceId),
                    ("pickCount", _racePicks.Count),
                    ("currentPicks", string.Join(",", picks)));
                // #endregion
                return false;
            }

            _racePicks[slot] = new FixedString32Bytes(picks[slot]);
            FillLocalStandInPicks();
            NotifyChanged();

            var picksAfterApply = ToMutablePickArray();
            var isComplete = RacePickNetworkRules.IsComplete(picksAfterApply);
            // #region agent log
            DebugSessionLog.Write(
                "NetworkRacePickState.cs:ApplyPick",
                "server race pick applied",
                "H3,H4",
                ("slot", slot),
                ("raceId", raceId),
                ("pickCount", _racePicks.Count),
                ("picksAfterApply", string.Join(",", picksAfterApply)),
                ("isComplete", isComplete));
            // #endregion

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
            // #region agent log
            DebugSessionLog.Write(
                "NetworkRacePickState.cs:BeginMatchOnServer",
                "server begins match after complete race picks",
                "H4",
                ("playerCount", _playerCount.Value),
                ("localSlot", localSlot),
                ("raceIds", string.Join(",", raceIds)));
            // #endregion

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
            // #region agent log
            DebugSessionLog.Write(
                "NetworkRacePickState.cs:BeginMatchClientRpc",
                "client received begin match rpc",
                "H4,H7",
                ("localSlot", localSlot),
                ("playerCount", _playerCount.Value),
                ("raceIds", string.Join(",", raceIds)),
                ("hasMatchRuntime", FindAnyObjectByType<MatchRuntime>() != null),
                ("activeScene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            // #endregion
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

        private string PicksDebugString() =>
            string.Join(",", ToMutablePickArray());

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
