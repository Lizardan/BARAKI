using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
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
        private bool _isTransitioning;
        private bool _isSettingsOpen;
        private bool _isSettingsAnimating;
        private bool _isMatchEntryOpen;
        private bool _isModeSelectOpen;

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
            BuildModeGrid();
            EnsureSettingsClosed();
            EnsureMatchEntryClosed();
            EnsureModeSelectClosed();
            EnsureVisibleRestState();

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
                if (_isSettingsOpen)
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

            if (_isSettingsOpen || _isMatchEntryOpen || _isModeSelectOpen)
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
            if (_isTransitioning || _isSettingsOpen || _settingsOverlay == null)
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
            if (_isTransitioning || _isSettingsOpen || _isSettingsAnimating || _isMatchEntryOpen)
            {
                return;
            }

            OpenMatchEntry();
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
            for (var n = MatchModeRules.MinPlayers; n <= MatchModeRules.MaxPlayers; n++)
            {
                var playerCount = n;
                var button = ModeMapThumbnailBuilder.BuildModeButton(playerCount);
                if (MatchModeRules.IsModeSelectable(playerCount))
                {
                    button.clicked += () =>
                        CreateMatchAsync(playerCount, this.GetCancellationTokenOnDestroy()).Forget();
                }

                _modeGrid.Add(button);
            }
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
            if (raw.IndexOf("tunnel_not_registered", System.StringComparison.OrdinalIgnoreCase) >= 0
                || raw.IndexOf("503", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Нет игрового сервера. Запусти Start-Playtest.bat на ПК хоста.";
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
                var instanceId = DiscordActivityBridge.TryGetSession(out var discordSession)
                    ? discordSession.InstanceId
                    : null;
                var displayName = DiscordActivityBridge.TryGetSession(out var named)
                    ? (string.IsNullOrEmpty(named.DisplayName) ? "Host" : named.DisplayName)
                    : "Host";
                var handle = await MatchSessionService.Backend.CreateAsync(
                    new CreateMatchRequest(playerCount, displayName, instanceId));
                MatchNetworkSession.ApplyHandle(handle);
                if (!await MatchNetworkSession.TryStartTransportAsync())
                {
                    throw new System.InvalidOperationException(
                        MatchNetworkSession.TransportConnectFailedMessage);
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
            if (string.IsNullOrEmpty(code)
                && DiscordActivityBridge.TryGetSession(out var discordJoin)
                && !string.IsNullOrEmpty(discordJoin.InstanceId))
            {
                code = discordJoin.InstanceId;
            }

            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            _isTransitioning = true;
            try
            {
                var displayName = DiscordActivityBridge.TryGetSession(out var named)
                    ? (string.IsNullOrEmpty(named.DisplayName) ? "Guest" : named.DisplayName)
                    : "Guest";
                var handle = await MatchSessionService.Backend.JoinAsync(
                    new JoinMatchRequest(code, displayName));
                MatchNetworkSession.ApplyHandle(handle);
                if (!await MatchNetworkSession.TryStartTransportAsync())
                {
                    throw new System.InvalidOperationException(
                        MatchNetworkSession.TransportConnectFailedMessage);
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
            _playButton?.SetEnabled(interactable);
            _settingsButton?.SetEnabled(interactable);
            _quitButton?.SetEnabled(interactable);
        }
    }
}
