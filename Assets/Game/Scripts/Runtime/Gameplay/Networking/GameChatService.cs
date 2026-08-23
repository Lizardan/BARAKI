using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Gameplay.Networking
{
    public readonly struct GameChatMessage
    {
        public GameChatMessage(
            MenuChatChannel channel,
            string senderPlayerId,
            string senderDisplayName,
            string text,
            DateTime receivedAt,
            bool fromSelf)
        {
            Channel = channel;
            SenderPlayerId = senderPlayerId ?? string.Empty;
            SenderDisplayName = GameChatRules.FormatDisplayName(senderDisplayName);
            Text = text ?? string.Empty;
            ReceivedAt = receivedAt;
            FromSelf = fromSelf;
        }

        public MenuChatChannel Channel { get; }
        public string SenderPlayerId { get; }
        public string SenderDisplayName { get; }
        public string Text { get; }
        public DateTime ReceivedAt { get; }
        public bool FromSelf { get; }
    }

    public readonly struct GameChatDirectMessage
    {
        public GameChatDirectMessage(
            string otherPlayerId,
            string senderPlayerId,
            string senderDisplayName,
            string text,
            DateTime receivedAt,
            bool fromSelf)
        {
            OtherPlayerId = otherPlayerId ?? string.Empty;
            SenderPlayerId = senderPlayerId ?? string.Empty;
            SenderDisplayName = GameChatRules.FormatDisplayName(senderDisplayName);
            Text = text ?? string.Empty;
            ReceivedAt = receivedAt;
            FromSelf = fromSelf;
        }

        public string OtherPlayerId { get; }
        public string SenderPlayerId { get; }
        public string SenderDisplayName { get; }
        public string Text { get; }
        public DateTime ReceivedAt { get; }
        public bool FromSelf { get; }
    }

    /// <summary>Menu chat over Cloudflare Worker (global / friends feed / DM). Match chat stays on NGO.</summary>
    public static class GameChatService
    {
        const string PrefsApiBase = "baraki.chat.apiBase";
        const string PrefsApiKey = "baraki.chat.apiKey";
        const float PollSeconds = 2.5f;

        static bool s_ready;
        static bool s_pollRunning;
        static string s_apiBase = string.Empty;
        static string s_apiKey = string.Empty;
        static string s_afterGlobal = string.Empty;
        static string s_afterFriends = string.Empty;
        static readonly Dictionary<string, string> s_afterDm = new(StringComparer.Ordinal);
        static readonly List<GameChatMessage> s_globalHistory = new();
        static readonly List<GameChatMessage> s_friendsHistory = new();
        static readonly Dictionary<string, List<GameChatDirectMessage>> s_dmByPeer =
            new(StringComparer.Ordinal);
        static readonly HashSet<string> s_seenIds = new(StringComparer.Ordinal);

        public static event Action ReadyChanged;
        public static event Action<GameChatMessage> ChannelMessageReceived;
        public static event Action<GameChatDirectMessage> DirectMessageReceived;

        public static bool IsReady => s_ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetForPlaySession()
        {
            s_ready = false;
            s_pollRunning = false;
            s_apiBase = string.Empty;
            s_apiKey = string.Empty;
            s_afterGlobal = string.Empty;
            s_afterFriends = string.Empty;
            s_afterDm.Clear();
            s_globalHistory.Clear();
            s_friendsHistory.Clear();
            s_dmByPeer.Clear();
            s_seenIds.Clear();
        }

        public static IReadOnlyList<GameChatMessage> GetChannelHistory(MenuChatChannel channel) =>
            channel == MenuChatChannel.FriendsFeed ? s_friendsHistory : s_globalHistory;

        public static IReadOnlyList<GameChatDirectMessage> GetDirectHistory(string otherPlayerId)
        {
            var id = GameChatRules.NormalizePlayerId(otherPlayerId);
            if (id.Length == 0 || !s_dmByPeer.TryGetValue(id, out var list))
            {
                return Array.Empty<GameChatDirectMessage>();
            }

            return list;
        }

        /// <summary>Register a DM peer so the poll loop fetches history before the first send.</summary>
        public static void EnsureDirectPeer(string otherPlayerId)
        {
            var id = GameChatRules.NormalizePlayerId(otherPlayerId);
            if (id.Length == 0)
            {
                return;
            }

            if (!s_dmByPeer.ContainsKey(id))
            {
                s_dmByPeer[id] = new List<GameChatDirectMessage>();
            }
        }

        public static async UniTask EnsureInitializedAsync()
        {
            await UnityServicesBootstrap.EnsureInitializedAsync();
            if (!UnityServicesBootstrap.IsReady)
            {
                SetReady(false);
                return;
            }

            s_apiBase = ResolveApiBase();
            s_apiKey = PlayerPrefs.GetString(PrefsApiKey, GameChatRules.DefaultApiKey);
            if (string.IsNullOrWhiteSpace(s_apiBase))
            {
                Debug.LogWarning("GameChatService: chat API base URL is empty (set PlayerPrefs baraki.chat.apiBase).");
                SetReady(false);
                return;
            }

            try
            {
                FriendsHubService.HubChanged -= OnFriendsHubChanged;
                FriendsHubService.HubChanged += OnFriendsHubChanged;
                await PostSessionAsync();
                await PollOnceAsync();
                SetReady(true);
                EnsurePollLoop();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GameChatService: init failed: {ex.Message}");
                SetReady(false);
            }
        }

        public static async UniTask SyncOnlineFriendFeedsAsync()
        {
            if (!IsReady)
            {
                return;
            }

            try
            {
                await PostSessionAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GameChatService: session sync failed: {ex.Message}");
            }
        }

        public static async UniTaskVoid SendChannelAsync(MenuChatChannel channel, string text)
        {
            if (!GameChatRules.TrySanitizeMessage(text, out var message) || !IsReady)
            {
                return;
            }

            var path = channel == MenuChatChannel.FriendsFeed
                ? "/v1/channels/friends"
                : "/v1/channels/global";
            try
            {
                await RequestJsonAsync("POST", path, $"{{\"text\":{JsonString(message)}}}");
                await PollOnceAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GameChatService: send channel failed: {ex.Message}");
            }
        }

        public static async UniTaskVoid SendDirectAsync(string targetPlayerId, string text)
        {
            var target = GameChatRules.NormalizePlayerId(targetPlayerId);
            if (target.Length == 0
                || !GameChatRules.TrySanitizeMessage(text, out var message)
                || !IsReady)
            {
                return;
            }

            if (!s_dmByPeer.ContainsKey(target))
            {
                s_dmByPeer[target] = new List<GameChatDirectMessage>();
            }

            try
            {
                var path = $"/v1/dm/{UnityWebRequest.EscapeURL(target)}";
                await RequestJsonAsync("POST", path, $"{{\"text\":{JsonString(message)}}}");
                await PollOnceAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GameChatService: send DM failed: {ex.Message}");
            }
        }

        static void OnFriendsHubChanged()
        {
            SyncOnlineFriendFeedsAsync().Forget();
        }

        static void EnsurePollLoop()
        {
            if (s_pollRunning)
            {
                return;
            }

            s_pollRunning = true;
            PollLoopAsync().Forget();
        }

        static async UniTaskVoid PollLoopAsync()
        {
            while (s_ready)
            {
                try
                {
                    await PollOnceAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"GameChatService: poll failed: {ex.Message}");
                }

                await UniTask.Delay(TimeSpan.FromSeconds(PollSeconds), ignoreTimeScale: true);
            }

            s_pollRunning = false;
        }

        static async UniTask PollOnceAsync()
        {
            var globalQuery = string.IsNullOrEmpty(s_afterGlobal)
                ? "/v1/channels/global"
                : $"/v1/channels/global?after={UnityWebRequest.EscapeURL(s_afterGlobal)}";
            var friendsQuery = string.IsNullOrEmpty(s_afterFriends)
                ? "/v1/channels/friends"
                : $"/v1/channels/friends?after={UnityWebRequest.EscapeURL(s_afterFriends)}";

            var globalJson = await RequestJsonAsync("GET", globalQuery, null);
            IngestChannelPayload(globalJson, MenuChatChannel.Global, bumpAfter: true);
            var friendsJson = await RequestJsonAsync("GET", friendsQuery, null);
            IngestChannelPayload(friendsJson, MenuChatChannel.FriendsFeed, bumpAfter: true);

            foreach (var peer in new List<string>(s_dmByPeer.Keys))
            {
                s_afterDm.TryGetValue(peer, out var after);
                var path = string.IsNullOrEmpty(after)
                    ? $"/v1/dm/{UnityWebRequest.EscapeURL(peer)}"
                    : $"/v1/dm/{UnityWebRequest.EscapeURL(peer)}?after={UnityWebRequest.EscapeURL(after)}";
                var dmJson = await RequestJsonAsync("GET", path, null);
                IngestDmPayload(dmJson, peer, bumpAfter: true);
            }
        }

        static async UniTask PostSessionAsync()
        {
            var friends = FriendsHubService.GetFriendsSnapshot();
            var sb = new StringBuilder(64);
            sb.Append("{\"friendIds\":[");
            var first = true;
            for (var i = 0; i < friends.Count; i++)
            {
                var id = friends[i].PlayerId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append(JsonString(id));
            }

            sb.Append("]}");
            await RequestJsonAsync("POST", "/v1/session", sb.ToString());
        }

        static void IngestChannelPayload(string json, MenuChatChannel channel, bool bumpAfter)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var envelope = JsonUtility.FromJson<MessagesEnvelope>(json);
            if (envelope?.messages == null)
            {
                return;
            }

            for (var i = 0; i < envelope.messages.Length; i++)
            {
                var raw = envelope.messages[i];
                if (raw == null || string.IsNullOrEmpty(raw.id) || !s_seenIds.Add(raw.id))
                {
                    continue;
                }

                var localId = UnityServicesBootstrap.PlayerId;
                var msg = new GameChatMessage(
                    channel,
                    raw.playerId,
                    raw.displayName,
                    raw.text,
                    ParseTs(raw.ts),
                    string.Equals(raw.playerId, localId, StringComparison.Ordinal));
                AppendChannel(msg);
                ChannelMessageReceived?.Invoke(msg);
                if (bumpAfter && !string.IsNullOrEmpty(raw.ts))
                {
                    if (channel == MenuChatChannel.FriendsFeed)
                    {
                        s_afterFriends = MaxTs(s_afterFriends, raw.ts);
                    }
                    else
                    {
                        s_afterGlobal = MaxTs(s_afterGlobal, raw.ts);
                    }
                }
            }
        }

        static void IngestDmPayload(string json, string peerId, bool bumpAfter)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var envelope = JsonUtility.FromJson<MessagesEnvelope>(json);
            if (envelope?.messages == null)
            {
                return;
            }

            var localId = UnityServicesBootstrap.PlayerId;
            for (var i = 0; i < envelope.messages.Length; i++)
            {
                var raw = envelope.messages[i];
                if (raw == null || string.IsNullOrEmpty(raw.id) || !s_seenIds.Add(raw.id))
                {
                    continue;
                }

                var other = string.Equals(raw.playerId, localId, StringComparison.Ordinal)
                    ? peerId
                    : raw.playerId;
                var dm = new GameChatDirectMessage(
                    other,
                    raw.playerId,
                    raw.displayName,
                    raw.text,
                    ParseTs(raw.ts),
                    string.Equals(raw.playerId, localId, StringComparison.Ordinal));
                AppendDirect(dm);
                DirectMessageReceived?.Invoke(dm);
                if (bumpAfter && !string.IsNullOrEmpty(raw.ts))
                {
                    s_afterDm.TryGetValue(peerId, out var prev);
                    s_afterDm[peerId] = MaxTs(prev, raw.ts);
                }
            }
        }

        static async UniTask<string> RequestJsonAsync(string method, string path, string bodyJson)
        {
            var url = s_apiBase.TrimEnd('/') + path;
            using var req = new UnityWebRequest(url, method);
            if (!string.IsNullOrEmpty(bodyJson))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
            }

            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Baraki-Player-Id", UnityServicesBootstrap.PlayerId);
            var name = UnityServicesBootstrap.PlayerName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = PlayerProfileService.DisplayName;
            }

            req.SetRequestHeader("X-Baraki-Player-Name", string.IsNullOrWhiteSpace(name) ? "Игрок" : name);
            if (!string.IsNullOrWhiteSpace(s_apiKey))
            {
                req.SetRequestHeader("X-Baraki-Key", s_apiKey);
            }

            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"{(int)req.responseCode} {req.error}");
            }

            return req.downloadHandler?.text ?? string.Empty;
        }

        static string ResolveApiBase()
        {
            var prefs = PlayerPrefs.GetString(PrefsApiBase, string.Empty);
            if (!string.IsNullOrWhiteSpace(prefs))
            {
                return prefs.Trim();
            }

            return GameChatRules.DefaultApiBaseUrl;
        }

        static void AppendChannel(GameChatMessage message)
        {
            var list = message.Channel == MenuChatChannel.FriendsFeed ? s_friendsHistory : s_globalHistory;
            list.Add(message);
            const int max = 200;
            if (list.Count > max)
            {
                list.RemoveRange(0, list.Count - max);
            }
        }

        static void AppendDirect(GameChatDirectMessage message)
        {
            if (!s_dmByPeer.TryGetValue(message.OtherPlayerId, out var list))
            {
                list = new List<GameChatDirectMessage>();
                s_dmByPeer[message.OtherPlayerId] = list;
            }

            list.Add(message);
            const int max = 100;
            if (list.Count > max)
            {
                list.RemoveRange(0, list.Count - max);
            }
        }

        static void SetReady(bool ready)
        {
            if (s_ready == ready)
            {
                return;
            }

            s_ready = ready;
            ReadyChanged?.Invoke();
        }

        static DateTime ParseTs(string ts)
        {
            if (DateTime.TryParse(ts, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.ToLocalTime();
            }

            return DateTime.Now;
        }

        static string MaxTs(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? string.Empty;
            if (string.IsNullOrEmpty(b)) return a;
            return string.CompareOrdinal(a, b) >= 0 ? a : b;
        }

        static string JsonString(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            var escaped = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
            return $"\"{escaped}\"";
        }

        [Serializable]
        class MessagesEnvelope
        {
            public WireMessage[] messages;
        }

        [Serializable]
        class WireMessage
        {
            public string id;
            public string ts;
            public string playerId;
            public string displayName;
            public string text;
            public string channel;
            public string peerId;
        }
    }
}
