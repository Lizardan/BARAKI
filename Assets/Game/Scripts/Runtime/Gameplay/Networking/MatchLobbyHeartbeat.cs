using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Keeps the Unity Lobby alive so mid-match reconnect and host migration can find it.</summary>
    public sealed class MatchLobbyHeartbeat : MonoBehaviour
    {
        const float IntervalSeconds = 15f;

        static MatchLobbyHeartbeat s_instance;
        string _lobbyId;
        float _nextPingAtRealtime;
        bool _isSending;

        public static MatchLobbyHeartbeat Ensure()
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            var existing = FindAnyObjectByType<MatchLobbyHeartbeat>();
            if (existing != null)
            {
                s_instance = existing;
                return existing;
            }

            var go = new GameObject(nameof(MatchLobbyHeartbeat));
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<MatchLobbyHeartbeat>();
            return s_instance;
        }

        public void Bind(string lobbyId)
        {
            _lobbyId = lobbyId ?? string.Empty;
            _nextPingAtRealtime = 0f;
        }

        void Update()
        {
            if (string.IsNullOrEmpty(_lobbyId) || _isSending)
            {
                return;
            }

            if (Time.realtimeSinceStartup < _nextPingAtRealtime)
            {
                return;
            }

            _nextPingAtRealtime = Time.realtimeSinceStartup + IntervalSeconds;
            SendAsync().Forget();
        }

        async UniTaskVoid SendAsync()
        {
            _isSending = true;
            try
            {
                if (MatchSessionService.Backend is UnityLobbyRelaySessionBackend relay)
                {
                    await relay.SendHeartbeatAsync(_lobbyId);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"MatchLobbyHeartbeat: {ex.Message}");
            }
            finally
            {
                _isSending = false;
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
