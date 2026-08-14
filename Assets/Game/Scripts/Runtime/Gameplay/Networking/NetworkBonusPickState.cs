using Game.Gameplay.Match;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Thin network bridge (PRE-001): relays bonus pick requests to the authoritative
    /// <see cref="MatchController"/>. Picks replicate via snapshot v13 — no per-pick state here.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkBonusPickState : NetworkBehaviour
    {
        public static NetworkBonusPickState Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
                        Instance = this;
            BonusPickNetworkFacade.Register(this);
        }

        public override void OnNetworkDespawn()
        {
                        BonusPickNetworkFacade.Unregister(this);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Sends the local player's bonus pick to the server (host applies locally).</summary>
        public void RequestPick(int bonusSlot)
        {
            if (MatchPauseGate.IsPaused)
            {
                return;
            }

            if (IsServer)
            {
                TryPickLocal(MatchNetworkSession.LocalSlot, bonusSlot);
                return;
            }

            RequestPickServerRpc(bonusSlot);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RequestPickServerRpc(int bonusSlot, RpcParams rpcParams = default)
        {
            var lobby = NetworkLobbyState.Instance;
            if (lobby == null)
            {
                return;
            }

            TryPickLocal(lobby.FindClientSlot(rpcParams.Receive.SenderClientId), bonusSlot);
        }

        void TryPickLocal(int slot, int bonusSlot)
        {
            var controller = MatchRuntime.Current?.Controller;
            if (controller == null)
            {
                return;
            }

            controller.TrySetBonusPick(slot, bonusSlot);
        }
    }
}
