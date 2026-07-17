using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Cloud Save backed profile (displayName, avatar; rank/points stubs).</summary>
    public static class PlayerProfileService
    {
        public const string KeyDisplayName = "displayName";
        public const string KeyRank = "rank";
        public const string KeyPoints = "points";
        public const string KeyWins = "wins";
        public const string KeyLosses = "losses";
        public const string KeyMatches = "matches";
        public const string KeyAvatarId = "avatarId";
        public const int AvatarCount = 8;

        const string PrefsDisplayName = "baraki.profile.displayName";
        const string PrefsAvatarId = "baraki.profile.avatarId";

        static readonly Color[] AvatarColors =
        {
            new(0.45f, 0.28f, 0.14f),
            new(0.22f, 0.38f, 0.28f),
            new(0.28f, 0.32f, 0.48f),
            new(0.48f, 0.24f, 0.24f),
            new(0.40f, 0.34f, 0.18f),
            new(0.30f, 0.22f, 0.40f),
            new(0.20f, 0.36f, 0.40f),
            new(0.42f, 0.30f, 0.22f),
        };

        static readonly string[] AvatarGlyphs =
        {
            "◆", "●", "▲", "■", "✦", "❖", "◉", "▣",
        };

        static string s_displayName = "Player";
        static int s_rank;
        static int s_points;
        static int s_wins;
        static int s_losses;
        static int s_matches;
        static int s_avatarId;
        static bool s_loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetForPlaySession()
        {
            s_loaded = false;
        }

        public static string DisplayName => s_displayName;
        public static int Rank => s_rank;
        public static int Points => s_points;
        public static int Wins => s_wins;
        public static int Losses => s_losses;
        public static int Matches => s_matches;
        public static int AvatarId => s_avatarId;
        public static bool IsLoaded => s_loaded;

        public static Color GetAvatarColor(int avatarId) =>
            AvatarColors[ClampAvatarId(avatarId)];

        public static string GetAvatarGlyph(int avatarId) =>
            AvatarGlyphs[ClampAvatarId(avatarId)];

        public static int ClampAvatarId(int avatarId) =>
            Mathf.Clamp(avatarId, 0, AvatarCount - 1);

        /// <summary>Loads cached profile from PlayerPrefs without UGS.</summary>
        public static void PrimeFromLocalPrefs()
        {
            LoadLocalPrefs();
        }

        public static async UniTask LoadAsync()
        {
            LoadLocalPrefs();
            UnityServicesBootstrap.PrimeCachedPlayerNameFromPrefs();

            try
            {
                await UnityServicesBootstrap.EnsureInitializedAsync();
            }
            catch (Exception initEx)
            {
                Debug.LogWarning($"PlayerProfileService.LoadAsync UGS init: {initEx.Message}");
            }

            if (!UnityServicesBootstrap.IsReady)
            {
                s_loaded = true;
                return;
            }

            try
            {
                const float cloudTimeoutSec = 5f;
                var keys = new HashSet<string>
                {
                    KeyDisplayName, KeyRank, KeyPoints, KeyWins, KeyLosses, KeyMatches, KeyAvatarId,
                };
                var loadTask = CloudSaveService.Instance.Data.Player.LoadAsync(keys).AsUniTask();
                var (hasCloudData, cloudData) = await UniTask.WhenAny(
                    loadTask,
                    UniTask.Delay(TimeSpan.FromSeconds(cloudTimeoutSec), ignoreTimeScale: true));

                if (!hasCloudData)
                {
                    Debug.LogWarning(
                        $"PlayerProfileService.LoadAsync: cloud save timed out after {cloudTimeoutSec:0}s, using local profile.");
                    s_loaded = true;
                    return;
                }

                var data = cloudData;

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

                if (data.TryGetValue(KeyWins, out var winsItem))
                {
                    s_wins = winsItem.Value.GetAs<int>();
                }

                if (data.TryGetValue(KeyLosses, out var lossesItem))
                {
                    s_losses = lossesItem.Value.GetAs<int>();
                }

                if (data.TryGetValue(KeyMatches, out var matchesItem))
                {
                    s_matches = matchesItem.Value.GetAs<int>();
                }

                if (data.TryGetValue(KeyAvatarId, out var avatarItem))
                {
                    s_avatarId = ClampAvatarId(avatarItem.Value.GetAs<int>());
                }

                SaveLocalPrefs();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PlayerProfileService.LoadAsync: {ex.Message}");
                if (string.IsNullOrWhiteSpace(s_displayName) || s_displayName == "Player")
                {
                    var playerId = UnityServicesBootstrap.PlayerId ?? string.Empty;
                    if (playerId.Length > 0)
                    {
                        s_displayName = "Player-" + playerId[..Math.Min(6, playerId.Length)];
                    }
                }
            }

            s_loaded = true;
        }

        public static async UniTask SaveDisplayNameAsync(string displayName)
        {
            s_displayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            await SaveAsync();
        }

        public static async UniTask SaveProfileAsync(string displayName, int avatarId)
        {
            s_displayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            s_avatarId = ClampAvatarId(avatarId);
            await SaveAsync();
        }

        static async UniTask SaveAsync()
        {
            // Local prefs first so editor / offline paths still persist.
            SaveLocalPrefs();
            try
            {
                await UnityServicesBootstrap.EnsureInitializedAsync();
                var payload = new Dictionary<string, object>
                {
                    { KeyDisplayName, s_displayName },
                    { KeyRank, s_rank },
                    { KeyPoints, s_points },
                    { KeyWins, s_wins },
                    { KeyLosses, s_losses },
                    { KeyMatches, s_matches },
                    { KeyAvatarId, s_avatarId },
                };
                await CloudSaveService.Instance.Data.Player.SaveAsync(payload);
                try
                {
                    await UnityServicesBootstrap.TrySyncPlayerNameFromDisplayNameAsync(s_displayName);
                }
                catch (Exception nameEx)
                {
                    Debug.LogWarning($"PlayerProfileService.SaveAsync UGS name sync skipped: {nameEx.Message}");
                }
            }
            catch (Exception ex)
            {
                // Profile already on disk; cloud sync is best-effort in LocalDev.
                Debug.LogWarning($"PlayerProfileService.SaveAsync cloud sync skipped: {ex.Message}");
            }
        }

        static void LoadLocalPrefs()
        {
            if (PlayerPrefs.HasKey(PrefsDisplayName))
            {
                var localName = PlayerPrefs.GetString(PrefsDisplayName, s_displayName);
                if (!string.IsNullOrWhiteSpace(localName))
                {
                    s_displayName = localName;
                }
            }

            if (PlayerPrefs.HasKey(PrefsAvatarId))
            {
                s_avatarId = ClampAvatarId(PlayerPrefs.GetInt(PrefsAvatarId, 0));
            }
        }

        static void SaveLocalPrefs()
        {
            PlayerPrefs.SetString(PrefsDisplayName, s_displayName);
            PlayerPrefs.SetInt(PrefsAvatarId, s_avatarId);
            PlayerPrefs.Save();
        }
    }
}
