using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Gameplay.Networking
{
    public readonly struct MatchmakerEnsureResponse
    {
        public MatchmakerEnsureResponse(
            string matchId,
            string wssUrl,
            string joinToken,
            int slot,
            int playerCount,
            string roomCode)
        {
            MatchId = matchId ?? string.Empty;
            WssUrl = wssUrl ?? string.Empty;
            JoinToken = joinToken ?? string.Empty;
            Slot = slot;
            PlayerCount = playerCount;
            RoomCode = roomCode ?? string.Empty;
        }

        public string MatchId { get; }
        public string WssUrl { get; }
        public string JoinToken { get; }
        public int Slot { get; }
        public int PlayerCount { get; }
        public string RoomCode { get; }

        public static MatchmakerEnsureResponse Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            return new MatchmakerEnsureResponse(
                GetString(json, "match_id"),
                GetString(json, "wss_url"),
                GetString(json, "join_token"),
                GetInt(json, "slot"),
                GetInt(json, "player_count"),
                GetString(json, "room_code"));
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
            while (end < json.Length && char.IsWhiteSpace(json[end]))
            {
                end++;
            }

            var start = end;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
            {
                end++;
            }

            return int.TryParse(json.Substring(start, end - start), out var n) ? n : 0;
        }
    }
}
