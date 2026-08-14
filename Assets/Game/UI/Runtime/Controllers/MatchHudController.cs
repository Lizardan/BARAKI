using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const string CommandFeedbackHiddenClass = "match-hud__command-feedback--hidden";
        private const string MigrationHiddenClass = "match-hud__migration--hidden";
        private const string ResultsHiddenClass = "match-hud__results--hidden";
        private const float BountyPopupDurationSeconds = 1.5f;
        private const float CommandFeedbackDurationSeconds = 2.2f;

        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private float _barracksLabelWorldOffsetY = 4f;
        [SerializeField] private float _passiveRingWorldOffsetY = 6f;

        private MatchRuntime _matchRuntime;
        private MatchController _subscribedController;
        private Camera _camera;
        private int _localPlayerSlot = MatchSetup.DefaultLocalPlayerSlot;
        private float _bountyPopupUntilTime;
        private float _commandFeedbackUntilTime;
        private Label _phaseLabel;
        private Label _timeLabel;
        private Label _goldLabel;
        private Label _bountyPopupLabel;
        private Label _commandFeedbackLabel;
        private Label _startCountdownLabel;
        private VisualElement _migrationOverlay;
        private Label _disconnectTitle;
        private Label _disconnectBody;
        private Button _disconnectKickButton;
        private int _disconnectKickSlot = -1;
        private VisualElement _resultsOverlay;
        private Label _resultsTitle;
        private Button _resultsRematchButton;
        private Button _resultsLobbyButton;
        private VisualElement _barracksLayer;
        private VisualElement _passiveGoldLayer;
        private readonly Dictionary<string, Label> _barracksLabels = new();
        private readonly Dictionary<int, VisualElement> _passiveRings = new();
        private readonly Dictionary<int, VisualElement> _titanRings = new();
        private int _trackedBarracksCount = -1;
        private int _trackedBarracksLocalSlot = -1;
        private bool _resultsShown;

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
            _commandFeedbackLabel = root.Q<Label>("CommandFeedbackLabel");
            _startCountdownLabel = root.Q<Label>("StartCountdownLabel");
            _migrationOverlay = root.Q<VisualElement>("MigrationOverlay");
            _disconnectTitle = root.Q<Label>("DisconnectTitle");
            _disconnectBody = root.Q<Label>("DisconnectBody");
            _disconnectKickButton = root.Q<Button>("DisconnectKickButton");
            _resultsOverlay = root.Q<VisualElement>("ResultsOverlay");
            _resultsTitle = root.Q<Label>("ResultsTitle");
            _resultsRematchButton = root.Q<Button>("ResultsRematchButton");
            _resultsLobbyButton = root.Q<Button>("ResultsLobbyButton");
            _barracksLayer = root.Q<VisualElement>("BarracksTimerLayer");
            _passiveGoldLayer = root.Q<VisualElement>("PassiveGoldTimerLayer");

            if (_resultsRematchButton != null)
            {
                _resultsRematchButton.clicked += OnRematchClicked;
            }

            if (_resultsLobbyButton != null)
            {
                _resultsLobbyButton.clicked += OnReturnToMenuClicked;
            }
        }

        private void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            _camera = CameraCache.Main;
            RefreshLocalPlayerSlot();
            MatchNetworkCommands.CommandResultReceived += OnCommandResultReceived;
            if (_disconnectKickButton != null)
            {
                _disconnectKickButton.clicked += OnDisconnectKickClicked;
            }
        }

        private void OnDisable()
        {
            MatchNetworkCommands.CommandResultReceived -= OnCommandResultReceived;
            if (_disconnectKickButton != null)
            {
                _disconnectKickButton.clicked -= OnDisconnectKickClicked;
            }

            UnsubscribeFromController();
        }

        private void OnDestroy()
        {
            if (_resultsRematchButton != null)
            {
                _resultsRematchButton.clicked -= OnRematchClicked;
            }

            if (_resultsLobbyButton != null)
            {
                _resultsLobbyButton.clicked -= OnReturnToMenuClicked;
            }
        }

        private void LateUpdate()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

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
                if (phase == MatchPhase.End && controller != null && !_resultsShown)
                {
                    ShowResults(controller.WinnerSlot ?? 0);
                }

                if (phase != MatchPhase.End)
                {
                    ClearHud();
                }

                return;
            }

            if (!isRunning)
            {
                ClearBarracksLabels();
                ClearPassiveRings();
                ClearTitanRings();
                UpdateMigrationOverlay();
                UpdateCommandFeedback();
                return;
            }

            UpdateTopBar(controller);
            UpdateBarracksTimers(controller);
            UpdatePassiveGoldRings(controller);
            UpdateTitanRings(controller);
            UpdateMigrationOverlay();
            UpdateCommandFeedback();
        }

        void OnCommandResultReceived(MatchCommandResult result)
        {
            if (_commandFeedbackLabel == null || result == MatchCommandResult.Ok)
            {
                return;
            }

            _commandFeedbackLabel.text = MatchHudFormatting.FormatCommandFeedback(result);
            _commandFeedbackLabel.RemoveFromClassList(CommandFeedbackHiddenClass);
            _commandFeedbackUntilTime = Time.unscaledTime + CommandFeedbackDurationSeconds;
        }

        void UpdateCommandFeedback()
        {
            if (_commandFeedbackLabel == null)
            {
                return;
            }

            if (_commandFeedbackUntilTime > 0f && Time.unscaledTime >= _commandFeedbackUntilTime)
            {
                _commandFeedbackUntilTime = 0f;
                _commandFeedbackLabel.text = string.Empty;
                _commandFeedbackLabel.AddToClassList(CommandFeedbackHiddenClass);
            }
        }

        void UpdateMigrationOverlay()
        {
            if (_migrationOverlay == null)
            {
                return;
            }

            var view = MatchDisconnectOverlayQuery.Capture();
            if (!view.IsVisible)
            {
                _migrationOverlay.AddToClassList(MigrationHiddenClass);
                return;
            }

            _migrationOverlay.RemoveFromClassList(MigrationHiddenClass);
            if (_disconnectTitle != null)
            {
                _disconnectTitle.text = string.IsNullOrWhiteSpace(view.PlayerName)
                    ? "Пауза"
                    : view.PlayerName;
            }

            if (_disconnectBody != null)
            {
                _disconnectBody.text = view.StatusText;
            }

            _disconnectKickSlot = view.KickSlot;
            if (_disconnectKickButton != null)
            {
                _disconnectKickButton.EnableInClassList("match-hud__disconnect-kick--hidden", !view.ShowKick);
                _disconnectKickButton.SetEnabled(view.ShowKick && view.KickSlot >= 0);
            }
        }

        void OnDisconnectKickClicked()
        {
            if (_disconnectKickSlot < 0)
            {
                return;
            }

            MatchNetworkSession.RequestKickDisconnected(_disconnectKickSlot);
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

        private void OnMatchEnded(int winnerSlot) => ShowResults(winnerSlot);

        void ShowResults(int winnerSlot)
        {
            _resultsShown = true;
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
            var controllerGold = 0;
            foreach (var player in controller.Players)
            {
                if (player.SlotIndex == _localPlayerSlot)
                {
                    controllerGold = player.Gold;
                    break;
                }
            }

            var snapshotGold = 0;
            var useSnapshot = _matchRuntime != null
                && _matchRuntime.TickMode == MatchTickMode.Client
                && MatchHudGoldRules.TryGetSnapshotGold(
                    _matchRuntime.LastNetworkSnapshot,
                    _localPlayerSlot,
                    out snapshotGold);

            return MatchHudGoldRules.ResolveLocalGold(
                _localPlayerSlot,
                controllerGold,
                useSnapshot ? snapshotGold : controllerGold,
                useSnapshot);
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

                if (!MatchHudVisibility.ShouldShowBarracksTimer(barracks.OwnerSlot, _localPlayerSlot)
                    || barracks.OwnerSlot >= layout.Slots.Count)
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
            var barracks = controller.WaveScheduler.Barracks;
            var localCount = 0;
            for (var i = 0; i < barracks.Count; i++)
            {
                if (MatchHudVisibility.ShouldShowBarracksTimer(barracks[i].OwnerSlot, _localPlayerSlot))
                {
                    localCount++;
                }
            }

            if (_trackedBarracksCount == localCount && _trackedBarracksLocalSlot == _localPlayerSlot)
            {
                return;
            }

            ClearBarracksLabels();

            for (var i = 0; i < barracks.Count; i++)
            {
                var entry = barracks[i];
                if (!MatchHudVisibility.ShouldShowBarracksTimer(entry.OwnerSlot, _localPlayerSlot))
                {
                    continue;
                }

                var key = GetBarracksKey(entry.OwnerSlot, entry.BarracksId);
                var label = new Label
                {
                    pickingMode = PickingMode.Ignore,
                    text = "0",
                };
                label.AddToClassList(BarracksTimerClass);
                _barracksLayer.Add(label);
                _barracksLabels[key] = label;
            }

            _trackedBarracksCount = localCount;
            _trackedBarracksLocalSlot = _localPlayerSlot;
        }

        private void ClearHud()
        {
            _phaseLabel.text = "—";
            _timeLabel.text = "00:00";
            _goldLabel.text = MatchHudFormatting.FormatGold(0);
            _bountyPopupLabel.text = string.Empty;
            _bountyPopupLabel.AddToClassList(BountyPopupHiddenClass);
            if (!_resultsShown)
            {
                _resultsOverlay.AddToClassList(ResultsHiddenClass);
                _resultsTitle.text = string.Empty;
            }

            _startCountdownLabel.text = string.Empty;
            _startCountdownLabel.AddToClassList(StartCountdownHiddenClass);
            ClearBarracksLabels();
            ClearPassiveRings();
            ClearTitanRings();
        }

        private void ClearBarracksLabels()
        {
            foreach (var label in _barracksLabels.Values)
            {
                label.RemoveFromHierarchy();
            }

            _barracksLabels.Clear();
            _trackedBarracksCount = -1;
            _trackedBarracksLocalSlot = -1;
        }

        void UpdatePassiveGoldRings(MatchController controller)
        {
            if (controller.Phase == MatchPhase.Start)
            {
                ClearPassiveRings();
                return;
            }

            if (_camera == null || _passiveGoldLayer == null || _passiveGoldLayer.panel == null)
            {
                return;
            }

            var layout = controller.Layout;
            if (layout == null)
            {
                return;
            }

            var activeSlots = new HashSet<int>();
            foreach (var player in controller.Players)
            {
                if (player.IsEliminated || player.PassiveGoldLevel <= 0)
                {
                    continue;
                }

                activeSlots.Add(player.SlotIndex);
                if (!_passiveRings.TryGetValue(player.SlotIndex, out var ring))
                {
                    ring = CreatePassiveRing();
                    _passiveGoldLayer.Add(ring);
                    _passiveRings[player.SlotIndex] = ring;
                }

                if (player.SlotIndex >= layout.Slots.Count)
                {
                    ring.style.display = DisplayStyle.None;
                    continue;
                }

                var worldPosition = layout.Slots[player.SlotIndex]
                    .GetBuildingWorldPosition(GameIds.Buildings.Main);
                worldPosition.y += _passiveRingWorldOffsetY;
                var screenPoint = _camera.WorldToScreenPoint(worldPosition);
                if (screenPoint.z <= 0f)
                {
                    ring.style.display = DisplayStyle.None;
                    continue;
                }

                var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(
                    _passiveGoldLayer.panel,
                    worldPosition,
                    _camera);
                ring.style.display = DisplayStyle.Flex;
                ring.style.left = panelPosition.x;
                ring.style.top = panelPosition.y;
                ring.userData = MatchHudPassiveGoldRules.GetFill01(
                    player.PassiveGoldTickRemainingSeconds,
                    MatchEconomyRules.PassiveGoldTickIntervalSeconds);
                ring.MarkDirtyRepaint();
            }

            var remove = new List<int>();
            foreach (var pair in _passiveRings)
            {
                if (!activeSlots.Contains(pair.Key))
                {
                    pair.Value.RemoveFromHierarchy();
                    remove.Add(pair.Key);
                }
            }

            for (var i = 0; i < remove.Count; i++)
            {
                _passiveRings.Remove(remove[i]);
            }
        }

        static VisualElement CreatePassiveRing()
        {
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("match-hud__passive-ring");
            ring.generateVisualContent += DrawPassiveRing;
            return ring;
        }

        static void DrawPassiveRing(MeshGenerationContext ctx)
        {
            var element = ctx.visualElement;
            var fill = element.userData is float f ? Mathf.Clamp01(f) : 0f;
            var rect = element.contentRect;
            if (rect.width < 1f || rect.height < 1f)
            {
                return;
            }

            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.45f;
            var painter = ctx.painter2D;
            painter.lineWidth = 4f;
            painter.strokeColor = new Color(34f / 255f, 34f / 255f, 36f / 255f, 0.85f);
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Stroke();

            if (fill <= 0f)
            {
                return;
            }

            painter.strokeColor = new Color(194f / 255f, 119f / 255f, 58f / 255f, 1f);
            painter.BeginPath();
            painter.Arc(
                center,
                radius,
                Angle.Degrees(-90f),
                Angle.Degrees(-90f + (360f * fill)));
            painter.Stroke();
        }

        void ClearPassiveRings()
        {
            foreach (var ring in _passiveRings.Values)
            {
                ring.generateVisualContent -= DrawPassiveRing;
                ring.RemoveFromHierarchy();
            }

            _passiveRings.Clear();
        }

        void UpdateTitanRings(MatchController controller)
        {
            if (controller.Phase == MatchPhase.Start)
            {
                ClearTitanRings();
                return;
            }

            if (_camera == null || _passiveGoldLayer == null || _passiveGoldLayer.panel == null)
            {
                return;
            }

            var layout = controller.Layout;
            if (layout == null)
            {
                return;
            }

            var activeSlots = new HashSet<int>();
            foreach (var player in controller.Players)
            {
                var titan = controller.GetTitanState(player.SlotIndex);
                var roster = controller.GetHeroRoster(player.SlotIndex);
                var gatesMet = TitanRules.AreResearchGatesMet(player, roster);
                if (player.IsEliminated
                    || titan == null
                    || !TitanRules.ShouldShowResearchBar(
                        titan.State,
                        titan.ResearchProgressSeconds,
                        gatesMet))
                {
                    continue;
                }

                activeSlots.Add(player.SlotIndex);
                if (!_titanRings.TryGetValue(player.SlotIndex, out var ring))
                {
                    ring = CreateTitanRing();
                    _passiveGoldLayer.Add(ring);
                    _titanRings[player.SlotIndex] = ring;
                }

                if (player.SlotIndex >= layout.Slots.Count)
                {
                    ring.style.display = DisplayStyle.None;
                    continue;
                }

                var worldPosition = layout.Slots[player.SlotIndex]
                    .GetBuildingWorldPosition(GameIds.Buildings.Main);
                worldPosition.y += _passiveRingWorldOffsetY * 2f;
                var screenPoint = _camera.WorldToScreenPoint(worldPosition);
                if (screenPoint.z <= 0f)
                {
                    ring.style.display = DisplayStyle.None;
                    continue;
                }

                var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(
                    _passiveGoldLayer.panel,
                    worldPosition,
                    _camera);
                ring.style.display = DisplayStyle.Flex;
                ring.style.left = panelPosition.x;
                ring.style.top = panelPosition.y;
                ring.userData = Mathf.Clamp01(
                    titan.ResearchProgressSeconds / TitanRules.ResearchSeconds);
                ring.MarkDirtyRepaint();
            }

            var remove = new List<int>();
            foreach (var pair in _titanRings)
            {
                if (!activeSlots.Contains(pair.Key))
                {
                    pair.Value.RemoveFromHierarchy();
                    remove.Add(pair.Key);
                }
            }

            for (var i = 0; i < remove.Count; i++)
            {
                _titanRings.Remove(remove[i]);
            }
        }

        static VisualElement CreateTitanRing()
        {
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("match-hud__titan-ring");
            ring.generateVisualContent += DrawTitanRing;
            return ring;
        }

        static void DrawTitanRing(MeshGenerationContext ctx)
        {
            var element = ctx.visualElement;
            var fill = element.userData is float f ? Mathf.Clamp01(f) : 0f;
            var rect = element.contentRect;
            if (rect.width < 1f || rect.height < 1f)
            {
                return;
            }

            var painter = ctx.painter2D;
            painter.fillColor = new Color(34f / 255f, 34f / 255f, 36f / 255f, 0.85f);
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();

            if (fill <= 0f)
            {
                return;
            }

            var filled = rect.width * fill;
            painter.fillColor = new Color(155f / 255f, 93f / 255f, 229f / 255f, 1f);
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMin + filled, rect.yMin));
            painter.LineTo(new Vector2(rect.xMin + filled, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        void ClearTitanRings()
        {
            foreach (var ring in _titanRings.Values)
            {
                ring.generateVisualContent -= DrawTitanRing;
                ring.RemoveFromHierarchy();
            }

            _titanRings.Clear();
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

        void OnRematchClicked()
        {
            LeaveAndLoadAsync(GameSceneNames.Lobby).Forget();
        }

        void OnReturnToMenuClicked()
        {
            LeaveAndLoadAsync(GameSceneNames.MainMenu).Forget();
        }

        async UniTaskVoid LeaveAndLoadAsync(string sceneName)
        {
            MatchNetworkSession.LeaveMatch();
            await SceneManager.LoadSceneAsync(sceneName)
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        private static string GetBarracksKey(int ownerSlot, string barracksId) =>
            $"{ownerSlot}:{barracksId}";
    }
}
