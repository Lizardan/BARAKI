using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    public readonly struct DiscordActivitySession
    {
        public DiscordActivitySession(
            string instanceId,
            string userId,
            string displayName,
            string wssUrl,
            int slot,
            int playerCount,
            string roomCode,
            string joinToken)
        {
            InstanceId = instanceId ?? string.Empty;
            UserId = userId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            WssUrl = wssUrl ?? string.Empty;
            Slot = slot;
            PlayerCount = playerCount;
            RoomCode = roomCode ?? string.Empty;
            JoinToken = joinToken ?? string.Empty;
        }

        public string InstanceId { get; }
        public string UserId { get; }
        public string DisplayName { get; }
        public string WssUrl { get; }
        public int Slot { get; }
        public int PlayerCount { get; }
        public string RoomCode { get; }
        public string JoinToken { get; }

        public bool HasTransport =>
            WssUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
            || WssUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Receives Discord shell session JSON (SendMessage) and optional .jslib reads.
    /// </summary>
    public sealed class DiscordActivityBridge : MonoBehaviour
    {
        static DiscordActivitySession s_session;
        static bool s_hasSession;

        public static bool TryGetSession(out DiscordActivitySession session)
        {
            if (!s_hasSession)
            {
                TryPullFromBrowser();
            }

            session = s_session;
            return s_hasSession;
        }

        public static void CacheFromEnsure(string instanceId, MatchmakerEnsureResponse response)
        {
            s_session = new DiscordActivitySession(
                instanceId,
                s_session.UserId,
                s_session.DisplayName,
                response.WssUrl,
                response.Slot,
                response.PlayerCount,
                response.RoomCode,
                response.JoinToken);
            s_hasSession = true;
        }

        void Awake()
        {
            gameObject.name = nameof(DiscordActivityBridge);
            DontDestroyOnLoad(gameObject);
            TryPullFromBrowser();
        }

        /// <summary>Called from activity-shell boot.js via SendMessage.</summary>
        public void ReceiveSessionJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            s_session = ParseSessionJson(json);
            s_hasSession = !string.IsNullOrEmpty(s_session.InstanceId) || s_session.HasTransport;
            Debug.Log(
                $"DiscordActivityBridge session instance={s_session.InstanceId} slot={s_session.Slot} wss={s_session.WssUrl}");
        }

        static void TryPullFromBrowser()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var json = Baraki_GetDiscordSessionJson();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            s_session = ParseSessionJson(json);
            s_hasSession = !string.IsNullOrEmpty(s_session.InstanceId) || s_session.HasTransport;
#endif
        }

        static DiscordActivitySession ParseSessionJson(string json)
        {
            return new DiscordActivitySession(
                GetString(json, "instanceId"),
                GetString(json, "userId"),
                GetString(json, "displayName"),
                GetString(json, "wssUrl"),
                GetInt(json, "slot"),
                GetInt(json, "playerCount"),
                GetString(json, "roomCode"),
                GetString(json, "joinToken"));
        }

        static string GetString(string json, string key)
        {
            var token = $"\"{key}\"";
            var idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0)
            {
                return string.Empty;
            }

            var colon = json.IndexOf(':', idx + token.Length);
            var firstQuote = json.IndexOf('"', colon + 1);
            var secondQuote = json.IndexOf('"', firstQuote + 1);
            if (firstQuote < 0 || secondQuote < 0)
            {
                return string.Empty;
            }

            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        static int GetInt(string json, string key)
        {
            var token = $"\"{key}\"";
            var idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0)
            {
                return 0;
            }

            var colon = json.IndexOf(':', idx + token.Length);
            var end = colon + 1;
            while (end < json.Length && (char.IsWhiteSpace(json[end]) || json[end] == '-'))
            {
                if (json[end] == '-')
                {
                    break;
                }

                end++;
            }

            var start = end;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
            {
                end++;
            }

            return int.TryParse(json.Substring(start, end - start), out var n) ? n : 0;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern string Baraki_GetDiscordSessionJson();
#endif
    }
}
