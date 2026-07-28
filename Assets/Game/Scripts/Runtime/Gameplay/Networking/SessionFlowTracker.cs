using Game.Core;
using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Single owner of session flow: resolve → Flow.Changed → Friends presence.
    /// Controllers notify; they do not publish presence directly.
    /// </summary>
    public sealed class SessionFlowTracker : MonoBehaviour
    {
        static SessionFlowTracker s_instance;

        SessionFlowState _published = SessionFlowState.Offline;
        string _publishedElapsedBucket = string.Empty;
        string _publishedLobbyCode = string.Empty;
        int _publishedOccupied;
        int _publishedMax;
        string _lastScene = string.Empty;
        float _nextMatchPresenceAllowedAt;
        bool _hasPublished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (s_instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(SessionFlowTracker));
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SessionFlowTracker>();
        }

        public static void NotifyChanged() => s_instance?.Evaluate(forcePresence: false);

        /// <summary>After Friends init on Bootstrap — publish «в лаунчере» before Enter Game.</summary>
        public static void NotifyFriendsReadyOnBootstrap() => s_instance?.Evaluate(forcePresence: true);

        void Update() => Evaluate(forcePresence: false);

        void Evaluate(bool forcePresence)
        {
            var scene = SceneManager.GetActiveScene().name;
            if (!string.Equals(scene, _lastScene, System.StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(scene))
                {
                    PlaytestLog.Info("Scene", "Load", ("name", scene));
                }

                _lastScene = scene;
            }

            var state = SessionFlowRules.Resolve(
                scene,
                MatchNetworkSession.IsNetworked,
                MatchNetworkSession.HasNetworkLobby,
                MatchNetworkSession.MatchStarted,
                MatchNetworkSession.NetworkMatchSimStarted);

            var matchTimeSeconds = 0f;
            if (state == SessionFlowState.Match)
            {
                var runtime = FindAnyObjectByType<MatchRuntime>();
                matchTimeSeconds = runtime?.Controller?.MatchTimeSeconds ?? 0f;
            }

            var elapsedBucket = state == SessionFlowState.Match
                ? SessionFlowRules.ResolveElapsedBucket(matchTimeSeconds)
                : string.Empty;

            ResolveLobbyFields(state, out var lobbyCode, out var occupied, out var maxSlots);

            var flowChanged = !_hasPublished || state != _published;
            var lobbyChanged = state == SessionFlowState.Lobby
                               && (occupied != _publishedOccupied
                                   || maxSlots != _publishedMax
                                   || !string.Equals(lobbyCode, _publishedLobbyCode, System.StringComparison.Ordinal));
            var bucketChanged = state == SessionFlowState.Match
                                && !string.Equals(elapsedBucket, _publishedElapsedBucket, System.StringComparison.Ordinal);
            var canRefreshMatchBucket = bucketChanged
                                        && Time.unscaledTime >= _nextMatchPresenceAllowedAt;

            if (!flowChanged && !lobbyChanged && !canRefreshMatchBucket && !forcePresence)
            {
                return;
            }

            if (flowChanged)
            {
                PlaytestLog.Info(
                    "Flow",
                    "Changed",
                    ("from", SessionFlowRules.ToFlowId(_published)),
                    ("to", SessionFlowRules.ToFlowId(state)));
            }

            FriendsHubService.PublishFlowPresence(state, lobbyCode, occupied, maxSlots, elapsedBucket);

            _published = state;
            _publishedElapsedBucket = elapsedBucket;
            _publishedLobbyCode = lobbyCode;
            _publishedOccupied = occupied;
            _publishedMax = maxSlots;
            _hasPublished = true;

            if (state == SessionFlowState.Match && (flowChanged || canRefreshMatchBucket))
            {
                _nextMatchPresenceAllowedAt = Time.unscaledTime + SessionFlowRules.MatchElapsedBucketSeconds;
            }
        }

        static void ResolveLobbyFields(
            SessionFlowState state,
            out string lobbyCode,
            out int occupied,
            out int maxSlots)
        {
            lobbyCode = string.Empty;
            occupied = 0;
            maxSlots = 0;

            if (state != SessionFlowState.Lobby)
            {
                return;
            }

            lobbyCode = FriendsHubRules.NormalizeLobbyCode(MatchNetworkSession.RoomCode);
            if (MatchNetworkSession.HasNetworkLobby)
            {
                maxSlots = MatchNetworkSession.LobbySlotCount;
                for (var i = 0; i < maxSlots; i++)
                {
                    if (MatchNetworkSession.GetLobbySlot(i).IsOccupied)
                    {
                        occupied++;
                    }
                }

                return;
            }

            var local = LocalMatchRegistry.Active;
            if (local == null)
            {
                return;
            }

            maxSlots = local.SlotCount;
            occupied = LobbyReadyRules.CountOccupied(local);
            lobbyCode = FriendsHubRules.NormalizeLobbyCode(MatchNetworkSession.RoomCode);
        }
    }
}
