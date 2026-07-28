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
        Installing = 4,
        ReadyToRestart = 5,
    }

    /// <summary>
    /// Bootstrap loading screen: version check, force-update UI, UGS warm-up, then wait for Enter Game.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BootstrapLoadingController : MonoBehaviour
    {
        private const string OverlayHiddenClass = "ui-overlay--hidden";
        private const string SlotInactiveClass = "bl__slot--inactive";
        private const string CtaDownloadingClass = "bl__cta--downloading";
        private const string PrimaryButtonClass = "ui-btn--primary";
        private const string MutedButtonClass = "ui-btn--muted";
        private const float UgsInitTimeoutSeconds = 8f;
        private const float FriendsInitTimeoutSeconds = 8f;
#if BARAKI_UPDATER_ONLY
        private const bool IsUpdaterOnlyBuild = true;
#else
        private const bool IsUpdaterOnlyBuild = false;
#endif

        [SerializeField] private UIDocument _uiDocument;

#if UNITY_EDITOR
        [Header("Editor preview")]
        [SerializeField] private BootstrapPreviewMode _previewMode = BootstrapPreviewMode.Loading;
        [SerializeField] private string _previewStatusText = "Проверка версии";
        [SerializeField] private string _previewLocalVersion = "0.1.2";
        [SerializeField] private string _previewRemoteVersion = "0.1.4";
        [SerializeField] [Range(0f, 1f)] private float _previewDownloadProgress = 0.37f;
        [SerializeField] [Range(0f, 1f)] private float _previewInstallProgress = 0.55f;
#endif

        private VisualElement _root;
        private VisualElement _loadingPanel;
        private VisualElement _updatePanel;
        private VisualElement _idleCtaPanel;
        private Label _idleCtaLabel;
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
        private string _applyRemoteVersion;
        private GameUpdateApplyPhase _applyPhase = GameUpdateApplyPhase.Downloading;
        private bool _isUpdating;
        private bool _isReadyToRestart;
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
                    ShowApplying(
                        _previewLocalVersion,
                        _previewRemoteVersion,
                        GameUpdateApplyPhase.Downloading,
                        _previewDownloadProgress);
                    break;
                case BootstrapPreviewMode.Installing:
                    ShowApplying(
                        _previewLocalVersion,
                        _previewRemoteVersion,
                        GameUpdateApplyPhase.Installing,
                        _previewInstallProgress);
                    break;
                case BootstrapPreviewMode.ReadyToRestart:
                    ShowReadyToRestart(_previewLocalVersion, _previewRemoteVersion);
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
            _idleCtaPanel = _root.Q<VisualElement>("IdleCtaPanel");
            _idleCtaLabel = _root.Q<Label>("IdleCtaLabel");
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
            SetCtaMode(CtaMode.Idle, "ПОДГОТОВКА…");

            // Quit stays visible; interactivity is toggled by pipeline state.
            SetOverlayHidden(_quitButton, false);
            SetQuitInteractive(false);
        }

        /// <summary>Fills featured patch + secondary patch cards. Safe to call again with a remote feed later.</summary>
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

            SetStatusHeader(safeTitle);

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

        private void SetStatusHeader(string title, string versionText = null)
        {
            if (_sideStatusTitle != null)
            {
                _sideStatusTitle.text = title ?? string.Empty;
            }

            if (_versionLabel != null)
            {
                _versionLabel.text = string.IsNullOrWhiteSpace(versionText)
                    ? GameUpdateUiRules.FormatVersionLabel(GameLocalVersion.Current)
                    : versionText;
            }
        }

        private void SetStatusHeaderRange(string localVersion, string remoteVersion)
        {
            SetStatusHeader(
                GameUpdateUiRules.FormatVersionLabel(localVersion),
                "-> " + GameUpdateUiRules.FormatVersionLabel(remoteVersion));
        }

        private void SetUpdateMetaVisible(bool visible)
        {
            SetSlotInactive(_updateMeta, !visible);

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

        private void SetButtonDownloadProgress(
            bool active,
            float progress01 = 0f,
            GameUpdateApplyPhase phase = GameUpdateApplyPhase.Downloading)
        {
            if (_updateButton != null)
            {
                _updateButton.RemoveFromClassList(CtaDownloadingClass);
            }

            if (!active)
            {
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

            var bar01 = GameUpdateApplyProgressRules.MapBarProgress(phase, progress01);
            if (_versionProgressFill != null)
            {
                _versionProgressFill.style.width =
                    Length.Percent(GameUpdateUiRules.ProgressPercent(bar01));
            }

            var buttonText = GameUpdateApplyProgressRules.FormatProgressLabel(phase, progress01);

            if (_versionProgressLabel != null)
            {
                _versionProgressLabel.text = buttonText;
                SetOverlayHidden(_versionProgressLabel, false);
            }

            if (_updateButton != null)
            {
                _updateButton.text = phase == GameUpdateApplyPhase.Installing
                    ? "УСТАНОВКА"
                    : "ЗАГРУЗКА";
                _updateButton.SetEnabled(false);
            }
        }

        private void ReserveUpdateProgressSlot()
        {
            if (_versionProgress == null)
            {
                return;
            }

            SetOverlayHidden(_versionProgress, false);
            if (_versionProgressFill != null)
            {
                _versionProgressFill.style.width = Length.Percent(0);
            }
        }

        private void OnApplyProgress(GameUpdateApplyProgress progress)
        {
            _applyPhase = progress.Phase;
            if (progress.Phase == GameUpdateApplyPhase.ReadyToRestart)
            {
                return;
            }

            SetButtonDownloadProgress(true, progress.Phase01, progress.Phase);
            SetClientStatus(
                "ОБНОВЛЕНИЕ",
                GameUpdateApplyProgressRules.FormatSideStatus(progress.Phase, _applyRemoteVersion));
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

            if (!BootstrapUpdateFlowRules.ShouldOfferEnterGame(
                    IsUpdaterOnlyBuild,
                    GameUpdateService.UpdateRequired))
            {
                ShowUpdaterOnlyIdle(GameUpdateService.CheckFailed);
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
            if (_isReadyToRestart)
            {
                RestartPreparedUpdate();
                return;
            }

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
            if (_isUpdating || _isReadyToRestart || !GameUpdateService.UpdateRequired)
            {
                return;
            }

            _isUpdating = true;
            var remote = GameUpdateService.RemoteManifest?.version;
            _applyRemoteVersion = remote;
            ShowApplying(GameLocalVersion.Current, remote, GameUpdateApplyPhase.Downloading, 0f);
            if (_updateStatusLabel != null)
            {
                _updateStatusLabel.text = string.Empty;
            }

            SetSlotInactive(_updateStatusLabel, true);

            try
            {
                var progress = new Progress<GameUpdateApplyProgress>(OnApplyProgress);
                await GameUpdateService.DownloadAndPrepareAsync(progress);
                if (this == null)
                {
                    return;
                }

                _isUpdating = false;
                _isReadyToRestart = true;
                ShowReadyToRestart(GameLocalVersion.Current, remote);
            }
            catch (Exception ex)
            {
                _isUpdating = false;
                _isReadyToRestart = false;
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.text = "Ошибка обновления: " + ex.Message;
                }

                SetSlotInactive(_updateStatusLabel, false);
                ShowUpdateAvailable(GameLocalVersion.Current, remote);
            }
        }

        private void RestartPreparedUpdate()
        {
            if (!_isReadyToRestart || !GameUpdateService.HasPendingRestart)
            {
                return;
            }

            try
            {
                SetQuitInteractive(false);
                if (_updateButton != null)
                {
                    _updateButton.SetEnabled(false);
                }

                SetClientStatus("ОБНОВЛЕНИЕ", "Перезапуск клиента");
                GameUpdateService.ApplyPendingRestartAndQuit();
            }
            catch (Exception ex)
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.text = "Ошибка перезапуска: " + ex.Message;
                }

                SetSlotInactive(_updateStatusLabel, false);
                SetQuitInteractive(true);
                if (_updateButton != null)
                {
                    _updateButton.SetEnabled(true);
                }
            }
        }

        private void ShowLoading(string status)
        {
            SetCtaMode(CtaMode.Idle, BootstrapUpdateFlowRules.FormatIdleCtaLabel(status));
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            SetSlotInactive(_updateStatusLabel, true);
            SetQuitInteractive(false);
            SetClientStatus("ПОДГОТОВКА", status ?? string.Empty);
        }

        private void ShowUpdaterOnlyIdle(bool checkFailed)
        {
            var status = "Последняя версия уже установлена";
            if (checkFailed)
            {
                var detail = string.IsNullOrWhiteSpace(GameUpdateService.LastError)
                    ? "Попробуйте позже."
                    : GameUpdateService.LastError;
                status = $"Не удалось проверить обновления: {detail}";
            }

            SetCtaMode(CtaMode.Idle, "ГОТОВО");
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            SetSlotInactive(_updateStatusLabel, true);
            SetClientStatus("АПДЕЙТЕР", status);
            SetQuitInteractive(true);
        }

        private void ShowReadyToEnter()
        {
            SetCtaMode(CtaMode.EnterGame);
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            SetSlotInactive(_updateStatusLabel, true);
            SetClientStatus("ГОТОВО", "Клиент готов к запуску");

            if (_enterGameButton != null)
            {
                SetEnterGameInteractive(true);
            }

            SetQuitInteractive(true);
        }

        private void ShowEnteringGame()
        {
            SetCtaMode(CtaMode.EnterGame);
            SetEnterGameInteractive(false);
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            SetSlotInactive(_updateStatusLabel, true);
            SetQuitInteractive(false);
            SetClientStatus("ЗАПУСК", "Вход в игру…");
        }

        private void ShowUpdateAvailable(string localVersion, string remoteVersion)
        {
            _isUpdating = false;
            _isReadyToRestart = false;
            SetCtaMode(CtaMode.Update);
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            ReserveUpdateProgressSlot();
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

            SetClientStatus("ОБНОВЛЕНИЕ", "Доступна новая версия клиента");
            SetStatusHeaderRange(localVersion, remoteVersion);
            SetQuitInteractive(true);

            if (_updateStatusLabel != null)
            {
                SetSlotInactive(_updateStatusLabel, string.IsNullOrEmpty(_updateStatusLabel.text));
            }
        }

        private void ShowReadyToRestart(string localVersion, string remoteVersion)
        {
            _isUpdating = false;
            _isReadyToRestart = true;
            _applyPhase = GameUpdateApplyPhase.ReadyToRestart;
            _applyRemoteVersion = remoteVersion;

            SetCtaMode(CtaMode.Update);
            SetUpdateMetaVisible(false);
            SetButtonDownloadProgress(false);
            ReserveUpdateProgressSlot();
            SetSlotInactive(_updateStatusLabel, true);
            SetQuitInteractive(true);

            if (_updateButton != null)
            {
                _updateButton.text = GameUpdateUiRules.RestartButtonLabel;
                _updateButton.RemoveFromClassList(CtaDownloadingClass);
                _updateButton.SetEnabled(true);
                SetOverlayHidden(_updateButton, false);
            }

            if (_updateTitleLabel != null)
            {
                _updateTitleLabel.text = "ОБНОВЛЕНИЕ";
            }

            SetClientStatus(
                "ОБНОВЛЕНИЕ",
                GameUpdateApplyProgressRules.FormatSideStatus(
                    GameUpdateApplyPhase.ReadyToRestart,
                    remoteVersion));
            SetStatusHeaderRange(localVersion, remoteVersion);
        }

        private void ShowApplying(
            string localVersion,
            string remoteVersion,
            GameUpdateApplyPhase phase,
            float phase01)
        {
            _applyRemoteVersion = remoteVersion;
            _applyPhase = phase;
            _isReadyToRestart = false;

            SetCtaMode(CtaMode.Update);
            SetUpdateMetaVisible(false);
            SetSlotInactive(_updateStatusLabel, true);
            SetQuitInteractive(false);
            SetButtonDownloadProgress(true, phase01, phase);

            if (_updateTitleLabel != null)
            {
                _updateTitleLabel.text = "ОБНОВЛЕНИЕ";
            }

            SetClientStatus(
                "ОБНОВЛЕНИЕ",
                GameUpdateApplyProgressRules.FormatSideStatus(phase, remoteVersion));
            SetStatusHeaderRange(localVersion, remoteVersion);
        }

        private enum CtaMode
        {
            Idle = 0,
            EnterGame = 1,
            Update = 2,
        }

        private void SetCtaMode(CtaMode mode, string idleLabel = null)
        {
            var showIdle = mode == CtaMode.Idle;
            var showEnter = mode == CtaMode.EnterGame;
            var showUpdate = mode == CtaMode.Update;

            SetOverlayHidden(_idleCtaPanel, !showIdle);
            SetOverlayHidden(_loadingPanel, !showEnter);
            SetOverlayHidden(_updatePanel, !showUpdate);

            if (showIdle && _idleCtaLabel != null)
            {
                _idleCtaLabel.text = string.IsNullOrWhiteSpace(idleLabel)
                    ? "ПОДГОТОВКА…"
                    : idleLabel.Trim().ToUpperInvariant();
            }

            if (showEnter && _enterGameButton != null)
            {
                SetOverlayHidden(_enterGameButton, false);
            }
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

        private static void SetSlotInactive(VisualElement element, bool inactive)
        {
            if (element == null)
            {
                return;
            }

            // Keeps layout height so status / range / error do not jump when toggling.
            if (inactive)
            {
                element.AddToClassList(SlotInactiveClass);
            }
            else
            {
                element.RemoveFromClassList(SlotInactiveClass);
            }
        }
    }
}
