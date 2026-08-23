using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using Game.UI;
using Game.UI.Animations;
using Game.UI.Bindings;
using Game.UI.ViewModels;
using Game.UI.Views;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const float FadeDuration = 0.35f;
        private const float IntroDuration = 0.4f;
        private const float SettingsAnimDuration = 0.24f;
        private const string OverlayHiddenClass = "ui-overlay--hidden";
        private const string ModeSelectedClass = "mm-mode--selected";
        private const string HubTabActiveClass = "mm__friends-tab--active";
        private const string DockTabActiveClass = "mm__dock-tab--active";

        private enum LeftMenuTab
        {
            Chat,
            MatchHistory,
            PublicGames,
            Settings,
        }

        [SerializeField] private UIDocument _uiDocument;

#if UNITY_EDITOR
        [Header("Editor preview — friends hub")]
        [SerializeField] private bool _previewFriendsHub;
        [SerializeField] private FriendsHubTab _previewFriendsTab = FriendsHubTab.Invites;
#endif

        private VisualElement _root;
        private MainMenuViewModel _viewModel;
        private UIBindingScope _bindingScope;
        private VisualElement _menuScreen;
        private VisualElement _menuBrand;
        private VisualElement _menuPanel;
        private VisualElement _settingsOverlay;
        private VisualElement _menuOverlayDim;
        private VisualElement _menuDialog;
        private Button _playButton;
        private Button _returnToMatchButton;
        private Button _settingsButton;
        private Button _quitButton;
        private Button _chatTabButton;
        private Button _matchHistoryTabButton;
        private Button _publicGamesTabButton;
        private Button _chatSendButton;
        private VisualElement _chatTabContent;
        private VisualElement _matchHistoryTabContent;
        private VisualElement _publicGamesTabContent;
        private VisualElement _settingsTabContent;
        private VisualElement _chatMessages;
        private TextField _chatInput;
        private LeftMenuTab _leftMenuTab = LeftMenuTab.Chat;
        private Button _settingsCloseButton;
        private Toggle _soundToggle;
        private Slider _volumeSlider;
        private Label _volumeValueLabel;
        private VisualElement _matchEntryOverlay;
        private PanelLoadingOverlay _lobbyEntryOverlay;
        private VisualElement _modeSelectOverlay;
        private VisualElement _joinCodeRow;
        private VisualElement _modeGrid;
        private VisualElement _modeDossierPreview;
        private Label _modeDossierTitle;
        private Label _modeDossierBody;
        private Label _modeDossierNote;
        private float _dossierMapPx;
        private int _dossierMappedPlayerCount;
        private Button _createMatchButton;
        private Button _joinMatchButton;
        private Button _joinConfirmButton;
        private Button _matchEntryCloseButton;
        private Button _modeSelectCloseButton;
        private TextField _joinCodeField;
        private Label _modeSelectErrorLabel;
        private Label _matchEntryErrorLabel;
        private Label _profileNameLabel;
        private Label _profileStatsLabel;
        private Label _profileRecordLabel;
        private Label _profileAvatarLabel;
        private VisualElement _profileAvatar;
        private VisualElement _profileBadge;
        private Label _friendsCountLabel;
        private Label _friendsErrorLabel;
        private VisualElement _friendsTabContent;
        private VisualElement _invitesTabContent;
        private Button _friendsTabButton;
        private Button _invitesTabButton;
        private VisualElement _incomingRequestsList;
        private VisualElement _friendsListContainer;
        private VisualElement _addFriendSection;
        private VisualElement _lobbyInviteBanner;
        private Label _lobbyInviteBannerLabel;
        private Button _lobbyInviteJoinButton;
        private Button _lobbyInviteDismissButton;
        private FriendsHubPanel _friendsHubPanel;
        private string _pendingLobbyInviteCode;
        private Label _versionLabel;
        private Label _profileEditErrorLabel;
        private VisualElement _hubPanel;
        private VisualElement _profileEditOverlay;
        private VisualElement _avatarGrid;
        private TextField _displayNameField;
        private TextField _friendIdField;
        private Button _saveProfileButton;
        private Button _editProfileButton;
        private Button _profileEditCloseButton;
        private Button _addFriendButton;
        private Button _gameTabButton;
        private Button _hubFriendsTabButton;
        private VisualElement _gameTabContent;
        private VisualElement _friendsHost;
        private bool _hubGameTabActive = true;
        private int _selectedPlayerCount = 4;
        private bool _isTransitioning;
        private bool _isSettingsOpen;
        private bool _isSettingsAnimating;
        private bool _isMatchEntryOpen;
        private bool _isModeSelectOpen;
        private bool _isProfileEditOpen;
        private bool _isJoinUiOpen;
        private int _pendingAvatarId;
        private bool _overlayBlocksMenu;
        private void Awake()
        {
            // Safety: Bootstrap already warms these; cheap if ready.
            PlayerProfileService.PrimeFromLocalPrefs();
            UnityServicesBootstrap.PrimeCachedPlayerNameFromPrefs();
            UnityServicesBootstrap.EnsureInitializedAsync().Forget();

            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            _viewModel = new MainMenuViewModel();
            _root = _uiDocument.rootVisualElement;
            _menuScreen = _root.Q<VisualElement>("MenuScreen");
            _menuBrand = _root.Q<VisualElement>("MenuBrand");
            _menuPanel = _root.Q<VisualElement>("MenuPanel");
            _settingsOverlay = _root.Q<VisualElement>("SettingsOverlay");
            _menuOverlayDim = _root.Q<VisualElement>("MenuOverlayDim");
            _menuDialog = _root.Q<VisualElement>("MenuDialog");
            _matchEntryOverlay = _root.Q<VisualElement>("MatchEntryOverlay");
            _lobbyEntryOverlay = new PanelLoadingOverlay(_root.Q<VisualElement>("LobbyEntryOverlay"));
            _modeSelectOverlay = _root.Q<VisualElement>("ModeSelectOverlay");
            _joinCodeRow = _root.Q<VisualElement>("JoinCodeRow");
            _modeGrid = _root.Q<VisualElement>("ModeGrid");
            _modeDossierPreview = _root.Q<VisualElement>("ModeDossierPreview");
            _modeDossierTitle = _root.Q<Label>("ModeDossierTitle");
            _modeDossierBody = _root.Q<Label>("ModeDossierBody");
            _modeDossierNote = _root.Q<Label>("ModeDossierNote");
            _createMatchButton = _root.Q<Button>("CreateMatchButton");
            _joinMatchButton = _root.Q<Button>("JoinMatchButton");
            _joinConfirmButton = _root.Q<Button>("JoinConfirmButton");
            _matchEntryCloseButton = _root.Q<Button>("MatchEntryCloseButton");
            _modeSelectCloseButton = _root.Q<Button>("ModeSelectCloseButton");
            _joinCodeField = _root.Q<TextField>("JoinCodeField");
            _modeSelectErrorLabel = _root.Q<Label>("ModeSelectErrorLabel");
            _matchEntryErrorLabel = _root.Q<Label>("MatchEntryErrorLabel");
            _profileNameLabel = _root.Q<Label>("ProfileNameLabel");
            _profileStatsLabel = _root.Q<Label>("ProfileStatsLabel");
            _profileRecordLabel = _root.Q<Label>("ProfileRecordLabel");
            _profileAvatarLabel = _root.Q<Label>("ProfileAvatarLabel");
            _profileAvatar = _root.Q<VisualElement>("ProfileAvatar");
            _profileBadge = _root.Q<VisualElement>("ProfileBadge");
            if (_profileBadge != null)
            {
                _profileBadge.style.minHeight = 120;
            }

            _friendsCountLabel = _root.Q<Label>("FriendsCountLabel");
            _friendsErrorLabel = _root.Q<Label>("FriendsErrorLabel");
            _friendsTabContent = _root.Q<VisualElement>("FriendsTabContent");
            _invitesTabContent = _root.Q<VisualElement>("InvitesTabContent");
            _friendsTabButton = _root.Q<Button>("FriendsTabButton");
            _invitesTabButton = _root.Q<Button>("InvitesTabButton");
            _incomingRequestsList = _root.Q<VisualElement>("IncomingRequestsList");
            _friendsListContainer = _root.Q<VisualElement>("FriendsListContainer");
            _addFriendSection = _root.Q<VisualElement>("AddFriendSection");
            _lobbyInviteBanner = _root.Q<VisualElement>("LobbyInviteBanner");
            _lobbyInviteBannerLabel = _root.Q<Label>("LobbyInviteBannerLabel");
            _lobbyInviteJoinButton = _root.Q<Button>("LobbyInviteJoinButton");
            _lobbyInviteDismissButton = _root.Q<Button>("LobbyInviteDismissButton");
            _versionLabel = _root.Q<Label>("VersionLabel");
            _profileEditErrorLabel = _root.Q<Label>("ProfileEditErrorLabel");
            _hubPanel = _root.Q<VisualElement>("HubPanel");
            _profileEditOverlay = _root.Q<VisualElement>("ProfileEditOverlay");
            _avatarGrid = _root.Q<VisualElement>("AvatarGrid");
            _displayNameField = _root.Q<TextField>("DisplayNameField");
            _friendIdField = _root.Q<TextField>("FriendIdField");
            _saveProfileButton = _root.Q<Button>("SaveProfileButton");
            _editProfileButton = _root.Q<Button>("EditProfileButton");
            _profileEditCloseButton = _root.Q<Button>("ProfileEditCloseButton");
            _addFriendButton = _root.Q<Button>("AddFriendButton");
            _gameTabButton = _root.Q<Button>("GameTabButton");
            _hubFriendsTabButton = _root.Q<Button>("HubFriendsTabButton");
            _gameTabContent = _root.Q<VisualElement>("GameTabContent");
            _friendsHost = _root.Q<VisualElement>("FriendsHost");
            BindFriendsHubPanel();
            BindLobbyInviteBanner();
#if UNITY_EDITOR
            ApplyEditorFriendsHubPreview();
#endif

            if (_versionLabel != null)
            {
                _versionLabel.text = GameUpdateUiRules.FormatVersionLabel(GameLocalVersion.Current);
            }

            StyleHubTextField(_displayNameField);
            StyleHubTextField(_friendIdField);
            StyleHubTextField(_joinCodeField);
            BuildAvatarGrid();
            RefreshProfileLabels();

            var titleLabel = _root.Q<Label>("TitleLabel");
            _playButton = _root.Q<Button>("PlayButton");
            _returnToMatchButton = _root.Q<Button>("ReturnToMatchButton");
            _settingsButton = _root.Q<Button>("SettingsButton");
            _quitButton = _root.Q<Button>("QuitButton");
            _chatTabButton = _root.Q<Button>("ChatTabButton");
            _matchHistoryTabButton = _root.Q<Button>("MatchHistoryTabButton");
            _publicGamesTabButton = _root.Q<Button>("PublicGamesTabButton");
            _chatSendButton = _root.Q<Button>("ChatSendButton");
            _chatTabContent = _root.Q<VisualElement>("ChatTabContent");
            _matchHistoryTabContent = _root.Q<VisualElement>("MatchHistoryTabContent");
            _publicGamesTabContent = _root.Q<VisualElement>("PublicGamesTabContent");
            _settingsTabContent = _root.Q<VisualElement>("SettingsTabContent");
            _chatMessages = _root.Q<VisualElement>("ChatMessages");
            _chatInput = _root.Q<TextField>("ChatInput");
            _settingsCloseButton = _root.Q<Button>("SettingsCloseButton");
            _soundToggle = _root.Q<Toggle>("SoundToggle");
            _volumeSlider = _root.Q<Slider>("VolumeSlider");
            _volumeValueLabel = _root.Q<Label>("VolumeValueLabel");

            StyleHubTextField(_chatInput);
            if (_chatInput != null)
            {
                _chatInput.multiline = false;
                _chatInput.textEdition.placeholder = "Написать в чат…";
            }

            if (_joinCodeField != null)
            {
                _joinCodeField.textEdition.placeholder = "Код комнаты";
            }

            if (_friendIdField != null)
            {
                _friendIdField.textEdition.placeholder = "Добавить по имени (Ник#1234)";
            }

            GameAudio.Apply();
            BindSettingsUi();
            BindMatchEntryUi();
            BindHubUi();
            BuildModeGrid();
            ApplyHubTabVisibility();
            ApplyLeftMenuTabVisibility();
            BindMenuChat();
            EnsureSettingsClosed();
            EnsureMatchEntryClosed();
            EnsureModeSelectClosed();
            EnsureProfileEditClosed();
            EnsureLobbyEntryClosed();
            EnsureVisibleRestState();
            BindHubFromCacheAsync().Forget();

            _bindingScope = new UIBindingScope(_root);
            if (titleLabel != null)
            {
                _bindingScope.Add(_viewModel.Title.SubscribeToText(titleLabel));
            }

            if (_playButton != null)
            {
                _bindingScope.Add(_viewModel.PlayCommand.BindTo(_playButton));
                _bindingScope.Add(_viewModel.PlayCommand.Subscribe(_ => OnPlayRequested()));
            }

            if (_quitButton != null)
            {
                _bindingScope.Add(_viewModel.QuitCommand.BindTo(_quitButton));
                _bindingScope.Add(_viewModel.QuitCommand.Subscribe(_ => OnQuitRequested()));
            }
        }

        private void BindSettingsUi()
        {
            if (_soundToggle != null)
            {
                _soundToggle.value = GameAudio.SoundEnabled;
                _soundToggle.RegisterValueChangedCallback(evt =>
                {
                    GameAudio.SoundEnabled = evt.newValue;
                    GameAudio.Apply();
                });
            }

            if (_volumeSlider != null)
            {
                _volumeSlider.SetValueWithoutNotify(GameAudio.Volume);
                UpdateVolumeLabel(GameAudio.Volume);
                _volumeSlider.RegisterValueChangedCallback(evt =>
                {
                    GameAudio.Volume = evt.newValue;
                    GameAudio.Apply();
                    UpdateVolumeLabel(evt.newValue);
                });
            }
        }

        private void UpdateVolumeLabel(float volume)
        {
            if (_volumeValueLabel != null)
            {
                _volumeValueLabel.text = $"{Mathf.RoundToInt(volume * 100f)}%";
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_root == null)
            {
                return;
            }

            ApplyEditorFriendsHubPreview();
        }
#endif

        private void OnEnable()
        {
            GameSession.Reset();

            if (_settingsButton != null)
            {
                _settingsButton.clicked += OnSettingsOpen;
            }

            if (_chatTabButton != null)
            {
                _chatTabButton.clicked += OnChatTabClicked;
            }

            if (_matchHistoryTabButton != null)
            {
                _matchHistoryTabButton.clicked += OnMatchHistoryTabClicked;
            }

            if (_publicGamesTabButton != null)
            {
                _publicGamesTabButton.clicked += OnPublicGamesTabClicked;
            }

            if (_chatSendButton != null)
            {
                _chatSendButton.clicked += OnChatSendClicked;
            }

            if (_chatInput != null)
            {
                _chatInput.RegisterCallback<KeyDownEvent>(OnChatInputKeyDown);
            }

            if (_settingsCloseButton != null)
            {
                _settingsCloseButton.clicked += OnSettingsClose;
            }

            if (_returnToMatchButton != null)
            {
                _returnToMatchButton.clicked += OnReturnToMatchClicked;
            }

            if (_gameTabButton != null)
            {
                _gameTabButton.clicked += OnGameHubTabClicked;
            }

            if (_hubFriendsTabButton != null)
            {
                _hubFriendsTabButton.clicked += OnFriendsHubTabClicked;
            }

            RefreshReturnToMatchButton();

            _root?.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _modeDossierPreview?.RegisterCallback<GeometryChangedEvent>(OnModeDossierPreviewGeometry);
            FriendsHubService.LobbyInviteReceived += OnLobbyInviteReceived;
            UnityServicesBootstrap.PlayerNameChanged += OnPlayerNameChanged;
            _profileBadge?.RegisterCallback<ClickEvent>(OnProfileBadgeClicked);
            NotifySessionFlowIfIdle();
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            PlayIntroAsync(cancellationToken).Forget();
            RestoreIntroIfStalledAsync(cancellationToken).Forget();
        }

        private static void NotifySessionFlowIfIdle()
        {
            var hasActiveSession = MatchNetworkSession.HasHandle
                || MatchNetworkSession.IsNetworked
                || LocalMatchRegistry.Active != null;
            if (hasActiveSession)
            {
                return;
            }

            SessionFlowTracker.NotifyChanged();
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            _modeDossierPreview?.UnregisterCallback<GeometryChangedEvent>(OnModeDossierPreviewGeometry);
            FriendsHubService.LobbyInviteReceived -= OnLobbyInviteReceived;
            UnityServicesBootstrap.PlayerNameChanged -= OnPlayerNameChanged;
            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsOpen;
            }

            if (_chatTabButton != null)
            {
                _chatTabButton.clicked -= OnChatTabClicked;
            }

            if (_matchHistoryTabButton != null)
            {
                _matchHistoryTabButton.clicked -= OnMatchHistoryTabClicked;
            }

            if (_publicGamesTabButton != null)
            {
                _publicGamesTabButton.clicked -= OnPublicGamesTabClicked;
            }

            if (_chatSendButton != null)
            {
                _chatSendButton.clicked -= OnChatSendClicked;
            }

            if (_chatInput != null)
            {
                _chatInput.UnregisterCallback<KeyDownEvent>(OnChatInputKeyDown);
            }

            if (_settingsCloseButton != null)
            {
                _settingsCloseButton.clicked -= OnSettingsClose;
            }

            if (_returnToMatchButton != null)
            {
                _returnToMatchButton.clicked -= OnReturnToMatchClicked;
            }

            if (_gameTabButton != null)
            {
                _gameTabButton.clicked -= OnGameHubTabClicked;
            }

            if (_hubFriendsTabButton != null)
            {
                _hubFriendsTabButton.clicked -= OnFriendsHubTabClicked;
            }

            _profileBadge?.UnregisterCallback<ClickEvent>(OnProfileBadgeClicked);
            EnsureLobbyEntryClosed();
        }

        private void OnDestroy()
        {
            _bindingScope?.Dispose();
            _friendsHubPanel?.Dispose();
            _lobbyEntryOverlay?.Dispose();
        }

        private async UniTask PlayIntroAsync(System.Threading.CancellationToken cancellationToken)
        {
            PrepareIntroState();

            try
            {
                var tasks = new List<UniTask>();
                if (_menuBrand != null)
                {
                    tasks.Add(UiToolkitElementAnimator.FadeAsync(
                        _menuBrand,
                        0f,
                        1f,
                        IntroDuration,
                        cancellationToken: cancellationToken));
                }

                if (_menuPanel != null)
                {
                    tasks.Add(UiToolkitElementAnimator.FadeScaleAsync(
                        _menuPanel,
                        0f,
                        1f,
                        new Vector2(0.94f, 0.94f),
                        Vector2.one,
                        IntroDuration,
                        bounce: false,
                        cancellationToken: cancellationToken));
                }

                if (_hubPanel != null)
                {
                    tasks.Add(UiToolkitElementAnimator.FadeAsync(
                        _hubPanel,
                        0f,
                        1f,
                        IntroDuration,
                        cancellationToken: cancellationToken));
                }

                if (tasks.Count > 0)
                {
                    await UniTask.WhenAll(tasks);
                }

                _playButton?.Focus();
            }
            finally
            {
                EnsureVisibleRestState();
            }
        }

        private async UniTask RestoreIntroIfStalledAsync(System.Threading.CancellationToken cancellationToken)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(IntroDuration + 0.6f),
                ignoreTimeScale: true,
                cancellationToken: cancellationToken);
            EnsureVisibleRestState();
        }

        private void EnsureSettingsClosed()
        {
            _isSettingsOpen = false;
            _isSettingsAnimating = false;
            if (_settingsOverlay != null)
            {
                _settingsOverlay.AddToClassList(OverlayHiddenClass);
            }

            if (_menuOverlayDim != null)
            {
                _menuOverlayDim.style.opacity = 0f;
            }

            if (_menuDialog != null)
            {
                _menuDialog.style.opacity = 0f;
            }
        }

        private void EnsureVisibleRestState()
        {
            if (_menuScreen != null)
            {
                _menuScreen.style.opacity = 1f;
            }

            if (_menuBrand != null)
            {
                _menuBrand.style.opacity = 1f;
                _menuBrand.style.translate = new Translate(0f, 0f);
                _menuBrand.style.scale = new Scale(Vector3.one);
            }

            if (_menuPanel != null)
            {
                _menuPanel.style.opacity = 1f;
                _menuPanel.style.translate = new Translate(0f, 0f);
                _menuPanel.style.scale = new Scale(Vector3.one);
            }

            if (_hubPanel != null)
            {
                _hubPanel.style.opacity = 1f;
                _hubPanel.style.translate = new Translate(0f, 0f);
                _hubPanel.style.scale = new Scale(Vector3.one);
            }

            foreach (var button in new[] { _playButton, _settingsButton, _quitButton })
            {
                if (button == null)
                {
                    continue;
                }

                button.style.opacity = 1f;
                button.style.scale = new Scale(Vector3.one);
            }

            if (!_isSettingsOpen)
            {
                EnsureSettingsClosed();
            }
        }

        private void PrepareIntroState()
        {
            if (_menuBrand != null)
            {
                _menuBrand.style.opacity = 0f;
            }

            if (_menuPanel != null)
            {
                _menuPanel.style.opacity = 0f;
                _menuPanel.style.scale = new Scale(new Vector3(0.94f, 0.94f, 1f));
            }

            if (_hubPanel != null)
            {
                _hubPanel.style.opacity = 0f;
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_isTransitioning || _isSettingsAnimating)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                if (_isProfileEditOpen)
                {
                    CloseProfileEdit();
                }
                else if (_isSettingsOpen)
                {
                    OnSettingsClose();
                }
                else if (_isModeSelectOpen)
                {
                    CloseModeSelect();
                }
                else if (_isMatchEntryOpen)
                {
                    CloseMatchEntry();
                }
                else if (_isJoinUiOpen)
                {
                    CloseJoinUi();
                }
                else
                {
                    OnQuitRequested();
                }

                return;
            }

            if (_isSettingsOpen || _isMatchEntryOpen || _isModeSelectOpen || _isProfileEditOpen)
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    evt.StopPropagation();
                    if (_isJoinUiOpen)
                    {
                        JoinMatchAsync(this.GetCancellationTokenOnDestroy()).Forget();
                        break;
                    }

                    if (IsChatInputFocused())
                    {
                        break;
                    }

                    OnPlayRequested();
                    break;
                case KeyCode.Space:
                    if (_isJoinUiOpen)
                    {
                        break;
                    }

                    evt.StopPropagation();
                    OnPlayRequested();
                    break;
            }
        }

        private void OnSettingsOpen()
        {
            if (_isTransitioning || _isProfileEditOpen)
            {
                return;
            }

            ShowLeftMenuTab(LeftMenuTab.Settings);
        }

        private void OnSettingsClose()
        {
            if (!_isSettingsOpen || _settingsOverlay == null || _isSettingsAnimating)
            {
                return;
            }

            HideSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask ShowSettingsAsync(System.Threading.CancellationToken cancellationToken)
        {
            _isSettingsAnimating = true;
            _isSettingsOpen = true;
            _settingsOverlay.RemoveFromClassList(OverlayHiddenClass);
            SetMainMenuInteractable(false);

            if (_menuOverlayDim != null)
            {
                _menuOverlayDim.style.opacity = 0f;
            }

            if (_menuDialog != null)
            {
                _menuDialog.style.opacity = 0f;
                _menuDialog.style.scale = new Scale(new Vector3(0.94f, 0.94f, 1f));
            }

            await UniTask.WhenAll(
                UiToolkitElementAnimator.FadeAsync(
                    _menuOverlayDim,
                    0f,
                    1f,
                    SettingsAnimDuration,
                    cancellationToken: cancellationToken),
                UiToolkitElementAnimator.FadeScaleAsync(
                    _menuDialog,
                    0f,
                    1f,
                    new Vector2(0.94f, 0.94f),
                    Vector2.one,
                    SettingsAnimDuration,
                    bounce: true,
                    cancellationToken: cancellationToken));

            _isSettingsAnimating = false;
            _settingsCloseButton?.Focus();
        }

        private async UniTask HideSettingsAsync(System.Threading.CancellationToken cancellationToken)
        {
            _isSettingsAnimating = true;

            await UniTask.WhenAll(
                UiToolkitElementAnimator.FadeAsync(
                    _menuOverlayDim,
                    1f,
                    0f,
                    SettingsAnimDuration,
                    cancellationToken: cancellationToken),
                UiToolkitElementAnimator.FadeScaleAsync(
                    _menuDialog,
                    1f,
                    0f,
                    Vector2.one,
                    new Vector2(0.94f, 0.94f),
                    SettingsAnimDuration,
                    cancellationToken: cancellationToken));

            _isSettingsOpen = false;
            _settingsOverlay.AddToClassList(OverlayHiddenClass);
            SetMainMenuInteractable(true);
            _isSettingsAnimating = false;
            _playButton?.Focus();
        }

        private void OnPlayRequested()
        {
            if (_isTransitioning || _isSettingsOpen || _isSettingsAnimating || _isMatchEntryOpen || _isProfileEditOpen || _isJoinUiOpen)
            {
                return;
            }

            if (!MatchModeRules.IsModeSelectable(_selectedPlayerCount))
            {
                ShowGameHubTab();
                ShowModeSelectError("Выберите режим.");
                return;
            }

            CreateMatchAsync(_selectedPlayerCount, this.GetCancellationTokenOnDestroy()).Forget();
        }

        void RefreshReturnToMatchButton()
        {
            if (_returnToMatchButton == null)
            {
                return;
            }

            var show = PendingMatchReconnectStore.TryLoadActive(out _);
            _returnToMatchButton.EnableInClassList(OverlayHiddenClass, !show);
            _returnToMatchButton.SetEnabled(show && !_overlayBlocksMenu);
        }

        void OnReturnToMatchClicked()
        {
            if (_isTransitioning || _overlayBlocksMenu)
            {
                return;
            }

            ReturnToMatchAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTask ReturnToMatchAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (_isTransitioning)
            {
                return;
            }

            _isTransitioning = true;
            ShowLobbyEntry();
            try
            {
                if (!await MatchNetworkSession.TryReturnToPendingMatchAsync())
                {
                    throw new System.InvalidOperationException("Не удалось вернуться в матч.");
                }

                SessionFlowTracker.NotifyChanged();
                await LoadSceneWithFadeAsync(GameSceneNames.Lobby, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                MatchNetworkSession.Shutdown();
                PendingMatchReconnectStore.Clear();
                Debug.LogWarning($"Return to match failed: {ex.Message}");
                _isTransitioning = false;
                EnsureLobbyEntryClosed();
                RefreshReturnToMatchButton();
                ShowMatchEntryError("Матч уже недоступен.");
            }
        }

        private void BindHubUi()
        {
            if (_editProfileButton != null)
            {
                _editProfileButton.clicked += OpenProfileEdit;
            }

            if (_profileEditCloseButton != null)
            {
                _profileEditCloseButton.clicked += CloseProfileEdit;
            }

            if (_saveProfileButton != null)
            {
                _saveProfileButton.clicked += () => SaveProfileAsync().Forget();
            }
        }

        private void BuildAvatarGrid()
        {
            if (_avatarGrid == null)
            {
                return;
            }

            _avatarGrid.Clear();
            _avatarGrid.style.flexDirection = FlexDirection.Row;
            _avatarGrid.style.flexWrap = Wrap.Wrap;
            _avatarGrid.style.justifyContent = Justify.Center;

            for (var i = 0; i < PlayerProfileService.AvatarCount; i++)
            {
                var avatarId = i;
                var button = new Button
                {
                    name = $"AvatarOption_{avatarId}",
                    text = PlayerProfileService.GetAvatarGlyph(avatarId),
                };
                button.AddToClassList("mm-avatar-option");
                button.style.flexShrink = 0;
                button.style.backgroundColor = PlayerProfileService.GetAvatarColor(avatarId);
                button.clicked += () => SelectPendingAvatar(avatarId);
                _avatarGrid.Add(button);
            }
        }

        private void SelectPendingAvatar(int avatarId)
        {
            _pendingAvatarId = PlayerProfileService.ClampAvatarId(avatarId);
            RefreshAvatarSelectionUi();
        }

        private void RefreshAvatarSelectionUi()
        {
            if (_avatarGrid == null)
            {
                return;
            }

            foreach (var child in _avatarGrid.Children())
            {
                if (child is not Button button)
                {
                    continue;
                }

                var selected = button.name == $"AvatarOption_{_pendingAvatarId}";
                button.EnableInClassList("mm-avatar-option--selected", selected);
            }
        }

        private void OpenProfileEdit()
        {
            if (_profileEditOverlay == null || _isProfileEditOpen || _isTransitioning)
            {
                return;
            }

            _isProfileEditOpen = true;
            _pendingAvatarId = PlayerProfileService.AvatarId;
            if (_displayNameField != null)
            {
                _displayNameField.value = PlayerProfileService.DisplayName;
            }

            if (_profileEditErrorLabel != null)
            {
                _profileEditErrorLabel.text = string.Empty;
            }

            RefreshAvatarSelectionUi();
            _profileEditOverlay.RemoveFromClassList(OverlayHiddenClass);
            SetMainMenuInteractable(false);
            _displayNameField?.Focus();
        }

        private void CloseProfileEdit()
        {
            EnsureProfileEditClosed();
            SetMainMenuInteractable(true);
            _playButton?.Focus();
        }

        private void EnsureProfileEditClosed()
        {
            _isProfileEditOpen = false;
            _profileEditOverlay?.AddToClassList(OverlayHiddenClass);
            if (_profileEditErrorLabel != null)
            {
                _profileEditErrorLabel.text = string.Empty;
            }
        }

        private static void StyleHubTextField(TextField field)
        {
            if (field == null)
            {
                return;
            }

            var ink = new Color(8f / 255f, 9f / 255f, 8f / 255f, 1f);
            var cream = new Color(222f / 255f, 219f / 255f, 210f / 255f, 1f);
            field.style.backgroundColor = ink;
            field.style.color = cream;

            var input = field.Q(className: "unity-base-text-field__input")
                        ?? field.Q(className: "unity-text-field__input")
                        ?? field.Q(className: "unity-base-field__input");
            if (input == null)
            {
                return;
            }

            input.style.backgroundColor = ink;
            input.style.color = cream;
            input.style.borderTopWidth = 0;
            input.style.borderBottomWidth = 0;
            input.style.borderLeftWidth = 0;
            input.style.borderRightWidth = 0;
        }

        private async UniTaskVoid BindHubFromCacheAsync()
        {
            try
            {
                await UnityServicesBootstrap.EnsureInitializedAsync();
                await RefreshProfilePlayerNameAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"BindHubFromCacheAsync: {ex.Message}");
            }

            if (this == null)
            {
                return;
            }

            RefreshProfileLabels();
            _friendsHubPanel?.Refresh();

            if (!UnityServicesBootstrap.IsReady
                && _friendsErrorLabel != null
                && !string.IsNullOrEmpty(UnityServicesBootstrap.LastInitError))
            {
                SetFriendsStatus(
                    "Друзья недоступны: нет связи с Unity Auth. Суффикс #xxxx появится после входа.");
            }

            ScheduleProfileNameRetryAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid ScheduleProfileNameRetryAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (FriendsHubRules.IsValidUgsPlayerName(UnityServicesBootstrap.PlayerName))
            {
                return;
            }

            var delaySec = UnityServicesBootstrap.GetRecommendedRetryDelaySeconds(2.5f);
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delaySec),
                ignoreTimeScale: true,
                cancellationToken: cancellationToken);
            if (FriendsHubRules.IsValidUgsPlayerName(UnityServicesBootstrap.PlayerName))
            {
                return;
            }

            try
            {
                await RefreshProfilePlayerNameAsync();
                RefreshProfileLabels();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Profile name retry skipped: {ex.Message}");
            }
        }

        private void BindFriendsHubPanel()
        {
            _friendsHubPanel?.Dispose();
            _friendsHubPanel = new FriendsHubPanel(
                FriendsHubPanelMode.Full,
                _friendsListContainer,
                _friendsTabContent,
                _invitesTabContent,
                _friendsTabButton,
                _invitesTabButton,
                _incomingRequestsList,
                _friendsCountLabel,
                _friendIdField,
                _addFriendButton,
                _friendsErrorLabel,
                _addFriendSection);
            _friendsHubPanel.JoinLobbyRequested += OnJoinFriendLobbyRequested;
            _friendsHubPanel.Bind();
        }

        private void OnPlayerNameChanged() => RefreshProfileLabels();

        private async UniTask<string> RefreshProfilePlayerNameAsync()
        {
            try
            {
                await UnityServicesBootstrap.EnsureInitializedAsync();
            }
            catch (System.Exception initEx)
            {
                Debug.LogWarning($"UGS init for player name: {initEx.Message}");
            }

            if (!UnityServicesBootstrap.IsReady)
            {
                var cached = UnityServicesBootstrap.PlayerName;
                return cached;
            }

            try
            {
                var playerName = UnityServicesBootstrap.PlayerName;
                if (!FriendsHubRules.IsValidUgsPlayerName(playerName))
                {
                    playerName = await UnityServicesBootstrap.EnsurePlayerNameAsync();
                }

                if (PlayerProfileService.IsLoaded
                    && FriendsHubRules.ShouldSyncDisplayNameToUgs(playerName, PlayerProfileService.DisplayName))
                {
                    playerName = await UnityServicesBootstrap.TrySyncPlayerNameFromDisplayNameAsync(
                        PlayerProfileService.DisplayName);
                }

                return playerName;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"UGS player name unavailable: {ex.Message}");
                return UnityServicesBootstrap.PlayerName;
            }
        }

        private void OnProfileBadgeClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            CopyProfileNameAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask CopyProfileNameAsync(System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var fullName = await RefreshProfilePlayerNameAsync();
                var isValid = FriendsHubRules.IsValidUgsPlayerName(fullName);
                if (!isValid)
                {
                    SetFriendsStatus(
                        "Имя для друзей ещё не готово. Сохраните профиль или проверьте UGS/сеть.");

                    return;
                }

                GUIUtility.systemCopyBuffer = fullName;
                SetFriendsStatus("Имя скопировано.");

                await FlashProfileCopiedAsync(cancellationToken);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"CopyProfileNameAsync: {ex.Message}");
            }
        }

        private async UniTask FlashProfileCopiedAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (_profileBadge == null)
            {
                return;
            }

            _profileBadge.AddToClassList("mm__profile-badge--copied");
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(1.2f),
                ignoreTimeScale: true,
                cancellationToken: cancellationToken);
            _profileBadge.RemoveFromClassList("mm__profile-badge--copied");
        }

        private void BindLobbyInviteBanner()
        {
            if (_lobbyInviteJoinButton != null)
            {
                _lobbyInviteJoinButton.clicked += OnLobbyInviteJoinClicked;
            }

            if (_lobbyInviteDismissButton != null)
            {
                _lobbyInviteDismissButton.clicked += DismissLobbyInviteBanner;
            }
        }

        private void OnJoinFriendLobbyRequested(string lobbyCode)
        {
            if (_isTransitioning || string.IsNullOrWhiteSpace(lobbyCode))
            {
                return;
            }

            EnsureModeSelectClosed();
            EnsureMatchEntryClosed();
            JoinMatchByCodeAsync(
                lobbyCode,
                this.GetCancellationTokenOnDestroy(),
                showJoinUiOnError: true).Forget();
        }

        private void OnLobbyInviteReceived(FriendsLobbyInvite invite)
        {
            _pendingLobbyInviteCode = invite.LobbyCode;
            if (_lobbyInviteBannerLabel != null)
            {
                var sender = string.IsNullOrWhiteSpace(invite.SenderName)
                    ? FriendsHubRules.ShortPlayerId(invite.SenderPlayerId)
                    : invite.SenderName;
                _lobbyInviteBannerLabel.text =
                    $"{sender} приглашает в лобби · {invite.LobbyCode}";
            }

            _lobbyInviteBanner?.RemoveFromClassList(OverlayHiddenClass);
        }

        private void OnLobbyInviteJoinClicked()
        {
            if (string.IsNullOrWhiteSpace(_pendingLobbyInviteCode))
            {
                DismissLobbyInviteBanner();
                return;
            }

            var code = _pendingLobbyInviteCode;
            DismissLobbyInviteBanner();
            OnJoinFriendLobbyRequested(code);
        }

        private void DismissLobbyInviteBanner()
        {
            _pendingLobbyInviteCode = null;
            _lobbyInviteBanner?.AddToClassList(OverlayHiddenClass);
        }

#if UNITY_EDITOR
        /// <summary>Inspector / edit-mode preview for friends tabs and invite rows.</summary>
        public void ApplyEditorFriendsHubPreview()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _root ?? _uiDocument?.rootVisualElement;
            if (root == null)
            {
                return;
            }

            _root = root;
            if (_friendsHubPanel == null)
            {
                _friendsCountLabel = _root.Q<Label>("FriendsCountLabel");
                _friendsErrorLabel = _root.Q<Label>("FriendsErrorLabel");
                _friendsTabContent = _root.Q<VisualElement>("FriendsTabContent");
                _invitesTabContent = _root.Q<VisualElement>("InvitesTabContent");
                _friendsTabButton = _root.Q<Button>("FriendsTabButton");
                _invitesTabButton = _root.Q<Button>("InvitesTabButton");
                _incomingRequestsList = _root.Q<VisualElement>("IncomingRequestsList");
                _friendsListContainer = _root.Q<VisualElement>("FriendsListContainer");
                _addFriendSection = _root.Q<VisualElement>("AddFriendSection");
                _friendIdField = _root.Q<TextField>("FriendIdField");
                _addFriendButton = _root.Q<Button>("AddFriendButton");
                BindFriendsHubPanel();
            }

            if (_previewFriendsHub)
            {
                ShowFriendsHubTab();
                _friendsHubPanel.ApplyDesignPreview(_previewFriendsTab);
            }
            else
            {
                _friendsHubPanel.ClearDesignPreview();
            }
        }
#endif

        private void RefreshProfileLabels()
        {
            var displayName = PlayerProfileService.DisplayName;
            var ugsName = UnityServicesBootstrap.PlayerName;
            var avatarId = PlayerProfileService.AvatarId;

            if (_profileNameLabel != null)
            {
                _profileNameLabel.enableRichText = true;
                _profileNameLabel.text = FriendsHubRules.FormatProfileNameRichText(displayName, ugsName);
            }

            if (_profileStatsLabel != null)
            {
                _profileStatsLabel.text =
                    $"Ранг {PlayerProfileService.Rank} · Очки {PlayerProfileService.Points}";
            }

            if (_profileRecordLabel != null)
            {
                _profileRecordLabel.text =
                    $"Матчи {PlayerProfileService.Matches} · Победы {PlayerProfileService.Wins} · Поражения {PlayerProfileService.Losses}";
            }

            if (_profileAvatar != null)
            {
                _profileAvatar.style.backgroundColor = PlayerProfileService.GetAvatarColor(avatarId);
            }

            if (_profileAvatarLabel != null)
            {
                _profileAvatarLabel.text = PlayerProfileService.GetAvatarGlyph(avatarId);
            }

            if (_displayNameField != null && !_isProfileEditOpen)
            {
                _displayNameField.value = displayName;
            }

            _pendingAvatarId = avatarId;
            RefreshAvatarSelectionUi();
        }

        private async UniTaskVoid SaveProfileAsync()
        {
            if (_profileEditErrorLabel != null)
            {
                _profileEditErrorLabel.text = string.Empty;
            }

            try
            {
                var name = _displayNameField?.value ?? "Player";
                await PlayerProfileService.SaveProfileAsync(name, _pendingAvatarId);
                RefreshProfileLabels();

                try
                {
                    await RefreshProfilePlayerNameAsync();
                }
                catch (System.Exception nameEx)
                {
                    Debug.LogWarning($"SaveProfileAsync UGS name sync: {nameEx.Message}");
                }

                RefreshProfileLabels();
                _friendsHubPanel?.Refresh();
                CloseProfileEdit();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"SaveProfileAsync: {ex.Message}");
                if (_profileEditErrorLabel != null)
                {
                    _profileEditErrorLabel.text = "Не удалось сохранить профиль.";
                }
            }
        }

        private void BindMatchEntryUi()
        {
            if (_createMatchButton != null)
            {
                _createMatchButton.clicked += OnCreateMatchClicked;
            }

            if (_joinMatchButton != null)
            {
                _joinMatchButton.clicked += OnJoinMatchClicked;
            }

            if (_joinConfirmButton != null)
            {
                _joinConfirmButton.clicked += () => JoinMatchAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }

            if (_matchEntryCloseButton != null)
            {
                _matchEntryCloseButton.clicked += CloseMatchEntry;
            }

            if (_modeSelectCloseButton != null)
            {
                _modeSelectCloseButton.clicked += CloseModeSelect;
            }
        }

        private void BuildModeGrid()
        {
            if (_modeGrid == null)
            {
                return;
            }

            _modeGrid.Clear();

            var row = CreateModeRow();
            for (var n = MatchModeRules.MinPlayers; n <= MatchModeRules.MaxPlayers; n++)
            {
                var playerCount = n;
                var button = ModeMapThumbnailBuilder.BuildModeButton(playerCount);
                if (n < MatchModeRules.MaxPlayers)
                {
                    button.AddToClassList("mm-mode--gap");
                }

                if (MatchModeRules.IsModeSelectable(playerCount))
                {
                    button.clicked += () => SelectMode(playerCount);
                }

                row.Add(button);
            }

            _modeGrid.Add(row);
            SelectMode(MatchModeRules.IsModeSelectable(4) ? 4 : 2);
        }

        private void SelectMode(int playerCount)
        {
            if (!MatchModeRules.IsModeSelectable(playerCount))
            {
                return;
            }

            _selectedPlayerCount = playerCount;
            if (_modeGrid == null)
            {
                return;
            }

            foreach (var button in _modeGrid.Query<Button>(className: "mm-mode").ToList())
            {
                button.EnableInClassList(ModeSelectedClass, button.name == $"Mode_N{playerCount}");
            }

            RefreshModeDossier();
        }

        private void OnModeDossierPreviewGeometry(GeometryChangedEvent evt)
        {
            var size = Mathf.Floor(Mathf.Min(evt.newRect.width, evt.newRect.height));
            if (size < 8f || Mathf.Abs(size - _dossierMapPx) < 1.5f)
            {
                return;
            }

            _dossierMapPx = size;
            RebuildModeDossierMap();
        }

        private void RefreshModeDossier()
        {
            if (_modeDossierTitle != null)
            {
                _modeDossierTitle.text = MatchModeRules.GetModeTitle(_selectedPlayerCount);
            }

            if (_modeDossierBody != null)
            {
                _modeDossierBody.text = MatchModeRules.GetModeSummary(_selectedPlayerCount);
            }

            if (_modeDossierNote != null)
            {
                _modeDossierNote.text = MatchModeRules.ModeMapNote;
            }

            RebuildModeDossierMap();
        }

        private void RebuildModeDossierMap()
        {
            if (_modeDossierPreview == null)
            {
                return;
            }

            var size = _dossierMapPx;
            if (size < 8f)
            {
                var layout = _modeDossierPreview.layout;
                size = Mathf.Floor(Mathf.Min(layout.width, layout.height));
            }

            if (size < 8f)
            {
                return;
            }

            if (_dossierMappedPlayerCount == _selectedPlayerCount
                && Mathf.Abs(_dossierMapPx - size) < 1.5f
                && _modeDossierPreview.childCount > 0)
            {
                return;
            }

            _dossierMapPx = size;
            _dossierMappedPlayerCount = _selectedPlayerCount;
            _modeDossierPreview.Clear();
            var map = ModeMapThumbnailBuilder.BuildPreview(_selectedPlayerCount, size);
            map.pickingMode = PickingMode.Ignore;
            _modeDossierPreview.Add(map);
        }

        private static VisualElement CreateModeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("mm-modes__row");
            return row;
        }

        private void OpenMatchEntry()
        {
            if (_matchEntryOverlay == null)
            {
                return;
            }

            _isMatchEntryOpen = true;
            _joinCodeRow?.AddToClassList(OverlayHiddenClass);
            ClearMatchEntryError();
            _matchEntryOverlay.RemoveFromClassList(OverlayHiddenClass);
            SetMainMenuInteractable(false);
            _createMatchButton?.Focus();
        }

        private void CloseMatchEntry()
        {
            EnsureMatchEntryClosed();
            SetMainMenuInteractable(true);
            _playButton?.Focus();
        }

        private void EnsureMatchEntryClosed()
        {
            _isMatchEntryOpen = false;
            _matchEntryOverlay?.AddToClassList(OverlayHiddenClass);
            CloseJoinUi();
            ClearMatchEntryError();
        }

        private void OnCreateMatchClicked()
        {
            EnsureMatchEntryClosed();
            ShowGameHubTab();
        }

        private void OnJoinMatchClicked()
        {
            ShowGameHubTab();
            OpenJoinUi();
        }

        private void OpenJoinUi()
        {
            _isJoinUiOpen = true;
            _playButton?.AddToClassList(OverlayHiddenClass);
            _joinMatchButton?.AddToClassList(OverlayHiddenClass);
            _joinCodeRow?.RemoveFromClassList(OverlayHiddenClass);
            _joinConfirmButton?.RemoveFromClassList(OverlayHiddenClass);
            _joinCodeField?.Focus();
        }

        private void CloseJoinUi()
        {
            _isJoinUiOpen = false;
            _playButton?.RemoveFromClassList(OverlayHiddenClass);
            _joinMatchButton?.RemoveFromClassList(OverlayHiddenClass);
            _joinCodeRow?.AddToClassList(OverlayHiddenClass);
            _joinConfirmButton?.AddToClassList(OverlayHiddenClass);
            _playButton?.Focus();
        }

        private void OnGameHubTabClicked() => ShowGameHubTab();

        private void OnFriendsHubTabClicked() => ShowFriendsHubTab();

        private void ShowGameHubTab()
        {
            _hubGameTabActive = true;
            ApplyHubTabVisibility();
        }

        private void ShowFriendsHubTab()
        {
            _hubGameTabActive = false;
            ApplyHubTabVisibility();
        }

        private void ApplyHubTabVisibility()
        {
            _gameTabContent?.EnableInClassList(OverlayHiddenClass, !_hubGameTabActive);
            _friendsHost?.EnableInClassList(OverlayHiddenClass, _hubGameTabActive);
            _gameTabButton?.EnableInClassList(HubTabActiveClass, _hubGameTabActive);
            _hubFriendsTabButton?.EnableInClassList(HubTabActiveClass, !_hubGameTabActive);
        }

        private void OnChatTabClicked() => ShowLeftMenuTab(LeftMenuTab.Chat);

        private void OnMatchHistoryTabClicked() => ShowLeftMenuTab(LeftMenuTab.MatchHistory);

        private void OnPublicGamesTabClicked() => ShowLeftMenuTab(LeftMenuTab.PublicGames);

        private void ShowLeftMenuTab(LeftMenuTab tab)
        {
            _leftMenuTab = tab;
            ApplyLeftMenuTabVisibility();
        }

        private void ApplyLeftMenuTabVisibility()
        {
            _chatTabContent?.EnableInClassList(OverlayHiddenClass, _leftMenuTab != LeftMenuTab.Chat);
            _matchHistoryTabContent?.EnableInClassList(OverlayHiddenClass, _leftMenuTab != LeftMenuTab.MatchHistory);
            _publicGamesTabContent?.EnableInClassList(OverlayHiddenClass, _leftMenuTab != LeftMenuTab.PublicGames);
            _settingsTabContent?.EnableInClassList(OverlayHiddenClass, _leftMenuTab != LeftMenuTab.Settings);
            _chatTabButton?.EnableInClassList(DockTabActiveClass, _leftMenuTab == LeftMenuTab.Chat);
            _matchHistoryTabButton?.EnableInClassList(DockTabActiveClass, _leftMenuTab == LeftMenuTab.MatchHistory);
            _publicGamesTabButton?.EnableInClassList(DockTabActiveClass, _leftMenuTab == LeftMenuTab.PublicGames);
            _settingsButton?.EnableInClassList(DockTabActiveClass, _leftMenuTab == LeftMenuTab.Settings);
        }

        private void BindMenuChat()
        {
            if (_chatMessages == null)
            {
                return;
            }

            _chatMessages.Clear();
            _chatMessages.Add(CreateMenuChatMessage("Система", "00:00", "Общий чат. Сообщения пока только локально."));
        }

        private static VisualElement CreateMenuChatMessage(string nick, string time, string text)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("mm-chat-message");

            var meta = new VisualElement { pickingMode = PickingMode.Ignore };
            meta.AddToClassList("mm-chat-message__meta");

            var nickLabel = new Label(nick) { pickingMode = PickingMode.Ignore };
            nickLabel.AddToClassList("mm-chat-message__nick");
            meta.Add(nickLabel);

            var timeLabel = new Label(time) { pickingMode = PickingMode.Ignore };
            timeLabel.AddToClassList("mm-chat-message__time");
            meta.Add(timeLabel);
            row.Add(meta);

            var body = new Label(text) { pickingMode = PickingMode.Ignore };
            body.AddToClassList("mm-chat-message__text");
            row.Add(body);
            return row;
        }

        private void OnChatSendClicked() => TrySendMenuChatMessage();

        private void OnChatInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                TrySendMenuChatMessage();
            }
        }

        private void TrySendMenuChatMessage()
        {
            var text = _chatInput?.value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text) || _chatMessages == null)
            {
                return;
            }

            var time = DateTime.Now.ToString("HH:mm");
            _chatMessages.Add(CreateMenuChatMessage("Вы", time, text));
            _chatInput.value = string.Empty;
            var scroll = _root?.Q<ScrollView>("ChatScroll");
            scroll?.schedule.Execute(() =>
            {
                if (scroll.verticalScroller.highValue > 0f)
                {
                    scroll.verticalScroller.value = scroll.verticalScroller.highValue;
                }
            });
        }

        private bool IsChatInputFocused()
        {
            if (_chatInput == null)
            {
                return false;
            }

            var focused = _chatInput.focusController?.focusedElement as VisualElement;
            return focused != null && (_chatInput == focused || _chatInput.Contains(focused));
        }

        private bool IsJoinCodeFocused()
        {
            if (!_isJoinUiOpen || _joinCodeField == null)
            {
                return false;
            }

            var focused = _joinCodeField.focusController?.focusedElement as VisualElement;
            return focused != null && (_joinCodeField == focused || _joinCodeField.Contains(focused));
        }

        private void OpenModeSelect()
        {
            if (_modeSelectOverlay == null)
            {
                return;
            }

            _isModeSelectOpen = true;
            ClearModeSelectError();
            _modeSelectOverlay.RemoveFromClassList(OverlayHiddenClass);
            SetMainMenuInteractable(false);
        }

        private void CloseModeSelect()
        {
            EnsureModeSelectClosed();
            OpenMatchEntry();
        }

        private void EnsureModeSelectClosed()
        {
            _isModeSelectOpen = false;
            _modeSelectOverlay?.AddToClassList(OverlayHiddenClass);
            ClearModeSelectError();
        }

        private void ShowLobbyEntry()
        {
            _lobbyEntryOverlay?.SetVisible(true);
        }

        private void EnsureLobbyEntryClosed()
        {
            _lobbyEntryOverlay?.SetVisible(false);
        }

        private void ClearModeSelectError()
        {
            SetOverlayLabel(_modeSelectErrorLabel, string.Empty);
        }

        private void ClearMatchEntryError()
        {
            SetOverlayLabel(_matchEntryErrorLabel, string.Empty);
        }

        private void ShowModeSelectError(string message)
        {
            SetOverlayLabel(_modeSelectErrorLabel, message);
        }

        private void ShowMatchEntryError(string message)
        {
            SetOverlayLabel(_matchEntryErrorLabel, message);
        }

        private void SetFriendsStatus(string message)
        {
            SetOverlayLabel(_friendsErrorLabel, message);
        }

        private static void SetOverlayLabel(Label label, string message)
        {
            if (label == null)
            {
                return;
            }

            var text = message ?? string.Empty;
            label.text = text;
            label.EnableInClassList(OverlayHiddenClass, string.IsNullOrEmpty(text));
        }

        private static string FormatMatchSetupError(System.Exception ex)
        {
            var raw = ex?.Message ?? string.Empty;
            if (raw.IndexOf("lobby", System.StringComparison.OrdinalIgnoreCase) >= 0
                || raw.IndexOf("relay", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Не удалось создать/войти в лобби (Unity Lobby/Relay). Проверь UGS project и сеть.";
            }

            if (raw.IndexOf("WSS", System.StringComparison.OrdinalIgnoreCase) >= 0
                || raw.IndexOf("игровым сервером", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MatchNetworkSession.TransportConnectFailedMessage;
            }

            if (raw.IndexOf("ensure failed", System.StringComparison.OrdinalIgnoreCase) >= 0
                || raw.IndexOf("Matchmaker", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Matchmaker недоступен. Проверь Worker и URL Mapping /api.";
            }

            return string.IsNullOrWhiteSpace(raw)
                ? "Не удалось создать матч."
                : $"Не удалось создать матч: {raw}";
        }

        private async UniTask CreateMatchAsync(int playerCount, System.Threading.CancellationToken cancellationToken)
        {
            if (_isTransitioning)
            {
                return;
            }

            _isTransitioning = true;
            ClearModeSelectError();
            ShowLobbyEntry();
            try
            {
                var displayName = string.IsNullOrWhiteSpace(PlayerProfileService.DisplayName)
                    ? "Host"
                    : PlayerProfileService.DisplayName;
                var handle = await MatchSessionService.Backend.CreateAsync(
                    new CreateMatchRequest(playerCount, displayName));
                PendingMatchReconnectStore.Clear();
                MatchNetworkSession.ApplyHandle(handle);
                if (!await MatchNetworkSession.TryStartTransportAsync())
                {
                    throw new System.InvalidOperationException(
                        MatchNetworkSession.TransportConnectFailedMessage);
                }

                SessionFlowTracker.NotifyChanged();

                EnsureModeSelectClosed();
                EnsureMatchEntryClosed();
                await LoadSceneWithFadeAsync(GameSceneNames.Lobby, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // Scene load destroys this controller — cancel is expected success.
            }
            catch (System.Exception ex)
            {
                MatchNetworkSession.Shutdown();
                Debug.LogWarning($"Create match failed: {ex.Message}");
                _isTransitioning = false;
                EnsureLobbyEntryClosed();
                ShowGameHubTab();
                ShowModeSelectError(FormatMatchSetupError(ex));
            }
        }

        private async UniTask JoinMatchAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (_isTransitioning)
            {
                return;
            }

            var code = _joinCodeField?.value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            await JoinMatchByCodeAsync(code, cancellationToken, showJoinUiOnError: true);
        }

        private async UniTask JoinMatchByCodeAsync(
            string code,
            System.Threading.CancellationToken cancellationToken,
            bool showJoinUiOnError)
        {
            _isTransitioning = true;
            ShowLobbyEntry();
            try
            {
                var displayName = string.IsNullOrWhiteSpace(PlayerProfileService.DisplayName)
                    ? "Guest"
                    : PlayerProfileService.DisplayName;
                var handle = await MatchSessionService.Backend.JoinAsync(
                    new JoinMatchRequest(code, displayName));
                if (!MatchNetworkSession.IsRejoiningMatch)
                {
                    PendingMatchReconnectStore.Clear();
                }
                MatchNetworkSession.ApplyHandle(handle);
                if (!await MatchNetworkSession.TryStartTransportAsync())
                {
                    throw new System.InvalidOperationException(
                        MatchNetworkSession.TransportConnectFailedMessage);
                }

                SessionFlowTracker.NotifyChanged();

                EnsureMatchEntryClosed();
                await LoadSceneWithFadeAsync(GameSceneNames.Lobby, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // Scene load destroys this controller — cancel is expected success.
            }
            catch (System.Exception ex)
            {
                MatchNetworkSession.Shutdown();
                Debug.LogWarning($"Join match failed: {ex.Message}");
                _isTransitioning = false;
                EnsureLobbyEntryClosed();
                if (showJoinUiOnError)
                {
                    ShowGameHubTab();
                    OpenJoinUi();
                    if (_joinCodeField != null)
                    {
                        _joinCodeField.value = code;
                    }
                }

                ShowMatchEntryError(FormatMatchSetupError(ex));
            }
        }

        private static void OnQuitRequested()
        {
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private async UniTask LoadSceneWithFadeAsync(string sceneName, System.Threading.CancellationToken cancellationToken)
        {
            _isTransitioning = true;
            SetMainMenuInteractable(false);

            if (_menuScreen != null)
            {
                await UiToolkitElementAnimator.FadeAsync(
                    _menuScreen,
                    1f,
                    0f,
                    FadeDuration,
                    cancellationToken: cancellationToken);
            }

            if (!SceneFlow.IsLoaded(sceneName))
            {
                await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: cancellationToken);
            }

            _isTransitioning = false;
        }

        private void SetMainMenuInteractable(bool interactable)
        {
            _overlayBlocksMenu = !interactable;
            RefreshMainMenuControlState();
        }

        private void RefreshMainMenuControlState()
        {
            var menuEnabled = !_overlayBlocksMenu;

            _playButton?.SetEnabled(menuEnabled);
            _joinMatchButton?.SetEnabled(menuEnabled);
            _joinConfirmButton?.SetEnabled(menuEnabled);
            _joinCodeField?.SetEnabled(menuEnabled);
            _gameTabButton?.SetEnabled(menuEnabled);
            _hubFriendsTabButton?.SetEnabled(menuEnabled);
            _modeGrid?.SetEnabled(menuEnabled);
            _returnToMatchButton?.SetEnabled(menuEnabled && PendingMatchReconnectStore.TryLoadActive(out _));
            _settingsButton?.SetEnabled(menuEnabled);
            _quitButton?.SetEnabled(menuEnabled);
            _chatTabButton?.SetEnabled(menuEnabled);
            _matchHistoryTabButton?.SetEnabled(menuEnabled);
            _publicGamesTabButton?.SetEnabled(menuEnabled);
            _chatSendButton?.SetEnabled(menuEnabled);
            _chatInput?.SetEnabled(menuEnabled);
            _editProfileButton?.SetEnabled(menuEnabled);
            _friendsHubPanel?.SetInteractable(menuEnabled);
        }
    }
}
