using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Initializes UGS + anonymous auth once per process.</summary>
    public static class UnityServicesBootstrap
    {
        const string PrefsUgsPlayerName = "baraki.profile.ugsPlayerName";
        const float InitRetryCooldownSeconds = 45f;

        static UniTaskCompletionSource s_initGate;
        static bool s_initInFlight;
        static UniTask<string> s_playerNameTask;
        static bool s_playerNameFetchStarted;
        static string s_playerName = string.Empty;
        static string s_cachedPlayerName = string.Empty;
        static float s_cooldownUntilRealtime;
        static string s_lastInitError = string.Empty;

        public static event Action PlayerNameChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetForPlaySession()
        {
            ResetInitGate();
            s_playerName = string.Empty;
            s_playerNameTask = default;
            s_playerNameFetchStarted = false;
            s_cooldownUntilRealtime = 0f;
            s_lastInitError = string.Empty;
            LoadCachedPlayerName();
        }

        public static bool IsReady =>
            UnityServices.State == ServicesInitializationState.Initialized
            && AuthenticationService.Instance.IsSignedIn;

        public static bool IsInitCooldownActive =>
            !IsReady && Time.realtimeSinceStartup < s_cooldownUntilRealtime;

        public static string LastInitError => s_lastInitError;

        public static string PlayerId =>
            IsReady ? AuthenticationService.Instance.PlayerId : string.Empty;

        public static string PlayerName
        {
            get
            {
                if (!string.IsNullOrEmpty(s_playerName))
                {
                    return s_playerName;
                }

                if (!string.IsNullOrEmpty(s_cachedPlayerName))
                {
                    return s_cachedPlayerName;
                }

                return IsReady ? AuthenticationService.Instance.PlayerName ?? string.Empty : string.Empty;
            }
        }

        /// <summary>Loads last known UGS name (Name#suffix) from PlayerPrefs for offline UI/copy.</summary>
        public static void PrimeCachedPlayerNameFromPrefs()
        {
            LoadCachedPlayerName();
        }

        /// <summary>Delay before the next auth attempt after a failed init (keeps hub from serial 30s timeouts).</summary>
        public static float GetRecommendedRetryDelaySeconds(float minimumSeconds = 2.5f)
        {
            if (IsReady || !IsInitCooldownActive)
            {
                return minimumSeconds;
            }

            return Mathf.Max(minimumSeconds, s_cooldownUntilRealtime - Time.realtimeSinceStartup + 0.25f);
        }

        public static async UniTask<string> EnsurePlayerNameAsync()
        {
            await EnsureInitializedAsync();
            if (!IsReady)
            {
                return PlayerName;
            }

            if (!string.IsNullOrEmpty(s_playerName))
            {
                return s_playerName;
            }

            if (!s_playerNameFetchStarted)
            {
                s_playerNameFetchStarted = true;
                s_playerNameTask = FetchPlayerNameAsync().Preserve();
            }

            var fetched = await s_playerNameTask;
            if (string.IsNullOrEmpty(fetched))
            {
                ResetPlayerNameFetch();
                return PlayerName;
            }

            return fetched;
        }

        static void ResetInitGate()
        {
            s_initInFlight = false;
            s_initGate = null;
        }

        static async UniTaskVoid RunInitGateAsync()
        {
            var gate = s_initGate;
            await InitializeAsync();
            gate?.TrySetResult();
        }

        static void ResetPlayerNameFetch()
        {
            s_playerNameFetchStarted = false;
            s_playerNameTask = default;
        }

        static async UniTask<string> FetchPlayerNameAsync()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerName))
                {
                    SetPlayerName(AuthenticationService.Instance.PlayerName);
                    return s_playerName;
                }

                SetPlayerName(await AuthenticationService.Instance.GetPlayerNameAsync(autoGenerate: true));
                return s_playerName;
            }
            catch (Exception ex)
            {
                ResetPlayerNameFetch();
                Debug.LogWarning($"UnityServicesBootstrap: player name fetch failed: {ex.Message}");
                return PlayerName;
            }
        }

        static void SetPlayerName(string playerName)
        {
            var normalized = playerName ?? string.Empty;
            if (string.Equals(s_playerName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            s_playerName = normalized;
            if (FriendsHubRules.IsValidUgsPlayerName(normalized))
            {
                s_cachedPlayerName = normalized;
                PlayerPrefs.SetString(PrefsUgsPlayerName, normalized);
                PlayerPrefs.Save();
            }

            PlayerNameChanged?.Invoke();
        }

        static void LoadCachedPlayerName()
        {
            var cached = PlayerPrefs.GetString(PrefsUgsPlayerName, string.Empty);
            s_cachedPlayerName = FriendsHubRules.IsValidUgsPlayerName(cached) ? cached : string.Empty;
        }

        public static async UniTask<string> SyncPlayerNameFromDisplayNameAsync(string displayName)
        {
            await EnsureInitializedAsync();
            if (!IsReady)
            {
                return PlayerName;
            }

            var baseName = FriendsHubRules.SanitizeUgsNameBase(displayName);
            if (baseName.Length < 3)
            {
                return await EnsurePlayerNameAsync();
            }

            var current = await EnsurePlayerNameAsync();
            if (FriendsHubRules.UgsNameBaseMatches(current, baseName))
            {
                return current;
            }

            var updated = await AuthenticationService.Instance.UpdatePlayerNameAsync(baseName);
            SetPlayerName(updated);
            return s_playerName;
        }

        public static async UniTask<string> TrySyncPlayerNameFromDisplayNameAsync(string displayName)
        {
            try
            {
                return await SyncPlayerNameFromDisplayNameAsync(displayName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UnityServicesBootstrap: UGS name sync skipped: {ex.Message}");
                return PlayerName;
            }
        }

        public static UniTask EnsureInitializedAsync()
        {
            if (IsReady)
            {
                return UniTask.CompletedTask;
            }

            if (!Application.isPlaying)
            {
                return UniTask.CompletedTask;
            }

            if (IsInitCooldownActive)
            {
                return UniTask.CompletedTask;
            }

            if (s_initInFlight && s_initGate != null)
            {
                return s_initGate.Task;
            }

            s_initInFlight = true;
            s_initGate = new UniTaskCompletionSource();
            RunInitGateAsync().Forget();
            return s_initGate.Task;
        }

        static async UniTask InitializeAsync()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                s_lastInitError = string.Empty;
                s_cooldownUntilRealtime = 0f;

                // Auth may already expose Name#suffix — surface it before Cloud Save / Friends.
                if (!string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerName))
                {
                    SetPlayerName(AuthenticationService.Instance.PlayerName);
                }

                Debug.Log($"UnityServicesBootstrap: signed in as {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception ex)
            {
                s_lastInitError = ex.Message ?? ex.GetType().Name;
                s_cooldownUntilRealtime = Time.realtimeSinceStartup + InitRetryCooldownSeconds;
                Debug.LogWarning(
                    $"UnityServicesBootstrap: init failed (cooldown {InitRetryCooldownSeconds:0}s): {ex.Message}");
                ResetInitGate();
            }
        }
    }
}
