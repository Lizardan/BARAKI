using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Match;
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
        static HostMigrationSlotSnapshot[] s_cachedRoster;

        public static MatchSessionHandle CurrentHandle => s_currentHandle;
        public static bool HasHandle => s_hasHandle;
        public static bool IsNetworked { get; private set; }
        public static bool IsRejoiningMatch { get; set; }
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
            MatchLobbyHeartbeat.Ensure().Bind(handle.LobbyId);
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

        public static void RequestKickDisconnected(int slot)
        {
            if (slot < 0)
            {
                return;
            }

            var lobby = NetworkLobbyState.Instance;
            if (lobby != null)
            {
                lobby.RequestKickDisconnected(slot);
                return;
            }

            HostMigrationSession.QueueKick(slot);
        }

        public static event Action NetworkRacePickChanged;

        internal static void NotifyRacePickChanged() => NetworkRacePickChanged?.Invoke();

        public static void EnsureRacePickSession(int playerCount) =>
            NetworkRacePickState.Instance?.EnsureSession(playerCount);

        public static bool RequestRacePick(string raceId)
        {
            var state = NetworkRacePickState.Instance;
            var lobby = NetworkLobbyState.Instance;
            var nm = NetworkManager.Singleton;
            if (state == null)
            {
                PlaytestLog.Warn(
                    "RacePick",
                    "RequestMissingState",
                    ("race", raceId),
                    ("slot", LocalSlot),
                    ("hasLobby", lobby != null));
                return false;
            }

            var accepted = state.RequestPick(raceId);
            PlaytestLog.Info(
                "RacePick",
                "Request",
                ("race", raceId),
                ("slot", LocalSlot),
                ("accepted", accepted),
                ("server", nm != null && nm.IsServer),
                ("client", nm != null && nm.IsClient));
            return accepted;
        }

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
            IsRejoiningMatch = false;
            PlayerCount = 0;
            LocalSlot = -1;
            ListenHostSlot = NetworkLobbySlotRules.HostSlot;
            s_cachedRoster = null;
            HostMigrationSession.Clear();
            MatchLobbyHeartbeat.Ensure().Bind(null);
            MatchPauseGate.SetDisconnectHoldPaused(false);
            // Leaving a cleared session must not leave stale joinable lobby presence.
            SessionFlowTracker.NotifyChanged();
        }

        /// <summary>Drop NGO/Relay only; keep room/slot for host migration rebind.</summary>
        public static void ShutdownTransportKeepingSession() => Shutdown(clearSession: false);

        /// <summary>Leave match networking and reset session (results → menu/lobby).</summary>
        public static void LeaveMatch()
        {
            var matchEnded = MatchRuntime.Current?.Controller?.Phase == MatchPhase.End;
            Shutdown();
            GameSession.Reset();
            if (matchEnded)
            {
                PendingMatchReconnectStore.Clear();
            }
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
            PlaytestLog.Info("HostMigration", "LocalDevResume");
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

            PlaytestLog.Info("HostMigration", "LocalDevClientResume");
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

            return PlayerReconnectRules.BuildSessionToken(
                room,
                LocalSlot,
                UnityServicesBootstrap.PlayerId);
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

        public static void CacheLobbyRoster(HostMigrationSlotSnapshot[] snapshot)
        {
            s_cachedRoster = snapshot;
        }

        public static HostMigrationSlotSnapshot[] CopyCachedRoster()
        {
            if (s_cachedRoster == null || s_cachedRoster.Length == 0)
            {
                return Array.Empty<HostMigrationSlotSnapshot>();
            }

            var copy = new HostMigrationSlotSnapshot[s_cachedRoster.Length];
            Array.Copy(s_cachedRoster, copy, s_cachedRoster.Length);
            return copy;
        }

        public static bool[] GetEligibleHostSlots()
        {
            var roster = s_cachedRoster;
            if (roster == null || roster.Length == 0)
            {
                return Array.Empty<bool>();
            }

            var occupied = new bool[roster.Length];
            var reserved = new bool[roster.Length];
            for (var i = 0; i < roster.Length; i++)
            {
                occupied[i] = roster[i].IsOccupied;
                reserved[i] = roster[i].IsReserved;
            }

            return HostMigrationRules.BuildEligibleOccupied(occupied, reserved);
        }

        public static string GetCachedSlotName(int slot)
        {
            if (s_cachedRoster == null || slot < 0 || slot >= s_cachedRoster.Length)
            {
                return string.Empty;
            }

            return s_cachedRoster[slot].DisplayName;
        }

        public static async UniTask<bool> TryReturnToPendingMatchAsync()
        {
            if (!PendingMatchReconnectStore.TryLoadActive(out var pending)
                || !PendingMatchReconnectRules.CanAttemptRejoin(
                    true,
                    pending.RoomCode,
                    pending.Slot,
                    pending.SessionToken,
                    pending.SavedUtcTicks,
                    DateTime.UtcNow.Ticks))
            {
                PendingMatchReconnectStore.Clear();
                return false;
            }

            var displayName = string.IsNullOrWhiteSpace(PlayerProfileService.DisplayName)
                ? "Player"
                : PlayerProfileService.DisplayName;
            var handle = await MatchSessionService.Backend.JoinAsync(
                new JoinMatchRequest(pending.RoomCode, displayName));
            ApplyHandle(new MatchSessionHandle(
                handle.RoomCode,
                pending.PlayerCount > 0 ? pending.PlayerCount : handle.PlayerCount,
                pending.Slot,
                handle.TransportEndpoint,
                isListenHost: false,
                relayJoinCode: handle.RelayJoinCode,
                lobbyId: handle.LobbyId));
            LocalSlot = pending.Slot;
            IsRejoiningMatch = true;
            if (!await TryStartTransportAsync())
            {
                IsRejoiningMatch = false;
                return false;
            }

            ClaimReconnectIfNeeded();
            return true;
        }

        static string BuildConnectionPayload(bool isHost)
        {
            var room = !string.IsNullOrWhiteSpace(RoomCode)
                ? RoomCode
                : s_currentHandle.RoomCode;
            if (!string.IsNullOrWhiteSpace(room)
                && LocalSlot >= 0
                && (MatchStarted || HostMigrationSession.IsRebinding || IsRejoiningMatch))
            {
                return MatchConnectionPayloadRules.BuildReconnect(
                    PlayerReconnectRules.BuildSessionToken(
                        room,
                        LocalSlot,
                        UnityServicesBootstrap.PlayerId));
            }

            var isDesignatedHost = isHost
                || (HostMigrationSession.IsRebinding
                    && LocalSlot == HostMigrationSession.DesignatedHostSlot);
            return MatchConnectionPayloadRules.BuildInitial(
                isDesignatedHost,
                PlayerProfileService.DisplayName,
                UnityServicesBootstrap.PlayerId);
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

