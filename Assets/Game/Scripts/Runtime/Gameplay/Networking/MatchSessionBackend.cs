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
            string transportEndpoint)
        {
            RoomCode = roomCode ?? string.Empty;
            PlayerCount = playerCount;
            LocalPlayerSlot = localPlayerSlot;
            TransportEndpoint = transportEndpoint ?? string.Empty;
        }

        public string RoomCode { get; }
        public int PlayerCount { get; }
        public int LocalPlayerSlot { get; }
        public string TransportEndpoint { get; }
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

    /// <summary>
    /// Discord-ready session API. LocalDev now; Discord Activity matchmaker later.
    /// </summary>
    public interface IMatchSessionBackend
    {
        UniTask<MatchSessionHandle> CreateAsync(CreateMatchRequest request);
        UniTask<MatchSessionHandle> JoinAsync(JoinMatchRequest request);
    }

    /// <summary>In-process lobby registry. NGO/WSS attach later; same Create/Join contract.</summary>
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

    /// <summary>
    /// PC smoke-test backend. Lobby discovery is local, while slot ownership is assigned by
    /// NetworkLobbyState after NGO connects. Cross-process Join deliberately does not query
    /// LocalMatchRegistry because that registry is process-local.
    /// </summary>
    public sealed class NetDevSessionBackend : IMatchSessionBackend
    {
        readonly ushort _port;

        public NetDevSessionBackend(ushort port = MatchNetworkEndpoint.DefaultPort)
        {
            _port = port == 0 ? MatchNetworkEndpoint.DefaultPort : port;
        }

        public UniTask<MatchSessionHandle> CreateAsync(CreateMatchRequest request)
        {
            if (!MatchModeRules.IsModeSelectable(request.PlayerCount))
            {
                throw new InvalidOperationException($"Mode N={request.PlayerCount} is not selectable in MVP.");
            }

            LocalMatchRegistry.Clear();
            var lobby = LocalMatchRegistry.Create(request.PlayerCount, request.DisplayName);
            return UniTask.FromResult(new MatchSessionHandle(
                lobby.RoomCode,
                lobby.PlayerCount,
                localPlayerSlot: 0,
                transportEndpoint: $"ws://127.0.0.1:{_port}"));
        }

        public UniTask<MatchSessionHandle> JoinAsync(JoinMatchRequest request)
        {
            var roomCode = string.IsNullOrWhiteSpace(request.RoomOrInstanceId)
                ? "NET"
                : request.RoomOrInstanceId.Trim().ToUpperInvariant();
            var playerCount = LocalMatchRegistry.Active?.PlayerCount
                              ?? MatchSetup.DefaultPlayerCount;
            return UniTask.FromResult(new MatchSessionHandle(
                roomCode,
                playerCount,
                localPlayerSlot: -1,
                transportEndpoint: $"ws://127.0.0.1:{_port}"));
        }
    }

    /// <summary>
    /// Discord Activity matchmaker client. Falls back to LocalDev when instance id is missing.
    /// </summary>
    public sealed class DiscordActivitySessionBackend : IMatchSessionBackend
    {
        readonly string _matchmakerBaseUrl;
        readonly LocalDevSessionBackend _fallback = new();

        public DiscordActivitySessionBackend(string matchmakerBaseUrl = null)
        {
            var fromEnv = Environment.GetEnvironmentVariable("BARAKI_MATCHMAKER_URL");
            _matchmakerBaseUrl = TrimSlash(
                !string.IsNullOrWhiteSpace(matchmakerBaseUrl)
                    ? matchmakerBaseUrl
                    : !string.IsNullOrWhiteSpace(fromEnv)
                        ? fromEnv
                        : "/api");
        }

        public async UniTask<MatchSessionHandle> CreateAsync(CreateMatchRequest request)
        {
            var instanceId = ResolveInstanceId(request.InstanceId);
            if (string.IsNullOrEmpty(instanceId))
            {
                return await _fallback.CreateAsync(request);
            }

            return await EnsureAsync(instanceId, request.PlayerCount, request.DisplayName);
        }

        public async UniTask<MatchSessionHandle> JoinAsync(JoinMatchRequest request)
        {
            var instanceId = ResolveInstanceId(request.RoomOrInstanceId);
            if (string.IsNullOrEmpty(instanceId))
            {
                return await _fallback.JoinAsync(request);
            }

            var playerCount = MatchSetup.DefaultPlayerCount;
            if (DiscordActivityBridge.TryGetSession(out var session) && session.PlayerCount > 0)
            {
                playerCount = session.PlayerCount;
            }

            return await EnsureAsync(instanceId, playerCount, request.DisplayName);
        }

        async UniTask<MatchSessionHandle> EnsureAsync(string instanceId, int playerCount, string displayName)
        {
            if (!MatchModeRules.IsModeSelectable(playerCount))
            {
                playerCount = MatchSetup.DefaultPlayerCount;
            }

            if (DiscordActivityBridge.TryGetSession(out var cached)
                && !string.IsNullOrEmpty(cached.WssUrl)
                && (string.IsNullOrEmpty(cached.InstanceId)
                    || string.Equals(cached.InstanceId, instanceId, StringComparison.Ordinal)))
            {
                return new MatchSessionHandle(
                    string.IsNullOrEmpty(cached.RoomCode) ? instanceId : cached.RoomCode,
                    cached.PlayerCount > 0 ? cached.PlayerCount : playerCount,
                    cached.Slot >= 0 ? cached.Slot : 0,
                    cached.WssUrl);
            }

            var body =
                "{\"instance_id\":\"" + Escape(instanceId) + "\"," +
                "\"player_count\":" + playerCount + "," +
                "\"discord_user_id\":\"" + Escape(displayName) + "\"," +
                "\"display_name\":\"" + Escape(displayName) + "\"}";

            var url = _matchmakerBaseUrl + "/v1/match/ensure";
            using var req = new UnityEngine.Networking.UnityWebRequest(
                url,
                UnityEngine.Networking.UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    "Matchmaker ensure failed: " + req.responseCode + " " + req.error + " " +
                    req.downloadHandler?.text);
            }

            var parsed = MatchmakerEnsureResponse.Parse(req.downloadHandler.text);
            DiscordActivityBridge.CacheFromEnsure(instanceId, parsed);
            return new MatchSessionHandle(
                string.IsNullOrEmpty(parsed.RoomCode) ? instanceId : parsed.RoomCode,
                parsed.PlayerCount > 0 ? parsed.PlayerCount : playerCount,
                parsed.Slot,
                parsed.WssUrl);
        }

        static string ResolveInstanceId(string requested)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                return requested.Trim();
            }

            return DiscordActivityBridge.TryGetSession(out var session)
                ? session.InstanceId
                : null;
        }

        static string TrimSlash(string value) =>
            string.IsNullOrWhiteSpace(value) ? "/api" : value.Trim().TrimEnd('/');

        static string Escape(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
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

        public static void UseNetDev(ushort port = MatchNetworkEndpoint.DefaultPort) =>
            s_backend = new NetDevSessionBackend(port);

        public static void UseDiscordStub() => s_backend = new DiscordActivitySessionBackend();

        public static void UseDiscord(string matchmakerBaseUrl = null) =>
            s_backend = new DiscordActivitySessionBackend(matchmakerBaseUrl);
    }
}
