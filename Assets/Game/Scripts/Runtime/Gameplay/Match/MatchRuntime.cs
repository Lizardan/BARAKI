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

        private bool _isMatchStarted;
        private MatchTickMode _tickMode = MatchTickMode.Offline;
        private MatchSnapshot _lastNetworkSnapshot;
        private byte[] _lastNetworkSnapshotBytes;

        private GameplayCameraPanController _panController;
        private MatchSelectionBridge _selectionBridge;

        public MatchController Controller { get; private set; }
        public bool IsMatchStarted => _isMatchStarted;
        public MatchTickMode TickMode => _tickMode;
        public MatchSnapshot LastNetworkSnapshot => _lastNetworkSnapshot;
        /// <summary>Raw last-good snapshot bytes for host migration (all peers).</summary>
        public byte[] LastNetworkSnapshotBytes => _lastNetworkSnapshotBytes;
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
            PrepareArena();
        }

        private void Update()
        {
            if (!_isMatchStarted)
            {
                return;
            }

            if (!MatchTickAuthority.ShouldTickSimulation(_tickMode))
            {
                return;
            }

            // Server mode: MatchNetworkAuthority owns the tick.
            if (_tickMode == MatchTickMode.Server)
            {
                return;
            }

            Controller?.Tick(Time.deltaTime);
            TryBeginEarlyPhaseWhenReady();
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

            if (_panController != null && _panController.IsPanLocked)
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

            var config = new MatchConfig(setup.PlayerCount, raceIds);
            Controller = new MatchController();
            if (_raceCatalog != null)
            {
                Controller.CombatCatalog = new RaceCatalogCombatCatalog(_raceCatalog);
            }

            var visualCatalog = ResolveUnitVisualCatalog();
            Controller.UnitVisualCatalog = visualCatalog;
            Controller.Combat.UnitVisualCatalog = visualCatalog;
            Controller.StartMatch(config);
            _isMatchStarted = true;
            EnsureSelectionBridge();
            EnsureFogOfWar(localPlayerSlot);
            _selectionBridge.BeginMatch();

            if (_greybox != null)
            {
                _greybox.Configure(config.PlayerCount, config.CenterArenaRadius);
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
            _greybox.Configure(previewConfig.PlayerCount, previewConfig.CenterArenaRadius);
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
        }

        static IEnumerator DeferredBuildingPickRefresh(MatchBuildingPickPresenter presenter)
        {
            yield return null;
            presenter?.RefreshBuildingPicks();
        }
    }
}
