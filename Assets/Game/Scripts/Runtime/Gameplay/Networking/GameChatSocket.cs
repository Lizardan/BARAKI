using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Persistent WebSocket for the menu chat (single socket per app). Incoming frames
    /// are forwarded to <see cref="FrameReceived"/> on the main thread; reconnects follow
    /// <see cref="GameChatSocketRules.NextReconnectDelaySeconds"/>. Send failures are the
    /// caller's concern (outbox lives in GameChatService).
    /// </summary>
    public static class GameChatSocket
    {
        public static bool IsConnected => s_connected;
        public static float DowntimeSeconds =>
            s_connected ? 0f : Mathf.Max(0f, Time.realtimeSinceStartup - s_disconnectedAtRealtime);

        /// <summary>Raw server frame (JSON text), dispatched on the main thread.</summary>
        public static event Action<string> FrameReceived;
        /// <summary>Raised after every successful (re)connect — subscriber should send a sync frame.</summary>
        public static event Action Connected;
        public static event Action Disconnected;

        static ClientWebSocket s_socket;
        static CancellationTokenSource s_lifetimeCts;
        static int s_sessionId;
        static int s_reconnectAttempt;
        static float s_disconnectedAtRealtime = -1f;
        static bool s_running;
        static bool s_connected;
        static string s_url;
        static string s_playerId;
        static string s_displayName;
        static string s_apiKey;

        /// <summary>Tear down socket and state (Play enter / reset).</summary>
        public static void Reset()
        {
            Interlocked.Increment(ref s_sessionId);
            var socket = s_socket;
            s_socket = null;
            s_connected = false;
            s_running = false;
            s_reconnectAttempt = 0;
            s_url = null;
            s_playerId = null;
            s_displayName = null;
            s_apiKey = null;
            if (s_lifetimeCts != null)
            {
                try { s_lifetimeCts.Cancel(); } catch { /* already disposed */ }
                s_lifetimeCts.Dispose();
                s_lifetimeCts = null;
            }

            if (socket != null)
            {
                AbortQuietly(socket);
            }

            FrameReceived = null;
            Connected = null;
            Disconnected = null;
        }

        public static void Configure(string apiBase, string playerId, string displayName, string apiKey)
        {
            s_url = BuildWebSocketUrl(apiBase);
            s_playerId = playerId ?? string.Empty;
            s_displayName = ToAscii(displayName);
            s_apiKey = apiKey ?? string.Empty;
        }

        /// <summary>Start connect/receive/reconnect loop. Idempotent while running.</summary>
        public static void EnsureStarted()
        {
            if (s_running || string.IsNullOrEmpty(s_url))
            {
                return;
            }

            s_running = true;
            s_lifetimeCts = new CancellationTokenSource();
            RunLoopAsync(s_lifetimeCts.Token).Forget();
        }

        /// <summary>Send a text frame now; false when the socket is not open
        /// (caller buffers into the outbox).</summary>
        public static bool TrySend(string text)
        {
            var socket = s_socket;
            if (!s_connected || socket?.State != WebSocketState.Open)
            {
                return false;
            }

            SendQuietly(socket, text);
            return true;
        }

        static async UniTaskVoid RunLoopAsync(CancellationToken lifetime)
        {
            var session = s_sessionId;
            while (s_running && session == s_sessionId && !lifetime.IsCancellationRequested)
            {
                try
                {
                    await ConnectOnceAsync(session, lifetime);
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogThrottled($"GameChatSocket: connection failed: {ex.Message}");
                }

                MarkDisconnected();
                if (!s_running || session != s_sessionId || lifetime.IsCancellationRequested)
                {
                    break;
                }

                var delay = GameChatSocketRules.NextReconnectDelaySeconds(s_reconnectAttempt++);
                try
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(delay),
                        ignoreTimeScale: true,
                        cancellationToken: lifetime);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        static async UniTask ConnectOnceAsync(int session, CancellationToken lifetime)
        {
            using var socket = new ClientWebSocket();
            if (!string.IsNullOrEmpty(s_apiKey))
            {
                socket.Options.SetRequestHeader("X-Baraki-Key", s_apiKey);
            }

            socket.Options.SetRequestHeader("X-Baraki-Player-Id", ToAscii(s_playerId));
            socket.Options.SetRequestHeader("X-Baraki-Player-Name", ToAscii(s_displayName));
            // Protocol-level keepalive pings keep NAT mappings alive.
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(GameChatSocketRules.HeartbeatIntervalSeconds);

            await socket.ConnectAsync(new Uri(s_url), lifetime);

            s_socket = socket;
            s_connected = true;
            s_reconnectAttempt = 0;
            PlaytestLog.Info("Chat", "SocketConnected");
            SafeRaise(Connected);

            // Per-receive timeout doubles as the dead-connection watchdog:
            // any inbound traffic (data or protocol pong) resets it implicitly by
            // starting a fresh receive window.
            var buffer = new byte[8 * 1024];
            var message = new StringBuilder(1024);
            while (session == s_sessionId && socket.State == WebSocketState.Open && !lifetime.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                using (var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(lifetime))
                {
                    receiveCts.CancelAfter(TimeSpan.FromSeconds(GameChatSocketRules.DeadConnectionSeconds));
                    try
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), receiveCts.Token);
                    }
                    catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                    {
                        // No inbound traffic within the window: force reconnect.
                        LogThrottled("GameChatSocket: dead connection (receive timeout).");
                        AbortQuietly(socket);
                        break;
                    }
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                var frame = message.ToString();
                message.Clear();
                DispatchFrame(frame);
            }

            if (session == s_sessionId)
            {
                MarkDisconnected();
            }
        }

        static void DispatchFrame(string frame)
        {
            // FrameReceived subscribers are chat-service ingestion — cheap and main-thread safe.
            SafeRaise(() => FrameReceived?.Invoke(frame));
        }

        static void MarkDisconnected()
        {
            var wasConnected = s_connected;
            s_connected = false;
            s_socket = null;
            if (wasConnected || s_disconnectedAtRealtime <= 0f)
            {
                s_disconnectedAtRealtime = Time.realtimeSinceStartup;
            }

            if (wasConnected)
            {
                SafeRaise(Disconnected);
            }
        }

        static void SafeRaise(Action action)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        static void SendQuietly(ClientWebSocket socket, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            _ = socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        static void AbortQuietly(ClientWebSocket socket)
        {
            try
            {
                socket.Abort();
            }
            catch
            {
                // already closed/aborted
            }
        }

        static void LogThrottled(string message)
        {
            Debug.LogWarning(message);
        }

        static string BuildWebSocketUrl(string apiBase)
        {
            var trimmed = (apiBase ?? string.Empty).Trim().TrimEnd('/');
            if (trimmed.StartsWith("https://", StringComparison.Ordinal))
            {
                return "wss://" + trimmed["https://".Length..] + "/v1/ws";
            }

            if (trimmed.StartsWith("http://", StringComparison.Ordinal))
            {
                return "ws://" + trimmed["http://".Length..] + "/v1/ws";
            }

            return trimmed.Length > 0 ? trimmed + "/v1/ws" : string.Empty;
        }

        static string ToAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Player";
            }

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c >= 32 && c <= 126)
                {
                    sb.Append(c);
                }
            }

            return sb.Length > 0 ? sb.ToString() : "Player";
        }
    }
}
