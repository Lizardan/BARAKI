using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchHudController : MonoBehaviour
    {
        private const string BarracksTimerClass = "match-hud__barracks-timer";
        private const string BarracksTimerInactiveClass = "match-hud__barracks-timer--inactive";
        private const string StartCountdownHiddenClass = "match-hud__start-countdown--hidden";
        private const string BountyPopupHiddenClass = "match-hud__bounty-popup--hidden";
        private const string ResultsHiddenClass = "match-hud__results--hidden";
        private const float BountyPopupDurationSeconds = 1.5f;

        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private float _barracksLabelWorldOffsetY = 4f;

        private MatchRuntime _matchRuntime;
        private MatchController _subscribedController;
        private Camera _camera;
        private int _localPlayerSlot = MatchSetup.DefaultLocalPlayerSlot;
        private float _bountyPopupUntilTime;
        private Label _phaseLabel;
        private Label _timeLabel;
        private Label _goldLabel;
        private Label _bountyPopupLabel;
        private Label _startCountdownLabel;
        private VisualElement _resultsOverlay;
        private Label _resultsTitle;
        private VisualElement _barracksLayer;
        private readonly Dictionary<string, Label> _barracksLabels = new();
        private int _trackedBarracksCount = -1;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);
            _phaseLabel = root.Q<Label>("PhaseLabel");
            _timeLabel = root.Q<Label>("TimeLabel");
            _goldLabel = root.Q<Label>("GoldLabel");
            _bountyPopupLabel = root.Q<Label>("BountyPopupLabel");
            _startCountdownLabel = root.Q<Label>("StartCountdownLabel");
            _resultsOverlay = root.Q<VisualElement>("ResultsOverlay");
            _resultsTitle = root.Q<Label>("ResultsTitle");
            _barracksLayer = root.Q<VisualElement>("BarracksTimerLayer");
        }

        private void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }

            _camera = Camera.main;
            RefreshLocalPlayerSlot();
        }

        private void OnDisable()
        {
            UnsubscribeFromController();
        }

        private void LateUpdate()
        {
            RefreshLocalPlayerSlot();
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller != _subscribedController)
            {
                UnsubscribeFromController();
                if (controller != null)
                {
                    controller.UnitKilled += OnUnitKilled;
                    controller.MatchEnded += OnMatchEnded;
                    _subscribedController = controller;
                }
            }

            var phase = controller != null ? controller.Phase : MatchPhase.Lobby;
            var isRunning = controller != null && controller.IsRunning;
            if (MatchHudVisibility.ShouldClearRunningHud(controller != null, isRunning, phase))
            {
                ClearHud();
                return;
            }

            if (!isRunning)
            {
                ClearBarracksLabels();
                return;
            }

            UpdateTopBar(controller);
            UpdateBarracksTimers(controller);
        }

        private void OnUnitKilled(UnitKillEvent killEvent)
        {
            if (killEvent.KillerOwnerSlot != _localPlayerSlot || killEvent.GoldGranted <= 0)
            {
                return;
            }

            _bountyPopupLabel.text = MatchHudFormatting.FormatBountyPopup(killEvent.GoldGranted);
            _bountyPopupLabel.RemoveFromClassList(BountyPopupHiddenClass);
            _bountyPopupUntilTime = Time.unscaledTime + BountyPopupDurationSeconds;
        }

        private void OnMatchEnded(int winnerSlot)
        {
            _resultsTitle.text = MatchHudFormatting.FormatMatchResult(winnerSlot);
            _resultsOverlay.RemoveFromClassList(ResultsHiddenClass);
        }

        private void UpdateTopBar(MatchController controller)
        {
            if (controller.Phase == MatchPhase.Start)
            {
                _phaseLabel.text = "—";
                _timeLabel.text = "00:00";
                _goldLabel.text = MatchHudFormatting.FormatGold(GetLocalGold(controller));
                _startCountdownLabel.text = string.Empty;
                _startCountdownLabel.AddToClassList(StartCountdownHiddenClass);
                UpdateBountyPopup();
                return;
            }

            _phaseLabel.text = MatchHudFormatting.FormatPhase(controller.Phase);
            _timeLabel.text = MatchHudFormatting.FormatMatchTime(controller.MatchTimeSeconds);
            _goldLabel.text = MatchHudFormatting.FormatGold(GetLocalGold(controller));
            _startCountdownLabel.text = string.Empty;
            _startCountdownLabel.AddToClassList(StartCountdownHiddenClass);
            UpdateBountyPopup();
        }

        private void UpdateBountyPopup()
        {
            if (Time.unscaledTime >= _bountyPopupUntilTime)
            {
                _bountyPopupLabel.text = string.Empty;
                _bountyPopupLabel.AddToClassList(BountyPopupHiddenClass);
            }
        }

        private void RefreshLocalPlayerSlot()
        {
            _localPlayerSlot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
        }

        private int GetLocalGold(MatchController controller)
        {
            foreach (var player in controller.Players)
            {
                if (player.SlotIndex == _localPlayerSlot)
                {
                    return player.Gold;
                }
            }

            return 0;
        }

        private void UpdateBarracksTimers(MatchController controller)
        {
            if (controller.Phase == MatchPhase.Start)
            {
                ClearBarracksLabels();
                return;
            }

            if (_camera == null || _barracksLayer == null || _barracksLayer.panel == null)
            {
                return;
            }

            var scheduler = controller.WaveScheduler;
            var layout = controller.Layout;
            if (scheduler == null || layout == null)
            {
                return;
            }

            EnsureBarracksLabels(controller);
            var schedulerActive = scheduler.IsActive;

            for (var i = 0; i < scheduler.Barracks.Count; i++)
            {
                var barracks = scheduler.Barracks[i];
                var key = GetBarracksKey(barracks.OwnerSlot, barracks.BarracksId);
                if (!_barracksLabels.TryGetValue(key, out var label))
                {
                    continue;
                }

                if (barracks.OwnerSlot >= layout.Slots.Count)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                var worldPosition = layout.Slots[barracks.OwnerSlot]
                    .GetBuildingWorldPosition(barracks.BarracksId);
                worldPosition.y += _barracksLabelWorldOffsetY;

                var screenPoint = _camera.WorldToScreenPoint(worldPosition);
                if (screenPoint.z <= 0f)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(
                    _barracksLayer.panel,
                    worldPosition,
                    _camera);

                label.style.display = DisplayStyle.Flex;
                label.style.left = panelPosition.x;
                label.style.top = panelPosition.y;
                label.text = MatchHudFormatting.FormatBarracksTimer(
                    barracks.TimeUntilNextWaveSeconds,
                    schedulerActive);

                label.EnableInClassList(BarracksTimerInactiveClass, !schedulerActive);
            }
        }

        private void EnsureBarracksLabels(MatchController controller)
        {
            var barracksCount = controller.WaveScheduler.Barracks.Count;
            if (_trackedBarracksCount == barracksCount)
            {
                return;
            }

            ClearBarracksLabels();

            for (var i = 0; i < barracksCount; i++)
            {
                var barracks = controller.WaveScheduler.Barracks[i];
                var key = GetBarracksKey(barracks.OwnerSlot, barracks.BarracksId);
                var label = new Label
                {
                    pickingMode = PickingMode.Ignore,
                    text = "0",
                };
                label.AddToClassList(BarracksTimerClass);
                _barracksLayer.Add(label);
                _barracksLabels[key] = label;
            }

            _trackedBarracksCount = barracksCount;
        }

        private void ClearHud()
        {
            _phaseLabel.text = "—";
            _timeLabel.text = "00:00";
            _goldLabel.text = MatchHudFormatting.FormatGold(0);
            _bountyPopupLabel.text = string.Empty;
            _bountyPopupLabel.AddToClassList(BountyPopupHiddenClass);
            _resultsOverlay.AddToClassList(ResultsHiddenClass);
            _resultsTitle.text = string.Empty;
            _startCountdownLabel.text = string.Empty;
            _startCountdownLabel.AddToClassList(StartCountdownHiddenClass);
            ClearBarracksLabels();
        }

        private void ClearBarracksLabels()
        {
            foreach (var label in _barracksLabels.Values)
            {
                label.RemoveFromHierarchy();
            }

            _barracksLabels.Clear();
            _trackedBarracksCount = -1;
        }

        private void UnsubscribeFromController()
        {
            if (_subscribedController != null)
            {
                _subscribedController.UnitKilled -= OnUnitKilled;
                _subscribedController.MatchEnded -= OnMatchEnded;
                _subscribedController = null;
            }
        }

        private static string GetBarracksKey(int ownerSlot, string barracksId) =>
            $"{ownerSlot}:{barracksId}";
    }
}
