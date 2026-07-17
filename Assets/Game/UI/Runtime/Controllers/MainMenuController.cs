using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using Game.UI;
using Game.UI.Animations;
using Game.UI.Bindings;
using Game.UI.ViewModels;
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
        [Header("Editor preview — version strip (Play Mode)")]
        [SerializeField] private bool _previewUpdateAvailable = true;
        [SerializeField] private string _previewRemoteVersion = "0.1.4";
        [SerializeField] private bool _previewDownloading;
        [SerializeField] [Range(0f, 1f)] private float _previewDownloadProgress = 0.37f;

        [Header("Editor preview — hub loading")]
        [SerializeField] private bool _previewProfileLoading;
        [SerializeField] private bool _previewFriendsLoading;
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
        private Label _friendsListLabel;
        private Label _friendsCountLabel;
        private Label _updateStatusLabel;
        private Label _versionLabel;
        private Label _versionArrowLabel;
        private Label _versionRemoteLabel;
        private Button _versionUpdateButton;
        private VisualElement _versionProgress;
        private VisualElement _versionProgressFill;
        private Label _versionProgressLabel;
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
        private PanelLoadingOverlay _profileLoadingOverlay;
        private PanelLoadingOverlay _friendsLoadingOverlay;
        private bool _hubDataLoading;
        private bool _isTransitioning;
        private bool _isSettingsOpen;
        private bool _isSettingsAnimating;
        private bool _isMatchEntryOpen;
        private bool _isModeSelectOpen;
        private bool _isProfileEditOpen;
        private bool _playBlockedByUpdate;
        private bool _isUpdating;
        private int _pendingAvatarId;
        private bool _overlayBlocksMenu;

        private bool IsMainMenuBlockedByUpdate =>
#if UNITY_EDITOR
            _playBlockedByUpdate || _isUpdating ||
            (Application.isPlaying && (_previewUpdateAvailable || _previewDownloading));
#else
            _playBlockedByUpdate || _isUpdating;
#endif

        private void Awake()
        {
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
            var profileBadge = _root.Q<VisualElement>("ProfileBadge");
            if (profileBadge != null)
            {
                // Keep avatar insets equal: 12px padding + 96px avatar.
                profileBadge.style.height = 120;
                profileBadge.style.minHeight = 120;
                profileBadge.style.maxHeight = 120;
            }

            _friendsListLabel = _root.Q<Label>("FriendsListLabel");
            _friendsCountLabel = _root.Q<Label>("FriendsCountLabel");
            _updateStatusLabel = _root.Q<Label>("UpdateStatusLabel");
            _versionLabel = _root.Q<Label>("VersionLabel");
            _versionArrowLabel = _root.Q<Label>("VersionArrowLabel");
            _versionRemoteLabel = _root.Q<Label>("VersionRemoteLabel");
            _versionUpdateButton = _root.Q<Button>("VersionUpdateButton");
            _versionProgress = _root.Q<VisualElement>("VersionProgress");
            _versionProgressFill = _root.Q<VisualElement>("VersionProgressFill");
            _versionProgressLabel = _root.Q<Label>("VersionProgressLabel");
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
            BindLoadingOverlays();

            ApplyVersionStrip(updateAvailable: false, remoteVersion: null, downloading: false, progress01: 0f);

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
            RefreshHubAsync().Forget();

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

            if (Application.isPlaying)
            {
                ApplyEditorVersionStripPreview();
            }

            ApplyEditorHubLoadingPreview();
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
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            PlayIntroAsync(cancellationToken).Forget();
            RestoreIntroIfStalledAsync(cancellationToken).Forget();
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsOpen;
            }

            if (_settingsCloseButton != null)
            {
                _settingsCloseButton.clicked -= OnSettingsClose;
            }
        }

        private void OnDestroy()
        {
            _bindingScope?.Dispose();
            _profileLoadingOverlay?.Dispose();
            _friendsLoadingOverlay?.Dispose();
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
            if (_isTransitioning || _isSettingsAnimating || IsMainMenuBlockedByUpdate)
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
            if (_isTransitioning || _isSettingsOpen || _isProfileEditOpen || _settingsOverlay == null
                || IsMainMenuBlockedByUpdate)
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

            if (_playBlockedByUpdate)
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.text = "Сначала обновите игру до последней версии.";
                }

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

            if (_addFriendButton != null)
            {
                _addFriendButton.clicked += () => AddFriendAsync().Forget();
            }

            if (_versionUpdateButton != null)
            {
                _versionUpdateButton.clicked += () => ApplyUpdateAsync().Forget();
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
            if (_profileEditOverlay == null || _isProfileEditOpen || _isTransitioning
                || IsMainMenuBlockedByUpdate)
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

            var ink = new Color(18f / 255f, 14f / 255f, 12f / 255f, 1f);
            var cream = new Color(242f / 255f, 230f / 255f, 200f / 255f, 1f);
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

        private async UniTaskVoid RefreshHubAsync()
        {
            SetProfileLoadingVisible(true);
            SetFriendsLoadingVisible(true);
            _hubDataLoading = true;
            try
            {
                await GameUpdateService.RefreshAsync();
                _playBlockedByUpdate = GameUpdateService.UpdateRequired;
                RefreshMainMenuControlState();

                ApplyVersionStrip(
                    updateAvailable: _playBlockedByUpdate,
                    remoteVersion: GameUpdateService.RemoteManifest?.version,
                    downloading: false,
                    progress01: 0f);
#if UNITY_EDITOR
                ApplyEditorVersionStripPreview();
#endif

                if (_updateStatusLabel != null)
                {
                    if (GameUpdateService.CheckFailed)
                    {
                        var detail = string.IsNullOrWhiteSpace(GameUpdateService.LastError)
                            ? "Попробуйте позже."
                            : GameUpdateService.LastError;
                        _updateStatusLabel.text = $"Не удалось проверить обновления: {detail}";
                    }
                    else
                    {
                        _updateStatusLabel.text = string.Empty;
                    }
                }

                // Hub social needs UGS; LocalDev Editor path skips gracefully.
                try
                {
                    try
                    {
                        await PlayerProfileService.LoadAsync();
                        RefreshProfileLabels();
                    }
                    finally
                    {
                        SetProfileLoadingVisible(false);
                    }

                    try
                    {
                        await FriendsHubService.InitializeAsync();
                        await FriendsHubService.SetPresenceAsync("InLauncher");
                        RefreshFriendsLabel();
                    }
                    finally
                    {
                        SetFriendsLoadingVisible(false);
                    }
                }
                catch (System.Exception socialEx)
                {
                    Debug.LogWarning($"Hub social init skipped: {socialEx.Message}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"RefreshHubAsync: {ex.Message}");
            }
            finally
            {
                _hubDataLoading = false;
                SetProfileLoadingVisible(false);
                SetFriendsLoadingVisible(false);
            }
        }

        private void BindLoadingOverlays()
        {
            _profileLoadingOverlay?.Dispose();
            _friendsLoadingOverlay?.Dispose();
            _profileLoadingOverlay = new PanelLoadingOverlay(_root.Q<VisualElement>("ProfileLoadingOverlay"));
            _friendsLoadingOverlay = new PanelLoadingOverlay(_root.Q<VisualElement>("HubLoadingOverlay"));
        }

        private void SetProfileLoadingVisible(bool visible)
        {
#if UNITY_EDITOR
            if (_previewProfileLoading)
            {
                visible = true;
            }
#endif
            _profileLoadingOverlay?.SetVisible(visible);
            RefreshMainMenuControlState();
        }

        private void SetFriendsLoadingVisible(bool visible)
        {
#if UNITY_EDITOR
            if (_previewFriendsLoading)
            {
                visible = true;
            }
#endif
            _friendsLoadingOverlay?.SetVisible(visible);
            RefreshMainMenuControlState();
        }

#if UNITY_EDITOR
        /// <summary>Inspector / edit-mode preview for hub loading overlays.</summary>
        public void ApplyEditorHubLoadingPreview()
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

            if (_profileLoadingOverlay == null || _friendsLoadingOverlay == null)
            {
                _root = root;
                BindLoadingOverlays();
            }

            _profileLoadingOverlay?.SetVisible(_previewProfileLoading);
            _friendsLoadingOverlay?.SetVisible(_previewFriendsLoading);
        }
#endif

        private void RefreshProfileLabels()
        {
            var displayName = string.IsNullOrWhiteSpace(PlayerProfileService.DisplayName)
                ? "Игрок"
                : PlayerProfileService.DisplayName;
            var avatarId = PlayerProfileService.AvatarId;

            if (_profileNameLabel != null)
            {
                _profileNameLabel.text = displayName;
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

        private void RefreshFriendsLabel()
        {
            var friends = FriendsHubService.GetFriendsSnapshot();
            if (_friendsCountLabel != null)
            {
                _friendsCountLabel.text = friends.Count.ToString();
            }

            if (_friendsListLabel == null)
            {
                return;
            }

            if (friends.Count == 0)
            {
                _friendsListLabel.text = "Нет друзей. Добавьте по Player ID.";
                return;
            }

            var lines = new List<string>(friends.Count);
            foreach (var friend in friends)
            {
                lines.Add($"{friend.Name}: {friend.Status}");
            }

            _friendsListLabel.text = string.Join("\n", lines);
        }

        private async UniTaskVoid SaveProfileAsync()
        {
            try
            {
                var name = _displayNameField?.value ?? "Player";
                await PlayerProfileService.SaveProfileAsync(name, _pendingAvatarId);
                RefreshProfileLabels();
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

        private async UniTaskVoid AddFriendAsync()
        {
            try
            {
                var id = _friendIdField?.value?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    return;
                }

                await FriendsHubService.SendFriendRequestByIdAsync(id);
                RefreshFriendsLabel();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AddFriendAsync: {ex.Message}");
            }
        }

        private async UniTaskVoid ApplyUpdateAsync()
        {
            if (_isUpdating || !_playBlockedByUpdate)
            {
                return;
            }

            _isUpdating = true;
            RefreshMainMenuControlState();
            try
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.text = string.Empty;
                }

                ApplyVersionStrip(
                    updateAvailable: true,
                    remoteVersion: GameUpdateService.RemoteManifest?.version,
                    downloading: true,
                    progress01: 0f);

                var progress = new Progress<float>(p =>
                {
                    ApplyVersionStrip(
                        updateAvailable: true,
                        remoteVersion: GameUpdateService.RemoteManifest?.version,
                        downloading: true,
                        progress01: p);
                });

                await GameUpdateService.DownloadAndApplyAsync(progress);
            }
            catch (System.Exception ex)
            {
                _isUpdating = false;
                RefreshMainMenuControlState();
                ApplyVersionStrip(
                    updateAvailable: _playBlockedByUpdate,
                    remoteVersion: GameUpdateService.RemoteManifest?.version,
                    downloading: false,
                    progress01: 0f);
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.text = "Ошибка обновления: " + ex.Message;
                }
            }
        }

#if UNITY_EDITOR
        private void ApplyEditorVersionStripPreview()
        {
            if (_previewUpdateAvailable || _previewDownloading)
            {
                ApplyVersionStrip(
                    updateAvailable: _previewUpdateAvailable || _previewDownloading,
                    remoteVersion: _previewRemoteVersion,
                    downloading: _previewDownloading,
                    progress01: _previewDownloadProgress);
            }

            RefreshMainMenuControlState();
        }
#endif

        private void ApplyVersionStrip(
            bool updateAvailable,
            string remoteVersion,
            bool downloading,
            float progress01)
        {
            if (_versionLabel != null)
            {
                _versionLabel.text = GameUpdateUiRules.FormatVersionLabel(Application.version);
            }

            var showRemote = updateAvailable && !string.IsNullOrWhiteSpace(remoteVersion);
            SetOverlayHidden(_versionArrowLabel, !showRemote);
            SetOverlayHidden(_versionRemoteLabel, !showRemote);
            if (_versionRemoteLabel != null && showRemote)
            {
                _versionRemoteLabel.text = GameUpdateUiRules.FormatVersionLabel(remoteVersion);
            }

            SetOverlayHidden(_versionUpdateButton, !updateAvailable || downloading);
            SetOverlayHidden(_versionProgress, !downloading);

            if (_versionProgressFill != null)
            {
                _versionProgressFill.style.width = Length.Percent(GameUpdateUiRules.ProgressPercent(progress01));
            }

            if (_versionProgressLabel != null)
            {
                _versionProgressLabel.text = GameUpdateUiRules.FormatProgressLabel(progress01);
            }
        }

        private static void SetOverlayHidden(VisualElement element, bool hidden)
        {
            if (element == null)
            {
                return;
            }

            if (hidden)
            {
                element.AddToClassList(OverlayHiddenClass);
            }
            else
            {
                element.RemoveFromClassList(OverlayHiddenClass);
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
                    await FriendsHubService.SetPresenceAsync("InGame", handle.RoomCode);
                }
                catch (System.Exception)
                {
                    // Presence optional for LocalDev.
                }

                EnsureModeSelectClosed();
                EnsureMatchEntryClosed();
                await LoadSceneWithFadeAsync(GameSceneNames.Lobby, cancellationToken);
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
                    await FriendsHubService.SetPresenceAsync("InGame", handle.RoomCode);
                }
                catch (System.Exception)
                {
                    // Presence optional for LocalDev.
                }

                EnsureMatchEntryClosed();
                await LoadSceneWithFadeAsync(GameSceneNames.Lobby, cancellationToken);
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
            var menuEnabled = !_overlayBlocksMenu && !IsMainMenuBlockedByUpdate;
            var profileLoading = _profileLoadingOverlay?.IsVisible == true;
            var friendsLoading = _friendsLoadingOverlay?.IsVisible == true;

            _playButton?.SetEnabled(menuEnabled);
            _settingsButton?.SetEnabled(menuEnabled);
            _quitButton?.SetEnabled(menuEnabled);
            _editProfileButton?.SetEnabled(menuEnabled && !profileLoading);
            _addFriendButton?.SetEnabled(menuEnabled && !friendsLoading);
            _friendIdField?.SetEnabled(menuEnabled && !friendsLoading);
            _versionUpdateButton?.SetEnabled(IsUpdateActionAvailable());
        }

        private bool IsUpdateActionAvailable()
        {
            if (_isUpdating)
            {
                return false;
            }

            if (_playBlockedByUpdate)
            {
                return true;
            }

#if UNITY_EDITOR
            return Application.isPlaying && _previewUpdateAvailable;
#else
            return false;
#endif
        }
    }
}
