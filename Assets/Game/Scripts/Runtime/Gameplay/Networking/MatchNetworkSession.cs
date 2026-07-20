using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using Unity.Netcode;
using Unity.Services.Relay.Models;
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
        /// <summary>Player slot of the current listen-host (starts at 0; updates after migration).</summary>
        public static int ListenHostSlot { get; set; }
        public static int LobbyRevision => NetworkLobbyState.Instance?.Revision ?? -1;
        public static string RoomCode =>
            NetworkLobbyState.Instance?.RoomCodeValue ?? s_currentHandle.RoomCode ?? string.Empty;
        public static bool MatchStarted => NetworkLobbyState.Instance?.MatchStartedValue ?? false;
        public static int LobbySlotCount => NetworkLobbyState.Instance?.SlotCount ?? 0;
        public static bool HasNetworkLobby => NetworkLobbyState.Instance != null;
        public static bool CanLocalStart => NetworkLobbyState.Instance?.CanLocalStart ?? false;

        public static string TransportConnectFailedMessage =>
            MatchTransportConnectRules.ConnectFailedMessage;

        /// <summary>Short endpoint hint for lobby "connecting" UI (no secrets).</summary>
        public static string TransportEndpointHint
        {
            get
            {
                if (!s_hasHandle || string.IsNullOrEmpty(s_currentHandle.TransportEndpoint))
                {
                    return "нет endpoint";
                }

                if (!MatchNetworkEndpoint.TryParse(s_currentHandle.TransportEndpoint, out var endpoint))
                {
                    return s_currentHandle.TransportEndpoint;
                }

                if (endpoint.IsLocal)
                {
                    return $"local/{endpoint.LocalCode}";
                }

                if (endpoint.IsRelay)
                {
                    return endpoint.IsRelayHost ? "relay-host" : "relay-client";
                }

                return $"{endpoint.Host}:{endpoint.Port}";
            }
        }

        public static void ApplyHandle(MatchSessionHandle handle)
        {
            s_currentHandle = handle;
            s_hasHandle = true;
            PlayerCount = handle.PlayerCount;
            LocalSlot = handle.LocalPlayerSlot;
            if (!HostMigrationSession.IsRebinding)
            {
                ListenHostSlot = NetworkLobbySlotRules.HostSlot;
            }

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

            var manager = bootstrap.NetworkManager;
            if (manager == null || manager.NetworkConfig == null)
            {
                throw new InvalidOperationException(
                    "NetworkManager failed to initialize (NetworkConfig missing).");
            }

            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(
                BuildConnectionPayload(s_currentHandle.IsListenHost || LocalSlot == 0));

            if (endpoint.IsRelay)
            {
                return await StartRelayTransportAsync(bootstrap, endpoint, timeoutSeconds);
            }

            bootstrap.ConfigureEndpoint(
                endpoint.Host,
                endpoint.Port,
                listenAll: s_currentHandle.IsListenHost,
                useSecureWebSocket: endpoint.IsSecure && !s_currentHandle.IsListenHost);

            if (s_currentHandle.IsListenHost)
            {
                var started = bootstrap.StartAsHost();
                await UniTask.Yield();
                return started;
            }

            var clientStarted = bootstrap.StartAsClient();
            if (!clientStarted)
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

        public static bool IsNetworkRacePickActive => IsNetworked && NetworkRacePickState.Instance != null;

        public static event Action NetworkRacePickChanged;

        internal static void NotifyRacePickChanged() => NetworkRacePickChanged?.Invoke();

        public static void EnsureRacePickSession(int playerCount) =>
            NetworkRacePickState.Instance?.EnsureSession(playerCount);

        public static void RequestRacePick(string raceId) =>
            NetworkRacePickState.Instance?.RequestPick(raceId);

        public static bool HasRacePick(int slot) =>
            NetworkRacePickState.Instance?.HasPick(slot) ?? false;

        public static bool NetworkMatchSimStarted =>
            NetworkRacePickState.Instance?.MatchSimStarted ?? false;

        public static MatchSetup ToMatchSetup()
        {
            var state = NetworkLobbyState.Instance;
            return state != null
                ? state.ToMatchSetup(LocalSlot < 0 ? 0 : LocalSlot)
                : new MatchSetup(PlayerCount, LocalSlot < 0 ? 0 : LocalSlot);
        }

        public static void Shutdown(bool clearSession = true)
        {
            MatchNetworkBootstrap.Ensure().Shutdown();
            MatchRelayTransportState.Clear();
            if (!clearSession)
            {
                return;
            }

            s_currentHandle = default;
            s_hasHandle = false;
            IsNetworked = false;
            PlayerCount = 0;
            LocalSlot = -1;
            ListenHostSlot = NetworkLobbySlotRules.HostSlot;
            HostMigrationSession.Clear();
        }

        /// <summary>Drop NGO/Relay only; keep room/slot for host migration rebind.</summary>
        public static void ShutdownTransportKeepingSession() => Shutdown(clearSession: false);

        /// <summary>Leave match networking and reset session (results → menu/lobby).</summary>
        public static void LeaveMatch()
        {
            Shutdown();
            GameSession.Reset();
        }

        /// <summary>Designated host: new Relay allocation + lobby Data update + StartAsHost.</summary>
        public static async UniTask<bool> TryMigrateAsListenHostAsync()
        {
            if (!s_hasHandle)
            {
                return false;
            }

            var lobbyId = s_currentHandle.LobbyId;
            var playerCount = PlayerCount;
            var localSlot = LocalSlot;
            var roomCode = s_currentHandle.RoomCode;
            var displayName = PlayerProfileService.DisplayName;

            if (MatchSessionService.Backend is UnityLobbyRelaySessionBackend relayBackend
                && !string.IsNullOrEmpty(lobbyId))
            {
                var handle = await relayBackend.MigrateListenHostAsync(
                    lobbyId,
                    roomCode,
                    playerCount,
                    localSlot,
                    displayName);
                ApplyHandle(handle);
                ListenHostSlot = localSlot;
                return await TryStartTransportAsync();
            }

            // LocalDev / non-Relay: apply last-good in-process (no NGO rebind).
            Debug.Log("HostMigration: LocalDev path — resume without Relay rebind.");
            return true;
        }

        /// <summary>Non-host peers: poll lobby for new Relay join code and StartAsClient.</summary>
        public static async UniTask<bool> TryRejoinMigratedHostAsync()
        {
            if (!s_hasHandle)
            {
                return false;
            }

            var lobbyId = s_currentHandle.LobbyId;
            var localSlot = LocalSlot;
            var roomCode = s_currentHandle.RoomCode;
            var displayName = PlayerProfileService.DisplayName;
            var previousRelay = HostMigrationSession.PreviousRelayJoinCode;

            if (MatchSessionService.Backend is UnityLobbyRelaySessionBackend relayBackend
                && !string.IsNullOrEmpty(lobbyId))
            {
                var handle = await relayBackend.WaitForMigratedRelayAsync(
                    lobbyId,
                    roomCode,
                    previousRelay,
                    PlayerCount,
                    localSlot,
                    displayName);
                ApplyHandle(handle);
                return await TryStartTransportAsync();
            }

            Debug.Log("HostMigration: LocalDev client path — resume without Relay rebind.");
            return true;
        }

        public static string BuildReconnectToken()
        {
            var room = !string.IsNullOrWhiteSpace(RoomCode)
                ? RoomCode
                : s_currentHandle.RoomCode;
            if (LocalSlot < 0 || string.IsNullOrWhiteSpace(room))
            {
                return string.Empty;
            }

            return PlayerReconnectRules.BuildSessionToken(room, LocalSlot);
        }

        public static void ClaimReconnectIfNeeded()
        {
            var lobby = NetworkLobbyState.Instance;
            if (lobby == null || !lobby.MatchStartedValue)
            {
                return;
            }

            var token = BuildReconnectToken();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            lobby.RequestReconnect(token);
        }

        static string BuildConnectionPayload(bool isHost)
        {
            var room = !string.IsNullOrWhiteSpace(RoomCode)
                ? RoomCode
                : s_currentHandle.RoomCode;
            if (!string.IsNullOrWhiteSpace(room)
                && LocalSlot >= 0
                && (MatchStarted || HostMigrationSession.IsRebinding))
            {
                return $"reconnect:{PlayerReconnectRules.BuildSessionToken(room, LocalSlot)}";
            }

            return isHost
                   || (HostMigrationSession.IsRebinding
                       && LocalSlot == HostMigrationSession.DesignatedHostSlot)
                ? "Host"
                : "Guest";
        }

        private static async UniTask<bool> StartRelayTransportAsync(
            MatchNetworkBootstrap bootstrap,
            MatchNetworkEndpoint endpoint,
            float timeoutSeconds)
        {
            if (endpoint.IsRelayHost)
            {
                if (!MatchRelayTransportState.HasHostAllocation)
                {
                    throw new InvalidOperationException("Relay host allocation missing.");
                }

                var relayData = AllocationUtils.ToRelayServerData(
                    MatchRelayTransportState.HostAllocation,
                    "dtls");
                bootstrap.ConfigureRelay(relayData);
                var started = bootstrap.StartAsHost();
                await UniTask.Yield();
                return started;
            }

            if (!MatchRelayTransportState.HasClientAllocation)
            {
                throw new InvalidOperationException("Relay client allocation missing.");
            }

            var clientRelayData = AllocationUtils.ToRelayServerData(
                MatchRelayTransportState.ClientAllocation,
                "dtls");
            bootstrap.ConfigureRelay(clientRelayData);
            var clientStarted = bootstrap.StartAsClient();
            if (!clientStarted)
            {
                return false;
            }

            return await WaitForClientLobbyAsync(bootstrap.NetworkManager, timeoutSeconds);
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
                        Shutdown(clearSession: !HostMigrationSession.IsRebinding);
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

                Shutdown(clearSession: !HostMigrationSession.IsRebinding);
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

    }
}

