using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Production Windows session: Unity Lobby join code + Relay + NGO host-as-server.
    /// </summary>
    public sealed class UnityLobbyRelaySessionBackend : IMatchSessionBackend
    {
        public const string DataRelayJoinCode = "RelayJoinCode";
        public const string DataPlayerCount = "PlayerCount";
        public const string DataHostPlayerId = "HostPlayerId";

        public async UniTask<MatchSessionHandle> CreateAsync(CreateMatchRequest request)
        {
            if (!MatchModeRules.IsModeSelectable(request.PlayerCount))
            {
                throw new InvalidOperationException($"Mode N={request.PlayerCount} is not selectable.");
            }

            await UnityServicesBootstrap.EnsureInitializedAsync();

            var maxConnections = Math.Max(1, request.PlayerCount - 1);
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            MatchRelayTransportState.SetHost(allocation, relayJoinCode);

            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = true,
                Player = BuildPlayer(request.DisplayName),
                Data = new Dictionary<string, DataObject>
                {
                    {
                        DataRelayJoinCode,
                        new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                    },
                    {
                        DataPlayerCount,
                        new DataObject(
                            DataObject.VisibilityOptions.Public,
                            request.PlayerCount.ToString())
                    },
                    {
                        DataHostPlayerId,
                        new DataObject(
                            DataObject.VisibilityOptions.Member,
                            UnityServicesBootstrap.PlayerId)
                    },
                },
            };

            var lobby = await LobbyService.Instance.CreateLobbyAsync(
                "BARAKI",
                request.PlayerCount,
                lobbyOptions);

            Debug.Log(
                $"UnityLobbyRelay: created lobby code={lobby.LobbyCode} relay={relayJoinCode}");

            return new MatchSessionHandle(
                lobby.LobbyCode,
                request.PlayerCount,
                localPlayerSlot: 0,
                transportEndpoint: MatchNetworkEndpoint.FormatRelayHost(relayJoinCode),
                isListenHost: true,
                relayJoinCode: relayJoinCode,
                lobbyId: lobby.Id);
        }

        public async UniTask<MatchSessionHandle> JoinAsync(JoinMatchRequest request)
        {
            var code = request.RoomOrInstanceId?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException("Lobby join code is required.");
            }

            await UnityServicesBootstrap.EnsureInitializedAsync();

            var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(
                code.ToUpperInvariant(),
                new JoinLobbyByCodeOptions { Player = BuildPlayer(request.DisplayName) });

            if (!lobby.Data.TryGetValue(DataRelayJoinCode, out var relayData)
                || string.IsNullOrEmpty(relayData.Value))
            {
                throw new InvalidOperationException("Lobby is missing Relay join code.");
            }

            var playerCount = MatchSetup.DefaultPlayerCount;
            if (lobby.Data.TryGetValue(DataPlayerCount, out var countData)
                && int.TryParse(countData.Value, out var parsed)
                && parsed > 0)
            {
                playerCount = parsed;
            }

            var relayJoinCode = relayData.Value;
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            MatchRelayTransportState.SetClient(joinAllocation, relayJoinCode);

            Debug.Log($"UnityLobbyRelay: joined lobby code={lobby.LobbyCode} relay={relayJoinCode}");

            return new MatchSessionHandle(
                lobby.LobbyCode,
                playerCount,
                localPlayerSlot: -1,
                transportEndpoint: MatchNetworkEndpoint.FormatRelayClient(relayJoinCode),
                isListenHost: false,
                relayJoinCode: relayJoinCode,
                lobbyId: lobby.Id);
        }

        /// <summary>
        /// New listen-host: allocate fresh Relay and publish join code on the existing lobby.
        /// Preserves <paramref name="localPlayerSlot"/> (may be ≠ 0).
        /// </summary>
        public async UniTask<MatchSessionHandle> MigrateListenHostAsync(
            string lobbyId,
            string roomCode,
            int playerCount,
            int localPlayerSlot,
            string displayName)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new InvalidOperationException("Lobby id required for host migration.");
            }

            await UnityServicesBootstrap.EnsureInitializedAsync();

            var maxConnections = Math.Max(1, playerCount - 1);
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            MatchRelayTransportState.SetHost(allocation, relayJoinCode);

            await LobbyService.Instance.UpdateLobbyAsync(
                lobbyId,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            DataRelayJoinCode,
                            new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                        },
                        {
                            DataHostPlayerId,
                            new DataObject(
                                DataObject.VisibilityOptions.Member,
                                UnityServicesBootstrap.PlayerId)
                        },
                    },
                });

            Debug.Log(
                $"UnityLobbyRelay: migrated host lobby={roomCode} relay={relayJoinCode} slot={localPlayerSlot}");

            return new MatchSessionHandle(
                roomCode,
                playerCount,
                localPlayerSlot,
                MatchNetworkEndpoint.FormatRelayHost(relayJoinCode),
                isListenHost: true,
                relayJoinCode: relayJoinCode,
                lobbyId: lobbyId);
        }

        /// <summary>Poll lobby until Relay join code changes, then join the new allocation.</summary>
        public async UniTask<MatchSessionHandle> WaitForMigratedRelayAsync(
            string lobbyId,
            string roomCode,
            string previousRelayJoinCode,
            int playerCount,
            int localPlayerSlot,
            string displayName,
            float timeoutSeconds = 20f)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new InvalidOperationException("Lobby id required for migrated rejoin.");
            }

            await UnityServicesBootstrap.EnsureInitializedAsync();

            var startedAt = Time.realtimeSinceStartup;
            string relayJoinCode = null;
            while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
            {
                var lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                if (lobby.Data != null
                    && lobby.Data.TryGetValue(DataRelayJoinCode, out var relayData)
                    && !string.IsNullOrEmpty(relayData.Value)
                    && !string.Equals(
                        relayData.Value,
                        previousRelayJoinCode,
                        StringComparison.Ordinal))
                {
                    relayJoinCode = relayData.Value;
                    break;
                }

                await UniTask.Delay(400);
            }

            if (string.IsNullOrEmpty(relayJoinCode))
            {
                throw new TimeoutException("Timed out waiting for migrated Relay join code.");
            }

            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            MatchRelayTransportState.SetClient(joinAllocation, relayJoinCode);

            Debug.Log(
                $"UnityLobbyRelay: rejoined migrated host lobby={roomCode} relay={relayJoinCode} slot={localPlayerSlot}");

            return new MatchSessionHandle(
                roomCode,
                playerCount,
                localPlayerSlot,
                MatchNetworkEndpoint.FormatRelayClient(relayJoinCode),
                isListenHost: false,
                relayJoinCode: relayJoinCode,
                lobbyId: lobbyId);
        }

        static Player BuildPlayer(string displayName) =>
            new Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {
                        "DisplayName",
                        new PlayerDataObject(
                            PlayerDataObject.VisibilityOptions.Member,
                            displayName ?? "Player")
                    },
                },
            };
    }
}
