using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// If this process is still listen-host but Unity Lobby already elected someone else,
    /// drop the dead session and rejoin the migrated host as a client.
    /// </summary>
    public sealed class HostIsolationWatch : MonoBehaviour
    {
        static HostIsolationWatch s_instance;
        bool _isRunning;

        public static HostIsolationWatch Ensure()
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            var existing = FindAnyObjectByType<HostIsolationWatch>();
            if (existing != null)
            {
                s_instance = existing;
                return existing;
            }

            var go = new GameObject(nameof(HostIsolationWatch));
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<HostIsolationWatch>();
            return s_instance;
        }

        public void BeginCheck()
        {
            if (_isRunning)
            {
                return;
            }

            CheckAsync().Forget();
        }

        async UniTaskVoid CheckAsync()
        {
            _isRunning = true;
            try
            {
                if (MatchSessionService.Backend is not UnityLobbyRelaySessionBackend relay)
                {
                    return;
                }

                var lobbyId = MatchNetworkSession.CurrentHandle.LobbyId;
                if (string.IsNullOrEmpty(lobbyId))
                {
                    return;
                }

                var hostPlayerId = await relay.TryGetLobbyHostPlayerIdAsync(lobbyId);
                if (string.IsNullOrEmpty(hostPlayerId)
                    || string.Equals(hostPlayerId, UnityServicesBootstrap.PlayerId, System.StringComparison.Ordinal))
                {
                    return;
                }

                PlaytestLog.Info("Migration", "IsolatedHostDemoted", ("lobbyHost", hostPlayerId));
                MatchNetworkSession.IsRejoiningMatch = true;
                MatchNetworkSession.ShutdownTransportKeepingSession();
                var ok = await MatchNetworkSession.TryRejoinMigratedHostAsync();
                if (ok)
                {
                    MatchNetworkSession.ClaimReconnectIfNeeded();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"HostIsolationWatch: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
