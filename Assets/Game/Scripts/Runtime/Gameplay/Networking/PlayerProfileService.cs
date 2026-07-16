using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Cloud Save backed profile (displayName; rank/points stubs).</summary>
    public static class PlayerProfileService
    {
        public const string KeyDisplayName = "displayName";
        public const string KeyRank = "rank";
        public const string KeyPoints = "points";

        static string s_displayName = "Player";
        static int s_rank;
        static int s_points;
        static bool s_loaded;

        public static string DisplayName => s_displayName;
        public static int Rank => s_rank;
        public static int Points => s_points;
        public static bool IsLoaded => s_loaded;

        public static async UniTask LoadAsync()
        {
            await UnityServicesBootstrap.EnsureInitializedAsync();
            try
            {
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { KeyDisplayName, KeyRank, KeyPoints });

                if (data.TryGetValue(KeyDisplayName, out var nameItem))
                {
                    s_displayName = nameItem.Value.GetAs<string>() ?? s_displayName;
                }

                if (data.TryGetValue(KeyRank, out var rankItem))
                {
                    s_rank = rankItem.Value.GetAs<int>();
                }

                if (data.TryGetValue(KeyPoints, out var pointsItem))
                {
                    s_points = pointsItem.Value.GetAs<int>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PlayerProfileService.LoadAsync: {ex.Message}");
                if (string.IsNullOrWhiteSpace(s_displayName) || s_displayName == "Player")
                {
                    s_displayName = "Player-" + UnityServicesBootstrap.PlayerId[..Math.Min(6, UnityServicesBootstrap.PlayerId.Length)];
                }
            }

            s_loaded = true;
        }

        public static async UniTask SaveDisplayNameAsync(string displayName)
        {
            s_displayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            await UnityServicesBootstrap.EnsureInitializedAsync();
            var payload = new Dictionary<string, object>
            {
                { KeyDisplayName, s_displayName },
                { KeyRank, s_rank },
                { KeyPoints, s_points },
            };
            await CloudSaveService.Instance.Data.Player.SaveAsync(payload);
        }
    }
}
