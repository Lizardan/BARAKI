using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Gameplay.Networking
{
    public readonly struct MatchSessionHandle
    {
        public MatchSessionHandle(
            string roomCode,
            int playerCount,
            int localPlayerSlot,
            string transportEndpoint,
            bool isListenHost = false,
            string relayJoinCode = null,
            string lobbyId = null)
        {
            RoomCode = roomCode ?? string.Empty;
            PlayerCount = playerCount;
            LocalPlayerSlot = localPlayerSlot;
            TransportEndpoint = transportEndpoint ?? string.Empty;
            IsListenHost = isListenHost;
            RelayJoinCode = relayJoinCode ?? string.Empty;
            LobbyId = lobbyId ?? string.Empty;
        }

        public string RoomCode { get; }
        public int PlayerCount { get; }
        public int LocalPlayerSlot { get; }
        public string TransportEndpoint { get; }
        public bool IsListenHost { get; }
        public string RelayJoinCode { get; }
        public string LobbyId { get; }
    }

    public readonly struct CreateMatchRequest
    {
        public CreateMatchRequest(int playerCount, string displayName, string instanceId = null)
        {
            PlayerCount = playerCount;
            DisplayName = displayName ?? "Player";
            InstanceId = instanceId;
        }

        public int PlayerCount { get; }
        public string DisplayName { get; }
        public string InstanceId { get; }
    }

    public readonly struct JoinMatchRequest
    {
        public JoinMatchRequest(string roomOrInstanceId, string displayName)
        {
            RoomOrInstanceId = roomOrInstanceId ?? string.Empty;
            DisplayName = displayName ?? "Player";
        }

        public string RoomOrInstanceId { get; }
        public string DisplayName { get; }
    }

    public interface IMatchSessionBackend
    {
        UniTask<MatchSessionHandle> CreateAsync(CreateMatchRequest request);
        UniTask<MatchSessionHandle> JoinAsync(JoinMatchRequest request);
    }

    /// <summary>In-process lobby registry for Editor offline smoke.</summary>
    public sealed class LocalDevSessionBackend : IMatchSessionBackend
    {
        public UniTask<MatchSessionHandle> CreateAsync(CreateMatchRequest request)
        {
            if (!MatchModeRules.IsModeSelectable(request.PlayerCount))
            {
                throw new InvalidOperationException($"Mode N={request.PlayerCount} is not selectable in MVP.");
            }

            LocalMatchRegistry.Clear();
            var lobby = LocalMatchRegistry.Create(request.PlayerCount, request.DisplayName);
            var handle = new MatchSessionHandle(
                lobby.RoomCode,
                lobby.PlayerCount,
                localPlayerSlot: 0,
                transportEndpoint: $"local://{lobby.RoomCode}");
            return UniTask.FromResult(handle);
        }

        public UniTask<MatchSessionHandle> JoinAsync(JoinMatchRequest request)
        {
            var lobby = LocalMatchRegistry.Join(request.RoomOrInstanceId, request.DisplayName);
            var slot = LocalMatchRegistry.LocalPlayerSlot
                       ?? throw new InvalidOperationException("Local slot missing after join.");
            var handle = new MatchSessionHandle(
                lobby.RoomCode,
                lobby.PlayerCount,
                slot,
                transportEndpoint: $"local://{lobby.RoomCode}");
            return UniTask.FromResult(handle);
        }
    }

    public static class MatchSessionService
    {
        static IMatchSessionBackend s_backend = new LocalDevSessionBackend();

        public static IMatchSessionBackend Backend
        {
            get => s_backend;
            set => s_backend = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static void UseLocalDev() => s_backend = new LocalDevSessionBackend();

        public static void UseUnityLobbyRelay() =>
            s_backend = new UnityLobbyRelaySessionBackend();
    }
}
