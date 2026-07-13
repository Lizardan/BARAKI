using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
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
        [SerializeField] private MatchArenaGreybox _greybox;
        [SerializeField] private RaceCatalog _raceCatalog;

        private bool _isMatchStarted;
        private MatchTickMode _tickMode = MatchTickMode.Offline;
        private MatchSnapshot _lastNetworkSnapshot;

        private GameplayCameraPanController _panController;
        private MatchSelectionBridge _selectionBridge;

        public MatchController Controller { get; private set; }
        public bool IsMatchStarted => _isMatchStarted;
        public MatchTickMode TickMode => _tickMode;
        public MatchSnapshot LastNetworkSnapshot => _lastNetworkSnapshot;
        public MatchSelection Selection => _selectionBridge != null ? _selectionBridge.Selection : null;
        public MatchPickRegistry PickRegistry => _selectionBridge != null ? _selectionBridge.Registry : null;

        private void Awake()
        {
            RenderSettings.fog = false;
            MatchPickLayers.InitializeFromName();
            EnsureSelectionBridge();

            if (_greybox == null)
            {
                _greybox = FindAnyObjectByType<MatchArenaGreybox>();
            }
        }

        private void OnEnable()
        {
            if (GameSession.IsPlaying)
            {
                PrepareArena();
                return;
            }

            GameSession.Started += OnSessionStarted;
        }

        private void OnDisable()
        {
            GameSession.Started -= OnSessionStarted;
        }

        private void OnSessionStarted()
        {
            _isMatchStarted = false;
            Controller = null;
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

        public void ApplyNetworkSnapshot(MatchSnapshot snapshot)
        {
            _lastNetworkSnapshot = snapshot;
        }

        private void TryBeginEarlyPhaseWhenReady()
        {
            if (Controller == null || Controller.Phase != MatchPhase.Start)
            {
                return;
            }

            if (_panController == null)
            {
                _panController = FindAnyObjectByType<GameplayCameraPanController>();
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

            Controller.StartMatch(config);
            _isMatchStarted = true;
            EnsureSelectionBridge();
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

        private void FocusCameraOnLocalPlayer(int localPlayerSlot)
        {
            if (Controller?.Layout == null)
            {
                return;
            }

            var panController = FindAnyObjectByType<GameplayCameraPanController>();
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
        }

        static IEnumerator DeferredBuildingPickRefresh(MatchBuildingPickPresenter presenter)
        {
            yield return null;
            presenter?.RefreshBuildingPicks();
        }
    }
}
