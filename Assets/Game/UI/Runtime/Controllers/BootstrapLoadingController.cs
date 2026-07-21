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
        ReadyToEnter = 3,
    }

    /// <summary>
    /// Bootstrap loading screen: version check, force-update UI, UGS warm-up, then wait for Enter Game.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BootstrapLoadingController : MonoBehaviour
    {
        private const string OverlayHiddenClass = "ui-overlay--hidden";
        private const string CtaDownloadingClass = "bl__cta--downloading";
        private const string PrimaryButtonClass = "ui-btn--primary";
        private const string MutedButtonClass = "ui-btn--muted";
        private const float UgsInitTimeoutSeconds = 8f;
        private const float FriendsInitTimeoutSeconds = 8f;

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
        private Label _loadingTitleLabel;
        private Label _statusLabel;
        private Label _versionLabel;
        private Label _updateTitleLabel;
        private VisualElement _updateMeta;
        private VisualElement _updateRangeLabel;
        private Label _updateStatusLabel;
        private Label _sideStatusTitle;
        private Label _sideStatusLabel;
        private Label _newsFeaturedTag;
        private Label _newsFeaturedTitle;
        private Label _newsFeaturedBody;
        private VisualElement _newsListContainer;
        private Button _updateButton;
        private Button _enterGameButton;
        private Button _quitButton;
        private VisualElement _versionProgress;
        private VisualElement _versionProgressFill;
        private Label _versionProgressLabel;
        private string _appliedRangeLocal;
        private string _appliedRangeRemote;
        private bool _isUpdating;
        private bool _isEnteringGame;
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

            if (_enterGameButton != null)
            {
                _enterGameButton.clicked += OnEnterGameClicked;
            }

            if (_quitButton != null)
            {
                _quitButton.clicked += OnQuitClicked;
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

            if (_enterGameButton != null)
            {
                _enterGameButton.clicked -= OnEnterGameClicked;
            }

            if (_quitButton != null)
            {
                _quitButton.clicked -= OnQuitClicked;
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
                case BootstrapPreviewMode.ReadyToEnter:
                    ShowReadyToEnter();
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
            _loadingTitleLabel = _root.Q<Label>("LoadingTitleLabel");
            _statusLabel = _root.Q<Label>("StatusLabel");
            _versionLabel = _root.Q<Label>("VersionLabel");
            _updateTitleLabel = _root.Q<Label>("UpdateTitleLabel");
            _updateMeta = _root.Q<VisualElement>("UpdateMeta");
            _updateRangeLabel = _root.Q<VisualElement>("UpdateRangeLabel");
            _updateStatusLabel = _root.Q<Label>("UpdateStatusLabel");
            _sideStatusTitle = _root.Q<Label>("SideStatusTitle");
            _sideStatusLabel = _root.Q<Label>("SideStatusLabel");
            _newsFeaturedTag = _root.Q<Label>("NewsFeaturedTag");
            _newsFeaturedTitle = _root.Q<Label>("NewsFeaturedTitle");
            _newsFeaturedBody = _root.Q<Label>("NewsFeaturedBody");
            _newsListContainer = _root.Q<VisualElement>("NewsListContainer");
            _updateButton = _root.Q<Button>("UpdateButton");
            _enterGameButton = _root.Q<Button>("EnterGameButton");
            _quitButton = _root.Q<Button>("QuitButton");
            _versionProgress = _root.Q<VisualElement>("VersionProgress");
            _versionProgressFill = _root.Q<VisualElement>("VersionProgressFill");
            _versionProgressLabel = _root.Q<Label>("VersionProgressLabel");

            if (_versionLabel != null)
            {
                _versionLabel.text = GameUpdateUiRules.FormatVersionLabel(GameLocalVersion.Current);
            }

            BindNewsFeed(LauncherNewsRules.CreateDefaultFeed());
            SetButtonDownloadProgress(false);

            // Quit stays visible; interactivity is toggled by pipeline state.
            SetOverlayHidden(_quitButton, false);
            SetQuitInteractive(false);
        }

        /// <summary>Fills featured + secondary news cards. Safe to call again with a remote feed later.</summary>
        public void BindNewsFeed(LauncherNewsItem[] feed)
        {
            if (_root == null)
            {
                return;
            }

            _newsFeaturedTag ??= _root.Q<Label>("NewsFeaturedTag");
            _newsFeaturedTitle ??= _root.Q<Label>("NewsFeaturedTitle");
            _newsFeaturedBody ??= _root.Q<Label>("NewsFeaturedBody");
            _newsListContainer ??= _root.Q<VisualElement>("NewsListContainer");

            if (!LauncherNewsRules.IsValidFeed(feed))
            {
                feed = LauncherNewsRules.CreateDefaultFeed();
            }

            var featured = LauncherNewsRules.GetFeatured(feed);
            if (_newsFeaturedTag != null)
            {
                _newsFeaturedTag.text = featured.Tag;
            }

            if (_newsFeaturedTitle != null)
            {
                _newsFeaturedTitle.text = featured.Title;
            }

            if (_newsFeaturedBody != null)
            {
                _newsFeaturedBody.text = featured.Body;
            }

            if (_newsListContainer == null)
            {
                return;
            }

            _newsListContainer.Clear();
            var secondary = LauncherNewsRules.GetSecondaryItems(feed);
            for (var i = 0; i < secondary.Count; i++)
            {
                var isLast = i == secondary.Count - 1;
                _newsListContainer.Add(CreateNewsCard(secondary.Array[secondary.Offset + i], isLast));
            }
        }

        private static VisualElement CreateNewsCard(LauncherNewsItem item, bool isLast)
        {
            var card = new VisualElement();
            card.AddToClassList("bl__news-card");
            if (isLast)
            {
                card.AddToClassList("bl__news-card--last");
            }

            card.pickingMode = PickingMode.Ignore;

            var tag = new Label(item.Tag);
            tag.AddToClassList("bl__news-card__tag");
            tag.pickingMode = PickingMode.Ignore;

            var title = new Label(item.Title);
            title.AddToClassList("bl__news-card__title");
            title.pickingMode = PickingMode.Ignore;

            var body = new Label(item.Body);
            body.AddToClassList("bl__news-card__body");
            body.pickingMode = PickingMode.Ignore;

            card.Add(tag);
            card.Add(title);
            card.Add(body);
            return card;
        }

        private void SetClientStatus(string title, string status)
        {
            var safeTitle = title ?? string.Empty;
            var safeStatus = status ?? string.Empty;

            if (_sideStatusTitle != null)
            {
                _sideStatusTitle.text = safeTitle;
            }

            if (_sideStatusLabel != null)
            {
                _sideStatusLabel.text = safeStatus;
            }

            // Hidden anchors for tests / legacy bindings.
            if (_loadingTitleLabel != null)
            {
                _loadingTitleLabel.text = safeTitle;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = safeStatus;
            }
        }

        private void SetUpdateMetaVisible(bool visible)
        {
            SetOverlayHidden(_updateMeta, !visible);

            if (!visible)
            {
                _appliedRangeLocal = null;
                _appliedRangeRemote = null;
            }
        }

        private void ApplyUpdateRangeIfNeeded(string localVersion, string remoteVersion)
        {
            if (_appliedRangeLocal == localVersion && _appliedRangeRemote == remoteVersion)
            {
                return;
            }

            _appliedRangeLocal = localVersion;
            _appliedRangeRemote = remoteVersion;
            ApplyUpdateRange(localVersion, remoteVersion);
        }

        private void SetButtonDownloadProgress(bool active, float progress01 = 0f)
        {
            if (_updateButton != null)
            {
                _updateButton.RemoveFromClassList(CtaDownloadingClass);
            }

            if (!active)
            {
                SetOverlayHidden(_versionProgress, true);

                if (_versionProgressFill != null)
                {
                    _versionProgressFill.style.width = Length.Percent(0);
                }

                if (_versionProgressLabel != null)
                {
                    _versionProgressLabel.text = string.Empty;
                    SetOverlayHidden(_versionProgressLabel, true);
                }

                return;
            }

            SetOverlayHidden(_versionProgress, false);

            if (_versionProgressFill != null)
            {
                _versionProgressFill.style.width =
                    Length.Percent(GameUpdateUiRules.ProgressPercent(progress01));
            }

            var isComplete = progress01 >= 1f;
            var buttonText = isComplete
                ? "ИГРАТЬ"
                : GameUpdateUiRules.FormatProgressLabel(progress01);

            if (_versionProgressLabel != null)
            {
                _versionProgressLabel.text = buttonText;
                SetOverlayHidden(_versionProgressLabel, false);
            }

            if (_updateButton != null)
            {
                _updateButton.text = string.Empty;
                _updateButton.AddToClassList(CtaDownloadingClass);
                _updateButton.SetEnabled(!isComplete);
            }
        }

        private void SetDownloadProgress(float progress01)
        {
            SetButtonDownloadProgress(true, progress01);
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

            if (GameUpdateService.CheckFailed)
            {
                var detail = string.IsNullOrWhiteSpace(GameUpdateService.LastError)
                    ? "Попробуйте позже."
                    : GameUpdateService.LastError;
                SetClientStatus("ПОДГОТОВКА", $"Не удалось проверить обновления: {detail}");
                await UniTask.Delay(TimeSpan.FromSeconds(1.2f), ignoreTimeScale: true);
            }

            await WarmSocialServicesAsync();
            if (this == null)
            {
                return;
            }

            ShowReadyToEnter();
        }

        private async UniTask WarmSocialServicesAsync()
        {
            try
            {
                ShowLoading("Авторизация");
                if (!await TryAwaitWithTimeout(
                        UnityServicesBootstrap.EnsureInitializedAsync(),
                        UgsInitTimeoutSeconds))
                {
                    Debug.LogWarning(
                        $"Bootstrap: UGS init timed out after {UgsInitTimeoutSeconds:0}s, continuing without cloud.");
                    return;
                }

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
                        if (!await TryAwaitWithTimeout(
                                FriendsHubService.InitializeAsync(),
                                FriendsInitTimeoutSeconds))
                        {
                            Debug.LogWarning(
                                $"Bootstrap: Friends init timed out after {FriendsInitTimeoutSeconds:0}s.");
                        }
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

        private static async UniTask<bool> TryAwaitWithTimeout(UniTask task, float timeoutSeconds)
        {
            var winner = await UniTask.WhenAny(
                task,
                UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), ignoreTimeScale: true));
            return winner == 0;
        }

        private void OnUpdateClicked()
        {
            ApplyUpdateAsync().Forget();
        }

        private void OnEnterGameClicked()
        {
            if (_isEnteringGame)
            {
                return;
            }

            EnterGameAsync().Forget();
        }

        private void OnQuitClicked()
        {
            if (_isUpdating || _isEnteringGame || _quitButton == null || !_quitButton.enabledSelf)
            {
                return;
            }

            Application.Quit();
        }

        private async UniTaskVoid EnterGameAsync()
        {
            _isEnteringGame = true;
            SetQuitInteractive(false);
            SetEnterGameInteractive(false);

            ShowEnteringGame();
            await SceneManager.LoadSceneAsync(GameSceneNames.MainMenu);
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
                var progress = new Progress<float>(SetDownloadProgress);
                await GameUpdateService.DownloadAndApplyAsync(progress);
            }
            catch (Exception ex)
            {
                _isUpdating = false;
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.text = "Ошибка обновления: " + ex.Message;
                }

                ShowUpdateAvailable(GameLocalVersion.Current, remote);
            }
        }

        private void ShowLoading(string status)
        {
            SetOverlayHidden(_loadingPanel, false);
            SetOverlayHidden(_updatePanel, true);
            SetOverlayHidden(_enterGameButton, true);
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            SetOverlayHidden(_updateStatusLabel, true);
            SetQuitInteractive(false);
            SetClientStatus("ПОДГОТОВКА", status ?? string.Empty);
        }

        private void ShowReadyToEnter()
        {
            SetOverlayHidden(_loadingPanel, false);
            SetOverlayHidden(_updatePanel, true);
            SetOverlayHidden(_enterGameButton, false);
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            SetOverlayHidden(_updateStatusLabel, true);
            SetClientStatus("ГОТОВО", "Клиент готов к запуску");

            if (_enterGameButton != null)
            {
                SetEnterGameInteractive(true);
            }

            SetQuitInteractive(true);
        }

        private void ShowEnteringGame()
        {
            SetOverlayHidden(_loadingPanel, false);
            SetOverlayHidden(_updatePanel, true);
            SetOverlayHidden(_enterGameButton, false);
            SetEnterGameInteractive(false);
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            SetOverlayHidden(_updateStatusLabel, true);
            SetQuitInteractive(false);
            SetClientStatus("ЗАПУСК", "Вход в игру…");
        }

        private void ShowUpdateAvailable(string localVersion, string remoteVersion)
        {
            _isUpdating = false;
            SetOverlayHidden(_loadingPanel, true);
            SetOverlayHidden(_updatePanel, false);
            SetOverlayHidden(_enterGameButton, true);
            SetUpdateMetaVisible(true);
            SetButtonDownloadProgress(false);
            if (_updateButton != null)
            {
                _updateButton.text = "ОБНОВИТЬ";
                _updateButton.SetEnabled(true);
                SetOverlayHidden(_updateButton, false);
            }

            if (_updateTitleLabel != null)
            {
                _updateTitleLabel.text = "ДОСТУПНО ОБНОВЛЕНИЕ";
            }

            ApplyUpdateRangeIfNeeded(localVersion, remoteVersion);
            SetClientStatus("ОБНОВЛЕНИЕ", "Доступна новая версия клиента");
            SetQuitInteractive(true);

            if (_updateStatusLabel != null)
            {
                if (string.IsNullOrEmpty(_updateStatusLabel.text))
                {
                    SetOverlayHidden(_updateStatusLabel, true);
                }
                else
                {
                    SetOverlayHidden(_updateStatusLabel, false);
                }
            }
        }

        private void ShowDownloading(string localVersion, string remoteVersion, float progress01)
        {
            SetOverlayHidden(_loadingPanel, true);
            SetOverlayHidden(_updatePanel, false);
            SetOverlayHidden(_enterGameButton, true);
            SetUpdateMetaVisible(true);
            SetOverlayHidden(_updateButton, false);
            SetOverlayHidden(_updateStatusLabel, true);
            SetQuitInteractive(false);
            SetButtonDownloadProgress(true, progress01);

            if (_updateTitleLabel != null)
            {
                _updateTitleLabel.text = "ОБНОВЛЕНИЕ";
            }

            ApplyUpdateRangeIfNeeded(localVersion, remoteVersion);
            SetClientStatus(
                "ОБНОВЛЕНИЕ",
                "Загрузка "
                + GameUpdateUiRules.FormatVersionLabel(remoteVersion));
        }

        private void ApplyUpdateRange(string localVersion, string remoteVersion)
        {
            if (_updateRangeLabel == null)
            {
                return;
            }

            // Arrow → is not in Noto Sans; use a dedicated Symbols glyph label.
            _updateRangeLabel.Clear();

            var localLabel = new Label(GameUpdateUiRules.FormatVersionLabel(localVersion));
            localLabel.AddToClassList("bl__range-part");
            localLabel.AddToClassList("bl__range-part--local");
            localLabel.pickingMode = PickingMode.Ignore;

            var arrowLabel = new Label(GameUpdateUiRules.UpdateRangeArrow);
            arrowLabel.AddToClassList("bl__range-arrow");
            arrowLabel.pickingMode = PickingMode.Ignore;

            var remoteLabel = new Label(GameUpdateUiRules.FormatVersionLabel(remoteVersion));
            remoteLabel.AddToClassList("bl__range-part");
            remoteLabel.AddToClassList("bl__range-part--remote");
            remoteLabel.pickingMode = PickingMode.Ignore;

            _updateRangeLabel.Add(localLabel);
            _updateRangeLabel.Add(arrowLabel);
            _updateRangeLabel.Add(remoteLabel);
        }

        private void SetEnterGameInteractive(bool interactive)
        {
            if (_enterGameButton == null)
            {
                return;
            }

            if (interactive)
            {
                _enterGameButton.RemoveFromClassList(MutedButtonClass);
                _enterGameButton.AddToClassList(PrimaryButtonClass);
                _enterGameButton.SetEnabled(true);
                _enterGameButton.pickingMode = PickingMode.Position;
                return;
            }

            _enterGameButton.RemoveFromClassList(PrimaryButtonClass);
            _enterGameButton.AddToClassList(MutedButtonClass);
            _enterGameButton.SetEnabled(false);
            _enterGameButton.pickingMode = PickingMode.Ignore;
        }

        private void SetQuitInteractive(bool interactive)
        {
            if (_quitButton == null)
            {
                return;
            }

            SetOverlayHidden(_quitButton, false);
            _quitButton.SetEnabled(interactive);
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
