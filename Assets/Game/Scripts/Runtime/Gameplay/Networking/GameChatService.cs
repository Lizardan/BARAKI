using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

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
        const float PollSeconds = 5f;
        const float PollBackoffSeconds = 15f;
        const float WarnThrottleSeconds = 45f;

        static bool s_ready;
        static bool s_pollRunning;
        static bool s_initRunning;
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
        static int s_sessionId; // bumps every Play enter — invalidates in-flight requests
        static float s_nextPollAllowedAt;
        static float s_nextWarnAt;
        static HttpClient s_http;
        static CancellationTokenSource s_cts = new();

        public static event Action ReadyChanged;
        public static event Action<GameChatMessage> ChannelMessageReceived;
        public static event Action<GameChatDirectMessage> DirectMessageReceived;

        public static bool IsReady => s_ready;

        /// <summary>Editor Play Mode exit / Domain-Reload-off safe reset.</summary>
        public static void ResetSessionState() => ResetForPlaySession();

        // SubsystemRegistration runs on every Play enter even when Domain Reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession()
        {
            System.Threading.Interlocked.Increment(ref s_sessionId);
            s_ready = false;
            s_pollRunning = false;
            s_initRunning = false;
            s_apiBase = string.Empty;
            s_apiKey = string.Empty;
            s_afterGlobal = string.Empty;
            s_afterFriends = string.Empty;
            s_afterDm.Clear();
            s_globalHistory.Clear();
            s_friendsHistory.Clear();
            s_dmByPeer.Clear();
            s_seenIds.Clear();
            s_nextPollAllowedAt = 0f;
            s_nextWarnAt = 0f;
            RecycleHttpClient();
            ReadyChanged = null;
            ChannelMessageReceived = null;
            DirectMessageReceived = null;
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
            if (s_ready)
            {
                return;
            }

            if (s_initRunning)
            {
                while (s_initRunning && !s_ready)
                {
                    await UniTask.Yield();
                }

                return;
            }

            s_initRunning = true;
            try
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

                FriendsHubService.HubChanged -= OnFriendsHubChanged;
                FriendsHubService.HubChanged += OnFriendsHubChanged;

                // Soft-ready: UI can send even if the first session POST fails (common after Play restart).
                SetReady(true);
                EnsurePollLoop();
                try
                {
                    await PostSessionAsync();
                }
                catch (OperationCanceledException)
                {
                    // Play Mode exited mid-init.
                }
                catch (Exception ex)
                {
                    LogThrottledWarning($"GameChatService: session POST failed, retrying in background: {ex.Message}");
                    RetrySessionAsync().Forget();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogThrottledWarning($"GameChatService: init failed: {ex.Message}");
                // Still allow optimistic local chat if API base is configured.
                if (!string.IsNullOrWhiteSpace(s_apiBase))
                {
                    SetReady(true);
                    EnsurePollLoop();
                    RetrySessionAsync().Forget();
                }
                else
                {
                    SetReady(false);
                }
            }
            finally
            {
                s_initRunning = false;
            }
        }

        static async UniTaskVoid RetrySessionAsync()
        {
            var session = s_sessionId;
            for (var i = 0; i < 5 && session == s_sessionId; i++)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(2 + i * 2), ignoreTimeScale: true);
                if (session != s_sessionId)
                {
                    return;
                }

                try
                {
                    await PostSessionAsync();
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                }
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
                LogThrottledWarning($"GameChatService: session sync failed: {ex.Message}");
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

            // Optimistic local echo — server often succeeds even when Unity reports Curl 55.
            var localId = UnityServicesBootstrap.PlayerId;
            var localName = UnityServicesBootstrap.PlayerName;
            if (string.IsNullOrWhiteSpace(localName))
            {
                localName = PlayerProfileService.DisplayName;
            }

            var optimistic = new GameChatMessage(
                channel,
                localId,
                localName,
                message,
                DateTime.Now,
                fromSelf: true);
            AppendChannel(optimistic);
            ChannelMessageReceived?.Invoke(optimistic);

            try
            {
                var responseJson = await RequestJsonAsync("POST", path, $"{{\"text\":{JsonString(message)}}}");
                IngestPostedChannelMessage(responseJson, channel);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogThrottledWarning($"GameChatService: send channel failed: {ex.Message}");
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
                var path = $"/v1/dm/{Uri.EscapeDataString(target)}";
                await RequestJsonAsync("POST", path, $"{{\"text\":{JsonString(message)}}}");
            }
            catch (Exception ex)
            {
                LogThrottledWarning($"GameChatService: send DM failed: {ex.Message}");
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
            var session = s_sessionId;
            var token = s_cts.Token;
            while (s_ready && session == s_sessionId && !token.IsCancellationRequested)
            {
                var delaySeconds = PollSeconds;
                try
                {
                    if (Time.realtimeSinceStartup < s_nextPollAllowedAt)
                    {
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(Math.Max(0.25f, s_nextPollAllowedAt - Time.realtimeSinceStartup)),
                            ignoreTimeScale: true,
                            cancellationToken: token);
                        continue;
                    }

                    await PollOnceAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (session != s_sessionId)
                    {
                        break;
                    }

                    LogThrottledWarning($"GameChatService: poll failed: {ex.Message}");
                    delaySeconds = PollBackoffSeconds;
                    s_nextPollAllowedAt = Time.realtimeSinceStartup + PollBackoffSeconds;
                }

                await UniTask.Delay(
                    TimeSpan.FromSeconds(delaySeconds),
                    ignoreTimeScale: true,
                    cancellationToken: token);
            }

            s_pollRunning = false;
        }

        static async UniTask PollOnceAsync()
        {
            var globalQuery = string.IsNullOrEmpty(s_afterGlobal)
                ? "/v1/channels/global"
                : $"/v1/channels/global?after={Uri.EscapeDataString(s_afterGlobal)}";
            var friendsQuery = string.IsNullOrEmpty(s_afterFriends)
                ? "/v1/channels/friends"
                : $"/v1/channels/friends?after={Uri.EscapeDataString(s_afterFriends)}";

            var globalJson = await RequestJsonAsync("GET", globalQuery, null);
            IngestChannelPayload(globalJson, MenuChatChannel.Global, bumpAfter: true);
            var friendsJson = await RequestJsonAsync("GET", friendsQuery, null);
            IngestChannelPayload(friendsJson, MenuChatChannel.FriendsFeed, bumpAfter: true);

            foreach (var peer in new List<string>(s_dmByPeer.Keys))
            {
                s_afterDm.TryGetValue(peer, out var after);
                var path = string.IsNullOrEmpty(after)
                    ? $"/v1/dm/{Uri.EscapeDataString(peer)}"
                    : $"/v1/dm/{Uri.EscapeDataString(peer)}?after={Uri.EscapeDataString(after)}";
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

                var receivedAt = ParseTs(raw.ts);
                if (!GameChatRules.IsWithinRetention(receivedAt))
                {
                    continue;
                }

                var localId = UnityServicesBootstrap.PlayerId;
                var fromSelf = string.Equals(raw.playerId, localId, StringComparison.Ordinal);
                // Skip server echo when optimistic local line already shown.
                if (fromSelf && IsDuplicateSelfEcho(channel, raw.text))
                {
                    continue;
                }

                var msg = new GameChatMessage(
                    channel,
                    raw.playerId,
                    raw.displayName,
                    raw.text,
                    receivedAt,
                    fromSelf);
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

                var receivedAt = ParseTs(raw.ts);
                if (!GameChatRules.IsWithinRetention(receivedAt))
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
                    receivedAt,
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

        static HttpClient EnsureHttpClient()
        {
            if (s_http != null)
            {
                return s_http;
            }

            var handler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = true,
            };
            var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12),
            };
            http.DefaultRequestHeaders.ExpectContinue = false;
            s_http = http;
            return http;
        }

        static void RecycleHttpClient()
        {
            try
            {
                s_cts.Cancel();
            }
            catch
            {
            }

            try
            {
                s_cts.Dispose();
            }
            catch
            {
            }

            s_cts = new CancellationTokenSource();

            var http = s_http;
            s_http = null;
            if (http == null)
            {
                return;
            }

            try
            {
                http.Dispose();
            }
            catch
            {
            }
        }

        static async UniTask<string> RequestJsonAsync(string method, string path, string bodyJson)
        {
            var session = s_sessionId;
            var isGet = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
            var maxAttempts = isGet ? 1 : 2;
            Exception lastError = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (session != s_sessionId)
                {
                    throw new OperationCanceledException("chat session reset");
                }

                var url = s_apiBase.TrimEnd('/') + path;
                using var request = new HttpRequestMessage(new HttpMethod(method), url);
                if (!string.IsNullOrEmpty(bodyJson))
                {
                    request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                }

                var playerId = UnityServicesBootstrap.PlayerId ?? string.Empty;
                request.Headers.TryAddWithoutValidation("X-Baraki-Player-Id", playerId);
                var name = UnityServicesBootstrap.PlayerName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = PlayerProfileService.DisplayName;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Player";
                }

                name = ToAsciiHeaderValue(name);
                request.Headers.TryAddWithoutValidation("X-Baraki-Player-Name", name);
                if (!string.IsNullOrWhiteSpace(s_apiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Baraki-Key", s_apiKey);
                }

                try
                {
                    var http = EnsureHttpClient();
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(s_cts.Token);
                    using var response = await http.SendAsync(request, linked.Token);
                    var body = await response.Content.ReadAsStringAsync();
                    await UniTask.SwitchToMainThread();

                    if (session != s_sessionId)
                    {
                        throw new OperationCanceledException("chat session reset");
                    }

                    var recovered = TryRecoverBody(body);
                    var code = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode || recovered)
                    {
                        return ExtractJsonPayload(body);
                    }

                    lastError = new InvalidOperationException($"{code} {response.ReasonPhrase}");
                    if (attempt < maxAttempts && code >= 500)
                    {
                        await UniTask.Delay(200 * attempt, ignoreTimeScale: true, cancellationToken: s_cts.Token);
                        continue;
                    }

                    throw lastError;
                }
                catch (OperationCanceledException) when (session != s_sessionId || s_cts.IsCancellationRequested)
                {
                    await UniTask.SwitchToMainThread();
                    throw new OperationCanceledException("chat session reset");
                }
                catch (Exception ex)
                {
                    await UniTask.SwitchToMainThread();
                    if (session != s_sessionId || s_cts.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("chat session reset");
                    }

                    var timedOut = ex is TimeoutException or TaskCanceledException;

                    lastError = timedOut
                        ? new InvalidOperationException("timeout after 12s")
                        : ex;
                    if (attempt < maxAttempts && (timedOut || IsTransientTransmitError(ex.Message)))
                    {
                        await UniTask.Delay(300 * attempt, ignoreTimeScale: true, cancellationToken: s_cts.Token);
                        continue;
                    }

                    throw lastError;
                }
            }

            throw lastError ?? new InvalidOperationException("chat request failed");
        }

        static void LogThrottledWarning(string message)
        {
            if (Time.realtimeSinceStartup < s_nextWarnAt)
            {
                return;
            }

            s_nextWarnAt = Time.realtimeSinceStartup + WarnThrottleSeconds;
            Debug.LogWarning(message);
        }

        static bool TryRecoverBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            var trimmed = body.Trim();
            // Unity sometimes prefixes Curl errors before the JSON payload.
            var jsonStart = trimmed.IndexOf('{');
            if (jsonStart < 0)
            {
                return false;
            }

            var json = trimmed[jsonStart..];
            return json.Contains("\"ok\"", StringComparison.Ordinal)
                   || json.Contains("\"message\"", StringComparison.Ordinal)
                   || json.Contains("\"messages\"", StringComparison.Ordinal);
        }

        static string ExtractJsonPayload(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            var trimmed = body.Trim();
            var jsonStart = trimmed.IndexOf('{');
            return jsonStart <= 0 ? trimmed : trimmed[jsonStart..];
        }

        static void IngestPostedChannelMessage(string json, MenuChatChannel channel)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var posted = JsonUtility.FromJson<PostedMessageEnvelope>(json);
            if (posted?.message == null)
            {
                return;
            }

            IngestChannelPayload(
                $"{{\"messages\":[{JsonUtility.ToJson(posted.message)}]}}",
                channel,
                bumpAfter: true);
        }

        static bool IsTransientTransmitError(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return false;
            }

            return error.IndexOf("Failed to transmit", StringComparison.OrdinalIgnoreCase) >= 0
                   || error.IndexOf("Curl error 55", StringComparison.OrdinalIgnoreCase) >= 0
                   || error.IndexOf("Connection was reset", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string ToAsciiHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Player";
            }

            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c >= 32 && c <= 126)
                {
                    sb.Append(c);
                }
            }

            return sb.Length > 0 ? sb.ToString() : "Player";
        }

        static bool IsDuplicateSelfEcho(MenuChatChannel channel, string text)
        {
            var list = channel == MenuChatChannel.FriendsFeed ? s_friendsHistory : s_globalHistory;
            if (list.Count == 0 || string.IsNullOrEmpty(text))
            {
                return false;
            }

            var last = list[list.Count - 1];
            return last.FromSelf
                   && string.Equals(last.Text, text, StringComparison.Ordinal)
                   && (DateTime.Now - last.ReceivedAt).TotalSeconds < 15;
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
            TrimHistory(list, GameChatRules.MaxChannelHistory);
        }

        static void AppendDirect(GameChatDirectMessage message)
        {
            if (!s_dmByPeer.TryGetValue(message.OtherPlayerId, out var list))
            {
                list = new List<GameChatDirectMessage>();
                s_dmByPeer[message.OtherPlayerId] = list;
            }

            list.Add(message);
            TrimHistory(list, GameChatRules.MaxDirectHistory);
        }

        static void TrimHistory<T>(List<T> list, int max)
        {
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
        class PostedMessageEnvelope
        {
            public WireMessage message;
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
