using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
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
        private const float FadeDuration = 0.4f;
        private const float IntroDuration = 0.42f;
        private const float SettingsAnimDuration = 0.28f;

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        private MainMenuViewModel _viewModel;
        private UIBindingScope _bindingScope;
        private VisualElement _menuScreen;
        private VisualElement _menuBackdrop;
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
        private bool _isTransitioning;
        private bool _isSettingsOpen;
        private bool _isSettingsAnimating;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            _viewModel = new MainMenuViewModel();
            _root = _uiDocument.rootVisualElement;
            _menuScreen = _root.Q<VisualElement>("MenuScreen");
            _menuBackdrop = _root.Q<VisualElement>("MenuBackdrop");
            _menuBrand = _root.Q<VisualElement>("MenuBrand");
            _menuPanel = _root.Q<VisualElement>("MenuPanel");
            _settingsOverlay = _root.Q<VisualElement>("SettingsOverlay");
            _menuOverlayDim = _root.Q<VisualElement>("MenuOverlayDim");
            _menuDialog = _root.Q<VisualElement>("MenuDialog");

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

            _bindingScope = new UIBindingScope(_root);
            _bindingScope.Add(_viewModel.Title.SubscribeToText(titleLabel));
            _bindingScope.Add(_viewModel.PlayCommand.BindTo(_playButton));
            _bindingScope.Add(_viewModel.QuitCommand.BindTo(_quitButton));
            _bindingScope.Add(_viewModel.PlayCommand.Subscribe(_ => OnPlayRequested()));
            _bindingScope.Add(_viewModel.QuitCommand.Subscribe(_ => OnQuitRequested()));

            _settingsButton.clicked += OnSettingsOpen;
            _settingsCloseButton.clicked += OnSettingsClose;
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
                var tasks = new List<UniTask>
                {
                    UiToolkitElementAnimator.TranslateAsync(
                        _menuBrand,
                        new Vector2(-48f, 0f),
                        Vector2.zero,
                        IntroDuration,
                        cancellationToken: cancellationToken),
                    UiToolkitElementAnimator.TranslateAsync(
                        _menuPanel,
                        new Vector2(48f, 0f),
                        Vector2.zero,
                        IntroDuration,
                        0.08f,
                        cancellationToken),
                };

                await UniTask.WhenAll(tasks);

                var buttons = new List<VisualElement>();
                if (_playButton != null)
                {
                    buttons.Add(_playButton);
                }

                if (_settingsButton != null)
                {
                    buttons.Add(_settingsButton);
                }

                if (_quitButton != null)
                {
                    buttons.Add(_quitButton);
                }

                await UiToolkitElementAnimator.StaggerFadeScaleAsync(
                    buttons,
                    0f,
                    1f,
                    new Vector2(0.96f, 0.96f),
                    Vector2.one,
                    0.28f,
                    0.05f,
                    bounce: false,
                    cancellationToken: cancellationToken);

                _playButton?.Focus();
            }
            finally
            {
                EnsureIntroRestState();
            }
        }

        private async UniTask RestoreIntroIfStalledAsync(System.Threading.CancellationToken cancellationToken)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(IntroDuration + 0.75f),
                ignoreTimeScale: true,
                cancellationToken: cancellationToken);
            EnsureIntroRestState();
        }

        private void EnsureIntroRestState()
        {
            if (_menuBackdrop != null)
            {
                _menuBackdrop.style.opacity = 1f;
            }

            if (_menuBrand != null)
            {
                _menuBrand.style.opacity = 1f;
                _menuBrand.style.translate = new Translate(0f, 0f);
            }

            if (_menuPanel != null)
            {
                _menuPanel.style.opacity = 1f;
                _menuPanel.style.translate = new Translate(0f, 0f);
            }

            SetButtonsIntroState(1f, Vector2.one);
        }

        private void PrepareIntroState()
        {
            if (_menuBrand != null)
            {
                _menuBrand.style.translate = new Translate(-48f, 0f);
            }

            if (_menuPanel != null)
            {
                _menuPanel.style.translate = new Translate(48f, 0f);
            }

            SetButtonsIntroState(0f, new Vector2(0.96f, 0.96f));
        }

        private void SetButtonsIntroState(float opacity, Vector2 scale)
        {
            foreach (var button in new[] { _playButton, _settingsButton, _quitButton })
            {
                if (button == null)
                {
                    continue;
                }

                button.style.opacity = opacity;
                button.style.scale = new Scale(new Vector3(scale.x, scale.y, 1f));
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
                else
                {
                    OnQuitRequested();
                }

                return;
            }

            if (_isSettingsOpen)
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
            _settingsOverlay.RemoveFromClassList("menu__overlay--hidden");
            SetMainMenuInteractable(false);

            if (_menuOverlayDim != null)
            {
                _menuOverlayDim.style.opacity = 0f;
            }

            if (_menuDialog != null)
            {
                _menuDialog.style.opacity = 0f;
                _menuDialog.style.scale = new Scale(new Vector3(0.95f, 0.95f, 1f));
            }

            await UniTask.WhenAll(
                UiToolkitElementAnimator.FadeAsync(_menuOverlayDim, 0f, 1f, SettingsAnimDuration, cancellationToken: cancellationToken),
                UiToolkitElementAnimator.FadeScaleAsync(
                    _menuDialog,
                    0f,
                    1f,
                    new Vector2(0.95f, 0.95f),
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
                UiToolkitElementAnimator.FadeAsync(_menuOverlayDim, 1f, 0f, SettingsAnimDuration, cancellationToken: cancellationToken),
                UiToolkitElementAnimator.FadeScaleAsync(
                    _menuDialog,
                    1f,
                    0f,
                    Vector2.one,
                    new Vector2(0.95f, 0.95f),
                    SettingsAnimDuration,
                    cancellationToken: cancellationToken));

            _isSettingsOpen = false;
            _settingsOverlay.AddToClassList("menu__overlay--hidden");
            SetMainMenuInteractable(true);
            _isSettingsAnimating = false;
            _playButton?.Focus();
        }

        private void OnPlayRequested()
        {
            if (_isTransitioning || _isSettingsOpen || _isSettingsAnimating)
            {
                return;
            }

            LoadSceneWithFadeAsync(GameSceneNames.Lobby, this.GetCancellationTokenOnDestroy()).Forget();
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

            if (_menuPanel != null)
            {
                await UiToolkitElementAnimator.ScaleAsync(
                    _menuPanel,
                    Vector2.one,
                    new Vector2(0.96f, 0.96f),
                    FadeDuration * 0.5f,
                    cancellationToken: cancellationToken);
            }

            if (_menuScreen != null)
            {
                await UiToolkitElementAnimator.FadeAsync(
                    _menuScreen,
                    _menuScreen.resolvedStyle.opacity,
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
