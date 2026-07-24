using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
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
        private Button _settingsButton;
        private Button _quitButton;
        private Button _settingsCloseButton;
        private Toggle _soundToggle;
        private Slider _volumeSlider;
        private Label _volumeValueLabel;
        private VisualElement _matchEntryOverlay;
        private VisualElement _modeSelectOverlay;
        private VisualElement _joinCodeRow;
        private VisualElement _modeGrid;
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
        private bool _isTransitioning;
        private bool _isSettingsOpen;
        private bool _isSettingsAnimating;
        private bool _isMatchEntryOpen;
        private bool _isModeSelectOpen;
        private bool _isProfileEditOpen;
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
            _modeSelectOverlay = _root.Q<VisualElement>("ModeSelectOverlay");
            _joinCodeRow = _root.Q<VisualElement>("JoinCodeRow");
            _modeGrid = _root.Q<VisualElement>("ModeGrid");
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
            _settingsButton = _root.Q<Button>("SettingsButton");
            _quitButton = _root.Q<Button>("QuitButton");
            _settingsCloseButton = _root.Q<Button>("SettingsCloseButton");
            _soundToggle = _root.Q<Toggle>("SoundToggle");
            _volumeSlider = _root.Q<Slider>("VolumeSlider");
            _volumeValueLabel = _root.Q<Label>("VolumeValueLabel");

            GameAudio.Apply();
            BindSettingsUi();
            BindMatchEntryUi();
            BindHubUi();
            BuildModeGrid();
            EnsureSettingsClosed();
            EnsureMatchEntryClosed();
            EnsureModeSelectClosed();
            EnsureProfileEditClosed();
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

            if (_settingsCloseButton != null)
            {
                _settingsCloseButton.clicked += OnSettingsClose;
            }

            _root?.RegisterCallback<KeyDownEvent>(OnKeyDown);
            FriendsHubService.LobbyInviteReceived += OnLobbyInviteReceived;
            UnityServicesBootstrap.PlayerNameChanged += OnPlayerNameChanged;
            _profileBadge?.RegisterCallback<ClickEvent>(OnProfileBadgeClicked);
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            PlayIntroAsync(cancellationToken).Forget();
            RestoreIntroIfStalledAsync(cancellationToken).Forget();
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            FriendsHubService.LobbyInviteReceived -= OnLobbyInviteReceived;
            UnityServicesBootstrap.PlayerNameChanged -= OnPlayerNameChanged;
            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsOpen;
            }

            if (_settingsCloseButton != null)
            {
                _settingsCloseButton.clicked -= OnSettingsClose;
            }

            _profileBadge?.UnregisterCallback<ClickEvent>(OnProfileBadgeClicked);
        }

        private void OnDestroy()
        {
            _bindingScope?.Dispose();
            _friendsHubPanel?.Dispose();
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
                case KeyCode.Space:
                    evt.StopPropagation();
                    OnPlayRequested();
                    break;
            }
        }

        private void OnSettingsOpen()
        {
            if (_isTransitioning || _isSettingsOpen || _isProfileEditOpen || _settingsOverlay == null)
            {
                return;
            }

            ShowSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
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
            if (_isTransitioning || _isSettingsOpen || _isSettingsAnimating || _isMatchEntryOpen || _isProfileEditOpen)
            {
                return;
            }

            OpenMatchEntry();
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

            var ink = new Color(15f / 255f, 15f / 255f, 16f / 255f, 1f);
            var cream = new Color(239f / 255f, 228f / 255f, 207f / 255f, 1f);
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
                _friendsErrorLabel.text =
                    "Друзья недоступны: нет связи с Unity Auth. Суффикс #xxxx появится после входа.";
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
                    if (_friendsErrorLabel != null)
                    {
                        _friendsErrorLabel.text =
                            "Имя для друзей ещё не готово. Сохраните профиль или проверьте UGS/сеть.";
                    }

                    return;
                }

                GUIUtility.systemCopyBuffer = fullName;
                if (_friendsErrorLabel != null)
                {
                    _friendsErrorLabel.text = "Имя скопировано.";
                }

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

            OpenMatchEntry();
            _joinCodeRow?.RemoveFromClassList(OverlayHiddenClass);
            if (_joinCodeField != null)
            {
                _joinCodeField.value = lobbyCode;
            }

            JoinMatchAsync(this.GetCancellationTokenOnDestroy()).Forget();
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
            _modeGrid.style.flexDirection = FlexDirection.Column;
            _modeGrid.style.justifyContent = Justify.Center;
            _modeGrid.style.alignItems = Align.Center;

            // Two centered rows (4 + 3) so tiles stay evenly aligned.
            var rowTop = CreateModeRow();
            var rowBottom = CreateModeRow();
            for (var n = MatchModeRules.MinPlayers; n <= MatchModeRules.MaxPlayers; n++)
            {
                var playerCount = n;
                var button = ModeMapThumbnailBuilder.BuildModeButton(playerCount);
                if (MatchModeRules.IsModeSelectable(playerCount))
                {
                    button.clicked += () =>
                        CreateMatchAsync(playerCount, this.GetCancellationTokenOnDestroy()).Forget();
                }

                if (playerCount <= 5)
                {
                    rowTop.Add(button);
                }
                else
                {
                    rowBottom.Add(button);
                }
            }

            _modeGrid.Add(rowTop);
            _modeGrid.Add(rowBottom);
        }

        private static VisualElement CreateModeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("mm-modes__row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
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
            _joinCodeRow?.AddToClassList(OverlayHiddenClass);
            ClearMatchEntryError();
        }

        private void OnCreateMatchClicked()
        {
            EnsureMatchEntryClosed();
            OpenModeSelect();
        }

        private void OnJoinMatchClicked()
        {
            if (_joinCodeRow == null)
            {
                return;
            }

            _joinCodeRow.RemoveFromClassList(OverlayHiddenClass);
            _joinCodeField?.Focus();
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

        private void ClearModeSelectError()
        {
            if (_modeSelectErrorLabel != null)
            {
                _modeSelectErrorLabel.text = string.Empty;
            }
        }

        private void ClearMatchEntryError()
        {
            if (_matchEntryErrorLabel != null)
            {
                _matchEntryErrorLabel.text = string.Empty;
            }
        }

        private void ShowModeSelectError(string message)
        {
            if (_modeSelectErrorLabel != null)
            {
                _modeSelectErrorLabel.text = message ?? string.Empty;
            }
        }

        private void ShowMatchEntryError(string message)
        {
            if (_matchEntryErrorLabel != null)
            {
                _matchEntryErrorLabel.text = message ?? string.Empty;
            }
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
            try
            {
                var displayName = string.IsNullOrWhiteSpace(PlayerProfileService.DisplayName)
                    ? "Host"
                    : PlayerProfileService.DisplayName;
                var handle = await MatchSessionService.Backend.CreateAsync(
                    new CreateMatchRequest(playerCount, displayName));
                MatchNetworkSession.ApplyHandle(handle);
                if (!await MatchNetworkSession.TryStartTransportAsync())
                {
                    throw new System.InvalidOperationException(
                        MatchNetworkSession.TransportConnectFailedMessage);
                }

                try
                {
                    await FriendsHubService.SetPresenceAsync(
                        FriendsHubRules.StatusInGame,
                        handle.RoomCode,
                        occupiedSlots: 1,
                        maxSlots: playerCount);
                }
                catch (System.Exception)
                {
                    // Presence optional for LocalDev.
                }

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
                Debug.LogWarning($"Create match failed: {ex.Message}");
                _isTransitioning = false;
                OpenModeSelect();
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

            _isTransitioning = true;
            try
            {
                var displayName = string.IsNullOrWhiteSpace(PlayerProfileService.DisplayName)
                    ? "Guest"
                    : PlayerProfileService.DisplayName;
                var handle = await MatchSessionService.Backend.JoinAsync(
                    new JoinMatchRequest(code, displayName));
                MatchNetworkSession.ApplyHandle(handle);
                if (!await MatchNetworkSession.TryStartTransportAsync())
                {
                    throw new System.InvalidOperationException(
                        MatchNetworkSession.TransportConnectFailedMessage);
                }

                try
                {
                    await FriendsHubService.SetPresenceAsync(
                        FriendsHubRules.StatusInGame,
                        handle.RoomCode,
                        occupiedSlots: 1,
                        maxSlots: handle.PlayerCount);
                }
                catch (System.Exception)
                {
                    // Presence optional for LocalDev.
                }

                EnsureMatchEntryClosed();
                await LoadSceneWithFadeAsync(GameSceneNames.Lobby, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // Scene load destroys this controller — cancel is expected success.
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Join match failed: {ex.Message}");
                _isTransitioning = false;
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
            _settingsButton?.SetEnabled(menuEnabled);
            _quitButton?.SetEnabled(menuEnabled);
            _editProfileButton?.SetEnabled(menuEnabled);
            _friendsHubPanel?.SetInteractable(menuEnabled);
        }
    }
}
