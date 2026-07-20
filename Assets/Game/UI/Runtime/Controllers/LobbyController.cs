using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using Game.UI.Bindings;
using Game.UI.ViewModels;
using Game.UI.Views;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LobbyController : MonoBehaviour
    {
        private sealed class NetworkLobbySlotsView : IReadOnlyLobbySlots
        {
            public int PlayerCount => MatchNetworkSession.PlayerCount;
            public int SlotCount => MatchNetworkSession.LobbySlotCount;
            public LobbySlotInfo GetSlot(int index) => MatchNetworkSession.GetLobbySlot(index);
        }

        private const float FadeDuration = 0.35f;
        private const string OverlayHiddenClass = "ui-overlay--hidden";

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        private VisualElement _lobbyScreen;
        private VisualElement _slotList;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _roomCodeLabel;
        private Button _readyButton;
        private Button _fillLocalButton;
        private Button _startButton;
        private Button _inviteFriendsButton;
        private Button _backButton;
        private VisualElement _friendsInviteOverlay;
        private Button _friendsInviteCloseButton;
        private VisualElement _lobbyFriendsListContainer;
        private Label _lobbyFriendsErrorLabel;
        private FriendsHubPanel _friendsHubPanel;
        private LobbyViewModel _viewModel;
        private UIBindingScope _bindingScope;
        private bool _isTransitioning;
        private bool _localReady;
        private int _lastLobbyRevision = -1;
        private float _networkConnectWaitStarted = -1f;
        private int _lastPresenceOccupied = -1;
        private int _lastPresenceMax = -1;
        private string _lastPresenceCode = string.Empty;
        private readonly NetworkLobbySlotsView _networkLobbySlots = new();

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            _viewModel = new LobbyViewModel();
            _root = _uiDocument.rootVisualElement;
            _lobbyScreen = _root.Q<VisualElement>("LobbyScreen");
            _slotList = _root.Q<VisualElement>("SlotList");
            _titleLabel = _root.Q<Label>("LobbyTitleLabel");
            _subtitleLabel = _root.Q<Label>("LobbySubtitleLabel");
            _roomCodeLabel = _root.Q<Label>("RoomCodeLabel");
            _readyButton = _root.Q<Button>("ReadyButton");
            _fillLocalButton = _root.Q<Button>("FillLocalButton");
            _startButton = _root.Q<Button>("StartButton");
            _inviteFriendsButton = _root.Q<Button>("InviteFriendsButton");
            _backButton = _root.Q<Button>("BackButton");
            _friendsInviteOverlay = _root.Q<VisualElement>("FriendsInviteOverlay");
            _friendsInviteCloseButton = _root.Q<Button>("FriendsInviteCloseButton");
            _lobbyFriendsListContainer = _root.Q<VisualElement>("LobbyFriendsListContainer");
            _lobbyFriendsErrorLabel = _root.Q<Label>("LobbyFriendsErrorLabel");

            _friendsHubPanel = new FriendsHubPanel(
                FriendsHubPanelMode.InviteOnly,
                _lobbyFriendsListContainer,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                _lobbyFriendsErrorLabel,
                null);
            _friendsHubPanel.Bind();

            _bindingScope = new UIBindingScope(_root);
            if (_startButton != null)
            {
                _bindingScope.Add(_viewModel.StartMatchCommand.BindTo(_startButton));
                _bindingScope.Add(_viewModel.StartMatchCommand.Subscribe(_ => OnStartMatch()));
            }

            if (_backButton != null)
            {
                _bindingScope.Add(_viewModel.BackCommand.BindTo(_backButton));
                _bindingScope.Add(_viewModel.BackCommand.Subscribe(_ => OnBack()));
            }

            if (_readyButton != null)
            {
                _readyButton.clicked += OnReadyClicked;
            }

#if UNITY_EDITOR
            if (_fillLocalButton != null)
            {
                _fillLocalButton.clicked += OnFillLocalClicked;
            }
#else
            if (_fillLocalButton != null)
            {
                _fillLocalButton.style.display = DisplayStyle.None;
            }
#endif

            if (_inviteFriendsButton != null)
            {
                _inviteFriendsButton.clicked += OnInviteFriendsClicked;
            }

            if (_friendsInviteCloseButton != null)
            {
                _friendsInviteCloseButton.clicked += CloseFriendsInviteOverlay;
            }
        }

        private void OnEnable()
        {
            _root?.RegisterCallback<KeyDownEvent>(OnKeyDown);
            RefreshLobbyUi();
            InitializeFriendsHubAsync().Forget();
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnDestroy()
        {
            if (_readyButton != null)
            {
                _readyButton.clicked -= OnReadyClicked;
            }

#if UNITY_EDITOR
            if (_fillLocalButton != null)
            {
                _fillLocalButton.clicked -= OnFillLocalClicked;
            }
#endif

            if (_inviteFriendsButton != null)
            {
                _inviteFriendsButton.clicked -= OnInviteFriendsClicked;
            }

            if (_friendsInviteCloseButton != null)
            {
                _friendsInviteCloseButton.clicked -= CloseFriendsInviteOverlay;
            }

            _bindingScope?.Dispose();
            _friendsHubPanel?.Dispose();
        }

        private void Update()
        {
            if (MatchNetworkSession.IsNetworked)
            {
                if (MatchNetworkSession.MatchStarted && !_isTransitioning)
                {
                    StartMatchAsync(this.GetCancellationTokenOnDestroy()).Forget();
                    return;
                }

                // Refresh while waiting for lobby spawn so connect timeout can surface.
                if (!MatchNetworkSession.HasNetworkLobby
                    || MatchNetworkSession.LobbyRevision != _lastLobbyRevision)
                {
                    RefreshLobbyUi();
                }

                return;
            }

            var lobby = LocalMatchRegistry.Active;
            if (lobby == null || _isTransitioning)
            {
                return;
            }

            if (lobby.Revision != _lastLobbyRevision)
            {
                RefreshLobbyUi();
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_isTransitioning)
            {
                return;
            }

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                OnStartMatch();
            }
            else if (evt.keyCode == KeyCode.Space)
            {
                evt.StopPropagation();
                OnReadyClicked();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                if (_friendsInviteOverlay != null
                    && !_friendsInviteOverlay.ClassListContains(OverlayHiddenClass))
                {
                    CloseFriendsInviteOverlay();
                }
                else
                {
                    OnBack();
                }
            }
        }

        private async UniTaskVoid InitializeFriendsHubAsync()
        {
            try
            {
                await FriendsHubService.InitializeAsync();
                SyncLobbyPresence();
                _friendsHubPanel?.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Lobby friends init skipped: {ex.Message}");
            }
        }

        private void OnInviteFriendsClicked()
        {
            if (_friendsInviteOverlay == null || _isTransitioning)
            {
                return;
            }

            _friendsHubPanel?.Refresh();
            _friendsInviteOverlay.RemoveFromClassList(OverlayHiddenClass);
        }

        private void CloseFriendsInviteOverlay()
        {
            _friendsInviteOverlay?.AddToClassList(OverlayHiddenClass);
        }

        private void OnReadyClicked()
        {
            if (MatchNetworkSession.IsNetworked)
            {
                if (!MatchNetworkSession.HasNetworkLobby || MatchNetworkSession.MatchStarted)
                {
                    return;
                }

                _localReady = !_localReady;
                MatchNetworkSession.RequestReady(_localReady);
                return;
            }

            var lobby = LocalMatchRegistry.Active;
            var slot = LocalMatchRegistry.LocalPlayerSlot;
            if (lobby == null || slot == null || lobby.MatchStarted)
            {
                return;
            }

            _localReady = !_localReady;
            lobby.SetReady(slot.Value, _localReady);
            RefreshLobbyUi();
        }

#if UNITY_EDITOR
        private void OnFillLocalClicked()
        {
            if (MatchNetworkSession.IsNetworked)
            {
                MatchNetworkSession.RequestFillLocal();
                return;
            }

            var lobby = LocalMatchRegistry.Active;
            if (lobby == null || lobby.MatchStarted)
            {
                return;
            }

            lobby.FillEmptySlotsWithLocalStandIns();
            if (LocalMatchRegistry.LocalPlayerSlot is int localSlot)
            {
                lobby.SetReady(localSlot, true);
                _localReady = true;
            }

            RefreshLobbyUi();
        }
#endif

        private void OnStartMatch()
        {
            if (_isTransitioning)
            {
                return;
            }

            if (MatchNetworkSession.IsNetworked)
            {
                MatchNetworkSession.RequestStart();
                return;
            }

            var lobby = LocalMatchRegistry.Active;
            if (lobby == null || !lobby.TryMarkMatchStarted())
            {
                RefreshLobbyUi();
                return;
            }

            StartMatchAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnBack()
        {
            if (_isTransitioning)
            {
                return;
            }

            LocalMatchRegistry.Clear();
            if (MatchNetworkSession.IsNetworked)
            {
                MatchNetworkSession.Shutdown();
            }

            try
            {
                FriendsHubService.SetPresenceAsync(FriendsHubRules.StatusInLauncher).Forget();
            }
            catch (System.Exception)
            {
                // Presence optional for LocalDev.
            }

            LoadSceneAsync(GameSceneNames.MainMenu, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void RefreshLobbyUi()
        {
            if (MatchNetworkSession.IsNetworked)
            {
                RefreshNetworkLobbyUi();
                return;
            }

            var lobby = LocalMatchRegistry.Active;
            _lastLobbyRevision = lobby?.Revision ?? -1;
            if (lobby == null)
            {
                if (_subtitleLabel != null)
                {
                    _subtitleLabel.text = "Нет активной комнаты — создай матч в меню";
                }

                _startButton?.SetEnabled(false);
                _readyButton?.SetEnabled(false);
                _fillLocalButton?.SetEnabled(false);
                _slotList?.Clear();
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = MatchModeRules.GetModeTitle(lobby.PlayerCount);
            }

            if (_subtitleLabel != null)
            {
                _subtitleLabel.text = "Ожидание игроков";
            }

            if (_roomCodeLabel != null)
            {
                _roomCodeLabel.text = $"КОД: {lobby.RoomCode}";
            }

            var localSlot = LocalMatchRegistry.LocalPlayerSlot;
            var isHost = localSlot == lobby.HostSlotIndex;

            _fillLocalButton?.SetEnabled(isHost && !lobby.MatchStarted);
            _readyButton?.SetEnabled(localSlot != null && !lobby.MatchStarted);
            if (_readyButton != null && localSlot != null)
            {
                _readyButton.text = lobby.GetSlot(localSlot.Value).IsReady ? "НЕ ГОТОВ" : "ГОТОВ";
            }

            _startButton?.SetEnabled(isHost && LobbyReadyRules.CanHostStart(lobby) && !lobby.MatchStarted);
            _inviteFriendsButton?.SetEnabled(!lobby.MatchStarted);

            RebuildSlotList(lobby);
            SyncLobbyPresence(lobby, lobby.RoomCode);
        }

        private void RefreshNetworkLobbyUi()
        {
            _lastLobbyRevision = MatchNetworkSession.LobbyRevision;
            if (!MatchNetworkSession.HasNetworkLobby)
            {
                if (_networkConnectWaitStarted < 0f)
                {
                    _networkConnectWaitStarted = Time.unscaledTime;
                }

                var timedOut = MatchTransportConnectRules.HasTimedOut(
                    Time.unscaledTime - _networkConnectWaitStarted);
                if (_subtitleLabel != null)
                {
                    _subtitleLabel.text = timedOut
                        ? MatchTransportConnectRules.ConnectFailedMessage
                        : $"Подключение… {MatchNetworkSession.TransportEndpointHint}";
                }

                if (_roomCodeLabel != null && string.IsNullOrEmpty(MatchNetworkSession.RoomCode))
                {
                    _roomCodeLabel.text = "КОД: ----";
                }

                _startButton?.SetEnabled(false);
                _readyButton?.SetEnabled(false);
                _fillLocalButton?.SetEnabled(false);
                _inviteFriendsButton?.SetEnabled(false);
                _slotList?.Clear();
                return;
            }

            _networkConnectWaitStarted = -1f;

            if (_titleLabel != null)
            {
                _titleLabel.text = MatchModeRules.GetModeTitle(MatchNetworkSession.PlayerCount);
            }

            if (_subtitleLabel != null)
            {
                _subtitleLabel.text = "Ожидание сетевых игроков";
            }

            if (_roomCodeLabel != null)
            {
                _roomCodeLabel.text = $"КОД: {MatchNetworkSession.RoomCode}";
            }

            var localSlot = MatchNetworkSession.LocalSlot;
            var isHost = NetworkLobbySlotRules.IsHostSlot(localSlot);
            var matchStarted = MatchNetworkSession.MatchStarted;
            _fillLocalButton?.SetEnabled(isHost && !matchStarted);
            _readyButton?.SetEnabled(localSlot >= 0 && !matchStarted);
            if (_readyButton != null && localSlot >= 0)
            {
                _localReady = MatchNetworkSession.GetLobbySlot(localSlot).IsReady;
                _readyButton.text = _localReady ? "НЕ ГОТОВ" : "ГОТОВ";
            }

            _startButton?.SetEnabled(MatchNetworkSession.CanLocalStart);
            _inviteFriendsButton?.SetEnabled(!matchStarted);
            RebuildSlotList(_networkLobbySlots);
            SyncLobbyPresence(_networkLobbySlots, MatchNetworkSession.RoomCode);
        }

        private void SyncLobbyPresence()
        {
            if (MatchNetworkSession.IsNetworked)
            {
                if (MatchNetworkSession.HasNetworkLobby)
                {
                    SyncLobbyPresence(_networkLobbySlots, MatchNetworkSession.RoomCode);
                }

                return;
            }

            var lobby = LocalMatchRegistry.Active;
            if (lobby != null)
            {
                SyncLobbyPresence(lobby, lobby.RoomCode);
            }
        }

        private void SyncLobbyPresence(IReadOnlyLobbySlots lobby, string roomCode)
        {
            if (lobby == null || string.IsNullOrWhiteSpace(roomCode))
            {
                return;
            }

            // Editor LocalDev often has no UGS — skip until bootstrap is ready.
            if (!UnityServicesBootstrap.IsReady)
            {
                return;
            }

            var occupied = LobbyReadyRules.CountOccupied(lobby);
            var maxSlots = lobby.SlotCount;
            var code = FriendsHubRules.NormalizeLobbyCode(roomCode);
            if (occupied == _lastPresenceOccupied
                && maxSlots == _lastPresenceMax
                && string.Equals(code, _lastPresenceCode, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastPresenceOccupied = occupied;
            _lastPresenceMax = maxSlots;
            _lastPresenceCode = code;
            FriendsHubService.SetPresenceAsync(
                FriendsHubRules.StatusInGame,
                code,
                occupied,
                maxSlots).Forget();
        }

        private void RebuildSlotList(IReadOnlyLobbySlots lobby)
        {
            if (_slotList == null)
            {
                return;
            }

            _slotList.Clear();
            for (var i = 0; i < lobby.SlotCount; i++)
            {
                var info = lobby.GetSlot(i);
                var row = new VisualElement();
                row.AddToClassList("lobby__slot");

                var name = new Label(info.IsOccupied ? info.DisplayName : $"Слот {i + 1}");
                name.AddToClassList("lobby__slot-name");
                row.Add(name);

                var status = new Label(info.IsOccupied
                    ? (info.IsReady ? "ГОТОВ" : "ЖДЁТ")
                    : "ПУСТО");
                status.AddToClassList("lobby__slot-status");
                if (!info.IsOccupied)
                {
                    status.AddToClassList("lobby__slot-status--empty");
                }
                else if (info.IsReady)
                {
                    status.AddToClassList("lobby__slot-status--ready");
                }

                row.Add(status);
                _slotList.Add(row);
            }
        }

        private async UniTask StartMatchAsync(System.Threading.CancellationToken cancellationToken)
        {
            _isTransitioning = true;
            await FadeOutAsync(cancellationToken);

            MatchSetup setup;
            if (MatchNetworkSession.IsNetworked)
            {
                setup = MatchNetworkSession.ToMatchSetup();
            }
            else
            {
                var lobby = LocalMatchRegistry.Active;
                var localSlot = LocalMatchRegistry.LocalPlayerSlot ?? 0;
                setup = lobby != null
                    ? lobby.ToMatchSetup(localSlot)
                    : MatchSetup.Default;
            }

            GameSession.Begin(setup);
            await SceneManager.LoadSceneAsync(GameSceneNames.Game)
                .ToUniTask(cancellationToken: cancellationToken);
            _isTransitioning = false;
        }

        private async UniTask LoadSceneAsync(string sceneName, System.Threading.CancellationToken cancellationToken)
        {
            _isTransitioning = true;
            await FadeOutAsync(cancellationToken);
            await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: cancellationToken);
            _isTransitioning = false;
        }

        private async UniTask FadeOutAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (_lobbyScreen == null)
            {
                return;
            }

            _lobbyScreen.AddToClassList("lobby--hidden");
            var elapsed = 0f;
            _lobbyScreen.style.opacity = 1f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.deltaTime;
                _lobbyScreen.style.opacity = 1f - Mathf.Clamp01(elapsed / FadeDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
