using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Process-wide handoff from session discovery to NGO transport and lobby UI.</summary>
    public static class MatchNetworkSession
    {
        private static MatchSessionHandle s_currentHandle;
        private static bool s_hasHandle;

        public static MatchSessionHandle CurrentHandle => s_currentHandle;
        public static bool HasHandle => s_hasHandle;
        public static bool IsNetworked { get; private set; }
        public static int PlayerCount { get; internal set; }
        public static int LocalSlot { get; internal set; } = -1;
        public static int LobbyRevision => NetworkLobbyState.Instance?.Revision ?? -1;
        public static string RoomCode =>
            NetworkLobbyState.Instance?.RoomCodeValue ?? s_currentHandle.RoomCode ?? string.Empty;
        public static bool MatchStarted => NetworkLobbyState.Instance?.MatchStartedValue ?? false;
        public static int LobbySlotCount => NetworkLobbyState.Instance?.SlotCount ?? 0;
        public static bool HasNetworkLobby => NetworkLobbyState.Instance != null;
        public static bool CanLocalStart => NetworkLobbyState.Instance?.CanLocalStart ?? false;

        public static string TransportConnectFailedMessage =>
            MatchTransportConnectRules.ConnectFailedMessage;

        public static void ApplyHandle(MatchSessionHandle handle)
        {
            s_currentHandle = handle;
            s_hasHandle = true;
            PlayerCount = handle.PlayerCount;
            LocalSlot = handle.LocalPlayerSlot;
            IsNetworked = MatchNetworkEndpoint.TryParse(handle.TransportEndpoint, out var endpoint)
                            && endpoint.IsNetworked;
        }

        public static async UniTask<bool> TryStartTransportAsync(
            float timeoutSeconds = MatchTransportConnectRules.DefaultTimeoutSeconds)
        {
            if (!IsNetworked)
            {
                return true;
            }

            if (!MatchNetworkEndpoint.TryParse(s_currentHandle.TransportEndpoint, out var endpoint))
            {
                throw new FormatException($"Invalid transport endpoint '{s_currentHandle.TransportEndpoint}'.");
            }

            var bootstrap = MatchNetworkBootstrap.Ensure();
            if (bootstrap == null)
            {
                throw new InvalidOperationException("MatchNetworkBootstrap.Ensure returned null.");
            }

            bootstrap.ConfigureEndpoint(
                endpoint.Host,
                endpoint.Port,
                listenAll: IsServerRole(),
                useSecureWebSocket: endpoint.IsSecure && !IsServerRole());

            var manager = bootstrap.NetworkManager;
            if (manager == null || manager.NetworkConfig == null)
            {
                throw new InvalidOperationException(
                    "NetworkManager failed to initialize (NetworkConfig missing). Rebuild WebGL client.");
            }

            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(
                LocalSlot == 0 ? "Host" : "Guest");

            bool started;
            if (IsServerRole())
            {
                started = bootstrap.StartAsServer();
                await UniTask.Yield();
                return started;
            }

            started = bootstrap.StartAsClient();
            if (!started)
            {
                return false;
            }

            return await WaitForClientLobbyAsync(manager, timeoutSeconds);
        }

        public static LobbySlotInfo GetLobbySlot(int slot) =>
            NetworkLobbyState.Instance?.GetSlotInfo(slot) ?? default;

        public static void RequestReady(bool isReady) =>
            NetworkLobbyState.Instance?.RequestReady(isReady);

        public static void RequestFillLocal() =>
            NetworkLobbyState.Instance?.RequestFillLocal();

        public static void RequestStart() =>
            NetworkLobbyState.Instance?.RequestStart();

        public static MatchSetup ToMatchSetup()
        {
            var state = NetworkLobbyState.Instance;
            return state != null
                ? state.ToMatchSetup(LocalSlot < 0 ? 0 : LocalSlot)
                : new MatchSetup(PlayerCount, LocalSlot < 0 ? 0 : LocalSlot);
        }

        public static void Shutdown()
        {
            MatchNetworkBootstrap.Ensure().Shutdown();
            s_currentHandle = default;
            s_hasHandle = false;
            IsNetworked = false;
            PlayerCount = 0;
            LocalSlot = -1;
        }

        private static async UniTask<bool> WaitForClientLobbyAsync(
            NetworkManager manager,
            float timeoutSeconds)
        {
            var disconnected = false;
            void OnDisconnect(ulong clientId)
            {
                if (manager != null && clientId == manager.LocalClientId)
                {
                    disconnected = true;
                }
            }

            manager.OnClientDisconnectCallback += OnDisconnect;
            var startedAt = Time.realtimeSinceStartup;
            try
            {
                while (!MatchTransportConnectRules.HasTimedOut(
                           Time.realtimeSinceStartup - startedAt,
                           timeoutSeconds))
                {
                    if (disconnected
                        || manager == null
                        || (!manager.IsListening && !manager.IsConnectedClient))
                    {
                        Shutdown();
                        return false;
                    }

                    if (MatchTransportConnectRules.IsConnectComplete(
                            manager.IsConnectedClient,
                            NetworkLobbyState.Instance != null))
                    {
                        return true;
                    }

                    await UniTask.Yield();
                }

                Shutdown();
                return false;
            }
            finally
            {
                if (manager != null)
                {
                    manager.OnClientDisconnectCallback -= OnDisconnect;
                }
            }
        }

        private static bool IsServerRole() =>
            string.Equals(
                Environment.GetEnvironmentVariable("BARAKI_ROLE"),
                "server",
                StringComparison.OrdinalIgnoreCase);
    }
}
