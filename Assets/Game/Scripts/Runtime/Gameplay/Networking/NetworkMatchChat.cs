using System;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>In-match text chat over NGO (ServerRpc → ClientRpc).</summary>
    public sealed class NetworkMatchChat : NetworkBehaviour
    {
        public static NetworkMatchChat Instance { get; private set; }

        public static event Action<string, string> MessageReceived;

        readonly System.Collections.Generic.Dictionary<ulong, float> _lastSendByClient = new();

        public override void OnNetworkSpawn()
        {
            Instance = this;
            MatchChatNetworkFacade.Register(SendLocal);
        }

        public override void OnNetworkDespawn()
        {
            MatchChatNetworkFacade.Unregister(SendLocal);
            if (Instance == this)
            {
                Instance = null;
            }

            _lastSendByClient.Clear();
        }

        public void SendLocal(string text)
        {
            if (!NetworkMatchChatRules.TryNormalize(text, out var message))
            {
                return;
            }

            if (IsServer)
            {
                Broadcast(ResolveDisplayName(NetworkManager.LocalClientId), message);
                return;
            }

            SendChatServerRpc(message);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void SendChatServerRpc(string text, RpcParams rpcParams = default)
        {
            if (!NetworkMatchChatRules.TryNormalize(text, out var message))
            {
                return;
            }

            var sender = rpcParams.Receive.SenderClientId;
            var now = Time.realtimeSinceStartup;
            if (_lastSendByClient.TryGetValue(sender, out var last)
                && !NetworkMatchChatRules.CanSend(now, last))
            {
                return;
            }

            _lastSendByClient[sender] = now;
            Broadcast(ResolveDisplayName(sender), message);
        }

        void Broadcast(string displayName, string message)
        {
            ReceiveChatClientRpc(displayName ?? "Игрок", message);
        }

        [ClientRpc]
        void ReceiveChatClientRpc(string displayName, string message)
        {
            MessageReceived?.Invoke(displayName, message);
            MatchChatNetworkFacade.RaiseReceived(displayName, message);
        }

        static string ResolveDisplayName(ulong clientId)
        {
            var lobby = NetworkLobbyState.Instance;
            if (lobby != null)
            {
                var slot = lobby.FindClientSlot(clientId);
                if (slot >= 0)
                {
                    var info = lobby.GetSlotInfo(slot);
                    if (!string.IsNullOrWhiteSpace(info.DisplayName))
                    {
                        return GameChatRules.FormatDisplayName(info.DisplayName);
                    }
                }
            }

            return "Игрок";
        }
    }
}
