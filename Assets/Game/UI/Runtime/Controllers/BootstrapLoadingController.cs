using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace Game.UI.Controllers
{
    public enum BootstrapPreviewMode
    {
        Loading = 0,
        UpdateAvailable = 1,
        Downloading = 2,
    }

    /// <summary>
    /// Bootstrap loading screen: version check, force-update UI, UGS/profile/friends warm-up, then MainMenu.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BootstrapLoadingController : MonoBehaviour
    {
        private const string OverlayHiddenClass = "ui-overlay--hidden";

        [SerializeField] private UIDocument _uiDocument;

#if UNITY_EDITOR
        [Header("Editor preview")]
        [SerializeField] private BootstrapPreviewMode _previewMode = BootstrapPreviewMode.Loading;
        [SerializeField] private string _previewStatusText = "Проверка версии";
        [SerializeField] private string _previewLocalVersion = "0.1.2";
        [SerializeField] private string _previewRemoteVersion = "0.1.4";
        [SerializeField] [Range(0f, 1f)] private float _previewDownloadProgress = 0.37f;
#endif

        private VisualElement _root;
        private VisualElement _loadingPanel;
        private VisualElement _updatePanel;
        private Label _statusLabel;
        private Label _updateRangeLabel;
        private Label _updateStatusLabel;
        private Button _updateButton;
        private VisualElement _versionProgress;
        private VisualElement _versionProgressFill;
        private Label _versionProgressLabel;
        private bool _isUpdating;
        private bool _pipelineStarted;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            BindUi();
            ShowLoading("Проверка версии");
        }

        private void OnEnable()
        {
            if (_updateButton != null)
            {
                _updateButton.clicked += OnUpdateClicked;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
                return;
            }
#endif

            if (!_pipelineStarted)
            {
                _pipelineStarted = true;
                RunBootstrapPipelineAsync().Forget();
            }
        }

        private void OnDisable()
        {
            if (_updateButton != null)
            {
                _updateButton.clicked -= OnUpdateClicked;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
            }
        }

        /// <summary>Inspector / edit-mode preview for loading and update layouts.</summary>
        public void ApplyEditorPreview()
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
            BindUi();

            switch (_previewMode)
            {
                case BootstrapPreviewMode.UpdateAvailable:
                    ShowUpdateAvailable(_previewLocalVersion, _previewRemoteVersion);
                    break;
                case BootstrapPreviewMode.Downloading:
                    ShowDownloading(_previewLocalVersion, _previewRemoteVersion, _previewDownloadProgress);
                    break;
                default:
                    ShowLoading(string.IsNullOrWhiteSpace(_previewStatusText)
                        ? "Проверка версии"
                        : _previewStatusText);
                    break;
            }
        }
#endif

        private void BindUi()
        {
            _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            if (_root == null)
            {
                return;
            }

            _loadingPanel = _root.Q<VisualElement>("LoadingPanel");
            _updatePanel = _root.Q<VisualElement>("UpdatePanel");
            _statusLabel = _root.Q<Label>("StatusLabel");
            _updateRangeLabel = _root.Q<Label>("UpdateRangeLabel");
            _updateStatusLabel = _root.Q<Label>("UpdateStatusLabel");
            _updateButton = _root.Q<Button>("UpdateButton");
            _versionProgress = _root.Q<VisualElement>("VersionProgress");
            _versionProgressFill = _root.Q<VisualElement>("VersionProgressFill");
            _versionProgressLabel = _root.Q<Label>("VersionProgressLabel");
        }

        private async UniTaskVoid RunBootstrapPipelineAsync()
        {
            PlayerProfileService.PrimeFromLocalPrefs();
            UnityServicesBootstrap.PrimeCachedPlayerNameFromPrefs();

            ShowLoading("Проверка версии");
            await GameUpdateService.RefreshAsync();

            if (this == null)
            {
                return;
            }

            if (GameUpdateService.UpdateRequired)
            {
                var remote = GameUpdateService.RemoteManifest?.version;
                ShowUpdateAvailable(GameLocalVersion.Current, remote);
                return;
            }

            if (GameUpdateService.CheckFailed && _statusLabel != null)
            {
                var detail = string.IsNullOrWhiteSpace(GameUpdateService.LastError)
                    ? "Попробуйте позже."
                    : GameUpdateService.LastError;
                _statusLabel.text = $"Не удалось проверить обновления: {detail}";
                await UniTask.Delay(TimeSpan.FromSeconds(1.2f), ignoreTimeScale: true);
            }

            await WarmSocialServicesAsync();
            if (this == null)
            {
                return;
            }

            ShowLoading("Открытие меню");
            await SceneManager.LoadSceneAsync(GameSceneNames.MainMenu);
        }

        private async UniTask WarmSocialServicesAsync()
        {
            try
            {
                ShowLoading("Авторизация");
                await UnityServicesBootstrap.EnsureInitializedAsync();

                ShowLoading("Профиль");
                try
                {
                    await PlayerProfileService.LoadAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Bootstrap profile load skipped: {ex.Message}");
                }

                ShowLoading("Друзья");
                if (UnityServicesBootstrap.IsReady)
                {
                    try
                    {
                        await FriendsHubService.InitializeAsync();
                    }
                    catch (Exception friendsEx)
                    {
                        Debug.LogWarning($"Bootstrap friends init skipped: {friendsEx.Message}");
                    }
                }
            }
            catch (Exception socialEx)
            {
                Debug.LogWarning($"Bootstrap social warm-up skipped: {socialEx.Message}");
            }
        }

        private void OnUpdateClicked()
        {
            ApplyUpdateAsync().Forget();
        }

        private async UniTaskVoid ApplyUpdateAsync()
        {
            if (_isUpdating || !GameUpdateService.UpdateRequired)
            {
                return;
            }

            _isUpdating = true;
            var remote = GameUpdateService.RemoteManifest?.version;
            ShowDownloading(GameLocalVersion.Current, remote, 0f);
            if (_updateStatusLabel != null)
            {
                _updateStatusLabel.text = string.Empty;
            }

            try
            {
                var progress = new Progress<float>(p =>
                {
                    ShowDownloading(GameLocalVersion.Current, remote, p);
                });
                await GameUpdateService.DownloadAndApplyAsync(progress);
            }
            catch (Exception ex)
            {
                _isUpdating = false;
                ShowUpdateAvailable(GameLocalVersion.Current, remote);
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.text = "Ошибка обновления: " + ex.Message;
                }
            }
        }

        private void ShowLoading(string status)
        {
            SetOverlayHidden(_loadingPanel, false);
            SetOverlayHidden(_updatePanel, true);
            if (_statusLabel != null)
            {
                _statusLabel.text = status ?? string.Empty;
            }
        }

        private void ShowUpdateAvailable(string localVersion, string remoteVersion)
        {
            SetOverlayHidden(_loadingPanel, true);
            SetOverlayHidden(_updatePanel, false);
            SetOverlayHidden(_versionProgress, true);
            if (_updateButton != null)
            {
                _updateButton.SetEnabled(true);
                SetOverlayHidden(_updateButton, false);
            }

            if (_updateRangeLabel != null)
            {
                _updateRangeLabel.text = GameUpdateUiRules.FormatUpdateRange(localVersion, remoteVersion);
            }

            if (_updateStatusLabel != null && string.IsNullOrEmpty(_updateStatusLabel.text))
            {
                _updateStatusLabel.text = string.Empty;
            }
        }

        private void ShowDownloading(string localVersion, string remoteVersion, float progress01)
        {
            SetOverlayHidden(_loadingPanel, true);
            SetOverlayHidden(_updatePanel, false);
            SetOverlayHidden(_updateButton, true);
            SetOverlayHidden(_versionProgress, false);

            if (_updateRangeLabel != null)
            {
                _updateRangeLabel.text = GameUpdateUiRules.FormatUpdateRange(localVersion, remoteVersion);
            }

            if (_versionProgressFill != null)
            {
                _versionProgressFill.style.width =
                    Length.Percent(GameUpdateUiRules.ProgressPercent(progress01));
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
    }
}
