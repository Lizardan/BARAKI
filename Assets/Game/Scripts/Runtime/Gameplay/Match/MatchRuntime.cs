using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match.Fog;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Scene bridge: prepares arena on session start; starts <see cref="MatchController"/>
    /// only after race pick is complete.
    /// </summary>
    public sealed class MatchRuntime : MonoBehaviour
    {
        public static MatchRuntime Current { get; private set; }

        [SerializeField] private MatchArenaGreybox _greybox;
        [SerializeField] private RaceCatalog _raceCatalog;
        [SerializeField] private UnitVisualCatalog _unitVisualCatalog;
        [SerializeField] private UnitAbilityCatalog _abilityCatalog;

        private bool _isMatchStarted;
        private MatchTickMode _tickMode = MatchTickMode.Offline;
        private MatchSnapshot _lastNetworkSnapshot;
        private byte[] _lastNetworkSnapshotBytes;
        private float _startPhaseRealtime = -1f;

        private GameplayCameraPanController _panController;
        private MatchSelectionBridge _selectionBridge;

        public MatchController Controller { get; private set; }
        public bool IsMatchStarted => _isMatchStarted;
        public MatchTickMode TickMode => _tickMode;
        public MatchSnapshot LastNetworkSnapshot => _lastNetworkSnapshot;
        /// <summary>Raw last-good snapshot bytes for host migration (all peers).</summary>
        public byte[] LastNetworkSnapshotBytes => _lastNetworkSnapshotBytes;
        /// <summary>Local <see cref="Time.time"/> when the last network snapshot arrived (client render-time anchor).</summary>
        public float LastSnapshotArrivalRealtime { get; private set; } = -1f;
        public MatchSelection Selection => _selectionBridge != null ? _selectionBridge.Selection : null;
        public MatchPickRegistry PickRegistry => _selectionBridge != null ? _selectionBridge.Registry : null;
        public MatchFogOfWar FogOfWar => GetComponent<MatchFogOfWar>();

        private void Awake()
        {
            Current = this;
            MatchPickLayers.InitializeFromName();
            EnsureSelectionBridge();

            if (_greybox == null)
            {
                _greybox = MatchArenaGreybox.Current;
            }
        }

        private void OnEnable()
        {
            Current = this;
            EnsureArenaDirector();
            if (GameSession.IsPlaying)
            {
                PrepareArena();
                return;
            }

            GameSession.Started += OnSessionStarted;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
            GameSession.Started -= OnSessionStarted;
        }

        private void OnSessionStarted()
        {
            _isMatchStarted = false;
            Controller = null;
            _lastNetworkSnapshot = null;
            _lastNetworkSnapshotBytes = null;
            LastSnapshotArrivalRealtime = -1f;
            _startPhaseRealtime = -1f;
            PrepareArena();
        }

        private void Update()
        {
            if (!_isMatchStarted)
            {
                return;
            }

            // Cinematic Start→Early is local (camera / timeout). Do not gate it on tick
            // mode: clients never tick, and the listen-host ticks from MatchNetworkAuthority.
            TryBeginEarlyPhaseWhenReady();

            if (!MatchTickAuthority.ShouldTickSimulation(_tickMode))
            {
                return;
            }

            // Server mode: MatchNetworkAuthority owns the tick.
            if (_tickMode == MatchTickMode.Server)
            {
                return;
            }

            // Арена останавливает симуляцию матча, но не Time.timeScale.
            if (MatchPauseGate.IsSimulationPaused)
            {
                return;
            }

            Controller?.Tick(Time.deltaTime);
        }

        void EnsureArenaDirector()
        {
            if (GetComponent<ArenaDirector>() == null)
            {
                gameObject.AddComponent<ArenaDirector>();
            }
        }

        public void SetNetworkTickMode(MatchTickMode mode)
        {
            _tickMode = mode;
        }

        public void NotifyServerTick()
        {
            TryBeginEarlyPhaseWhenReady();
        }

        public void ApplyNetworkSnapshot(MatchSnapshot snapshot, byte[] rawBytes = null)
        {
            StoreLastNetworkSnapshot(snapshot, rawBytes);
            LastSnapshotArrivalRealtime = Time.time;
            Controller?.ApplyAuthoritativeSnapshot(snapshot);
        }

        public void StoreLastNetworkSnapshot(MatchSnapshot snapshot, byte[] rawBytes = null)
        {
            _lastNetworkSnapshot = snapshot;
            if (rawBytes is { Length: > 0 })
            {
                _lastNetworkSnapshotBytes = rawBytes;
            }
            else if (snapshot != null)
            {
                _lastNetworkSnapshotBytes = MatchSnapshotCodec.Serialize(snapshot);
            }
        }

        private void TryBeginEarlyPhaseWhenReady()
        {
            if (Controller == null || Controller.Phase != MatchPhase.Start)
            {
                return;
            }

            if (_panController == null)
            {
                _panController = GameplayCameraPanController.Current;
            }

            // Wait for the base-focus fly-in, but never for race-pick pan lock — that used
            // to freeze the match clock if the overlay failed to unlock. Timeout matches
            // GDD PHASE_START so a stuck camera cannot hold the sim forever.
            var cameraBusy = _panController != null && _panController.IsFocusInProgress;
            var waitedLongEnough = _startPhaseRealtime >= 0f
                && Time.realtimeSinceStartup - _startPhaseRealtime >= MatchRules.StartPhaseMaxWaitSeconds;
            if (cameraBusy && !waitedLongEnough)
            {
                return;
            }

            Controller.BeginEarlyPhase();
        }

        public void StartMatch(IReadOnlyList<string> raceIds, int localPlayerSlot)
        {
            if (_isMatchStarted)
            {
                return;
            }

            if (raceIds == null)
            {
                throw new ArgumentNullException(nameof(raceIds));
            }

            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            if (raceIds.Count != setup.PlayerCount)
            {
                throw new ArgumentException("RaceIds count must match PlayerCount.", nameof(raceIds));
            }

            if (localPlayerSlot < 0 || localPlayerSlot >= setup.PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerSlot));
            }

            var config = new MatchConfig(setup.PlayerCount, raceIds, autoFateBonuses: true);
            Controller = new MatchController();
            if (_raceCatalog != null)
            {
                Controller.CombatCatalog = new RaceCatalogCombatCatalog(_raceCatalog);
            }

            var visualCatalog = ResolveUnitVisualCatalog();
            Controller.UnitVisualCatalog = visualCatalog;
            Controller.Combat.UnitVisualCatalog = visualCatalog;
            Controller.Combat.AbilityCatalog = ResolveAbilityCatalog();

            Controller.StartMatch(config);
            _isMatchStarted = true;
            _startPhaseRealtime = Time.realtimeSinceStartup;
            EnsureSelectionBridge();
            EnsureFogOfWar(localPlayerSlot);
            _selectionBridge.BeginMatch();

            if (_greybox != null)
            {
                _greybox.Configure(config.PlayerCount, config.RaceIds, config.CenterArenaRadius);
            }

            var buildingPickPresenter = GetComponent<MatchBuildingPickPresenter>();
            buildingPickPresenter?.RefreshBuildingPicks();
            if (buildingPickPresenter != null)
            {
                StartCoroutine(DeferredBuildingPickRefresh(buildingPickPresenter));
            }

            FocusCameraOnLocalPlayer(localPlayerSlot);
            TryBeginEarlyPhaseWhenReady();
        }

        UnitVisualCatalog ResolveUnitVisualCatalog()
        {
            if (_unitVisualCatalog != null)
            {
                return _unitVisualCatalog;
            }

            var presenter = MatchCombatPresenter.Current;
            return presenter != null ? presenter.VisualCatalog : null;
        }

        UnitAbilityCatalog ResolveAbilityCatalog()
        {
            if (_abilityCatalog != null)
            {
                return _abilityCatalog;
            }

            var fromResources = Resources.Load<UnitAbilityCatalog>("Catalogs/UnitAbilityCatalog");
            if (fromResources != null)
            {
                return fromResources;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(
                "Assets/Game/ScriptableObjects/Catalogs/UnitAbilityCatalog.asset");
#else
            return null;
#endif
        }

        void EnsureFogOfWar(int localPlayerSlot)
        {
            var fog = GetComponent<MatchFogOfWar>();
            if (fog == null)
            {
                fog = gameObject.AddComponent<MatchFogOfWar>();
            }

            fog.Configure(this, localPlayerSlot);
        }

        private void FocusCameraOnLocalPlayer(int localPlayerSlot)
        {
            if (Controller?.Layout == null)
            {
                return;
            }

            var panController = GameplayCameraPanController.Current;
            if (panController == null)
            {
                return;
            }

            var focusPosition = GameplayCameraSettings.GetPlayerBaseFocusPosition(
                Controller.Layout,
                localPlayerSlot);
            // Snap on match start — pad buttons use the smooth OrientBaseToScreenEdge path.
            panController.SetYawDegrees(
                GameplayCameraSettings.ComputeYawDegreesForBaseAtScreenEdge(
                    focusPosition,
                    Vector3.zero,
                    GameplayCameraPreferences.PreferredBaseScreenEdge));
            panController.FocusOnPosition(focusPosition);
        }

        private void PrepareArena()
        {
            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            if (_greybox == null)
            {
                return;
            }

            var previewConfig = MatchConfig.MvpDefault(setup.PlayerCount);
            _greybox.Configure(previewConfig.PlayerCount, previewConfig.RaceIds, previewConfig.CenterArenaRadius);
        }

        void EnsureSelectionBridge()
        {
            if (_selectionBridge == null)
            {
                _selectionBridge = GetComponent<MatchSelectionBridge>();
            }

            if (_selectionBridge == null)
            {
                _selectionBridge = gameObject.AddComponent<MatchSelectionBridge>();
            }

            if (GetComponent<MatchBuildingStatusPresenter>() == null)
            {
                gameObject.AddComponent<MatchBuildingStatusPresenter>();
            }

            if (GetComponent<MatchBuildingFxPresenter>() == null)
            {
                gameObject.AddComponent<MatchBuildingFxPresenter>();
            }

            if (GetComponent<MatchMainExtraTargetingRingPresenter>() == null)
            {
                gameObject.AddComponent<MatchMainExtraTargetingRingPresenter>();
            }
        }

        static IEnumerator DeferredBuildingPickRefresh(MatchBuildingPickPresenter presenter)
        {
            yield return null;
            presenter?.RefreshBuildingPicks();
        }
    }
}
