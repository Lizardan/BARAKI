using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI.Controllers
{
    /// <summary>
    /// Bootstrap launcher: news/chat shell plus real update check, apply, and enter-game pipeline.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    public sealed class LauncherController : MonoBehaviour
    {
        private const string ProgressDetailsHiddenClass = "ln-progress__details--hidden";
        private const string ProgressErrorHiddenClass = "ln-progress__error--hidden";
        private const float ShinePeriodSeconds = 1.6f;
        private const float UgsInitTimeoutSeconds = 8f;
        private const float FriendsInitTimeoutSeconds = 8f;
        private const float ChatInitTimeoutSeconds = 10f;
#if BARAKI_UPDATER_ONLY
        private const bool IsUpdaterOnlyBuild = true;
#else
        private const bool IsUpdaterOnlyBuild = false;
#endif

        private static readonly string[] NewsImageClasses =
        {
            "ln-news-card__image--a",
            "ln-news-card__image--b",
            "ln-news-card__image--c",
        };

        [SerializeField] private UIDocument _uiDocument;

#if UNITY_EDITOR
        [Header("Editor preview")]
        [SerializeField] private LauncherProgressPhase _previewPhase = LauncherProgressPhase.Downloading;
        [SerializeField] [Range(0f, 1f)] private float _previewProgress = 0.34f;
        [SerializeField] private string _previewLocalVersion = "0.2.0";
        [SerializeField] private string _previewRemoteVersion = "0.2.2";
        [SerializeField] private string _previewWarmingDetail = "Авторизация";
        [SerializeField] private string _previewError = "";
#endif

        private VisualElement _root;
        private Button _playButton;
        private Button _returnToMatchButton;
        private Button _heroReadMoreButton;
        private Button _closeButton;
        private VisualElement _newsList;
        private Label _clientVersionLabel;
        private Label _progressStatusLabel;
        private Label _progressPercentLabel;
        private VisualElement _progressDetails;
        private VisualElement _progressFill;
        private VisualElement _progressShine;
        private Label _progressErrorLabel;
        private MenuChatPanel _menuChat;
        private readonly LauncherWindowChromeDriver _windowChrome = new();
        private LauncherProgressPhase _phase = LauncherProgressPhase.Ready;
        private IVisualElementScheduledItem _shineSchedule;
        private IVisualElementScheduledItem _barSchedule;
        private float _shineElapsed;
        private float _visualBar01;
        private float _targetBar01;
        private bool _barCatchingUp;
        private UniTask _barVisualChain = UniTask.CompletedTask;
        private bool _isEntering;
        private bool _isUpdating;
        private bool _isReadyToRestart;
        private bool _pipelineStarted;
        private string _applyRemoteVersion;
        private string _warmingDetail;
        private string _rangeLocalVersion;
        private string _rangeRemoteVersion;
#if UNITY_EDITOR
        private bool _previewApplyQueued;
        private int _previewRetryCount;
#endif

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            BindUi();
            PopulatePlaceholders();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
                return;
            }
#endif

            ApplyProgress(LauncherProgressPhase.Checking, 0f);
        }

        private void OnEnable()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            BindUi();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                QueueEditorPreviewApply();
                return;
            }
#endif

            PopulatePlaceholders();
            ScheduleUiRefresh();
            RegisterCallbacks(true);
            if (!_pipelineStarted)
            {
                _pipelineStarted = true;
                RunBootstrapPipelineAsync().Forget();
            }
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                StopShineAnimation();
                return;
            }
#endif

            RegisterCallbacks(false);
            StopShineAnimation();
            StopBarTicker();
            _windowChrome.Detach();
        }

        private void OnDestroy()
        {
            _menuChat?.Dispose();
            _windowChrome.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            QueueEditorPreviewApply();
        }

        private void QueueEditorPreviewApply()
        {
            if (_previewApplyQueued)
            {
                return;
            }

            _previewApplyQueued = true;
            EditorApplication.delayCall += ApplyEditorPreviewDeferred;
        }

        private void ApplyEditorPreviewDeferred()
        {
            _previewApplyQueued = false;
            if (this == null || Application.isPlaying)
            {
                return;
            }

            ApplyEditorPreview();
            if (_root == null)
            {
                if (_previewRetryCount < 8)
                {
                    _previewRetryCount++;
                    QueueEditorPreviewApply();
                }

                return;
            }

            _previewRetryCount = 0;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        /// <summary>Inspector / edit-mode preview for all launcher pipeline states.</summary>
        public void ApplyEditorPreview()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            _root = null;
            BindUi();
            if (_root == null)
            {
                return;
            }

            PopulatePlaceholders();
            _warmingDetail = _previewWarmingDetail;

            if (_previewPhase is LauncherProgressPhase.UpdateAvailable or LauncherProgressPhase.ReadyToRestart
                or LauncherProgressPhase.Downloading or LauncherProgressPhase.Installing)
            {
                SetUpdateRange(_previewLocalVersion, _previewRemoteVersion);
            }
            else
            {
                SetUpdateRange(null, null);
            }

            ApplyProgress(
                _previewPhase,
                _previewProgress,
                _previewError);
        }
#endif

        private void BindUi()
        {
            _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            if (_root == null)
            {
                return;
            }

            _playButton = _root.Q<Button>("PlayButton");
            _returnToMatchButton = _root.Q<Button>("ReturnToMatchButton");
            _heroReadMoreButton = _root.Q<Button>("HeroReadMoreButton");
            _closeButton = _root.Q<Button>("CloseButton");
            _newsList = _root.Q<VisualElement>("NewsList");
            _clientVersionLabel = _root.Q<Label>("ClientVersionLabel");
            _progressStatusLabel = _root.Q<Label>("ProgressStatusLabel");
            _progressPercentLabel = _root.Q<Label>("ProgressPercentLabel");
            _progressDetails = _root.Q<VisualElement>("ProgressDetails");
            _progressFill = _root.Q<VisualElement>("ProgressFill");
            _progressShine = _root.Q<VisualElement>("ProgressShine");
            _progressErrorLabel = _root.Q<Label>("ProgressErrorLabel");

            _menuChat?.Dispose();
            _menuChat = new MenuChatPanel(
                _root,
                messageRowClass: "ln-chat-message",
                messageMetaClass: "ln-chat-message__meta",
                messageNickClass: "ln-chat-message__nick",
                messageTimeClass: "ln-chat-message__time",
                messageTextClass: "ln-chat-message__text",
                tabActiveClass: "ln-chat-tab--active");
            _menuChat.Bind();

            var chatInput = _root.Q<TextField>("ChatInput");
            if (chatInput != null && chatInput.textEdition != null)
            {
                chatInput.value = string.Empty;
                chatInput.textEdition.placeholder = "Написать в чат…";
            }

            BindWindowChrome();
        }

        private void BindWindowChrome()
        {
            var screen = _root != null ? _root.Q<VisualElement>("LauncherScreen") ?? _root : null;
            _windowChrome.Attach(screen, _closeButton, QuitLauncher);
        }

        private static void QuitLauncher()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
#else
            Application.Quit();
#endif
        }

        private void PopulatePlaceholders()
        {
            if (_clientVersionLabel != null)
            {
                _clientVersionLabel.text = GameUpdateUiRules.FormatVersionLabel(GameLocalVersion.Current);
            }

            BindNewsCards();
        }

        /// <summary>
        /// UIDocument may rebuild the visual tree after Awake/OnEnable; refresh once the panel is ready.
        /// </summary>
        private void ScheduleUiRefresh()
        {
            if (_root == null)
            {
                return;
            }

            _root.schedule.Execute(() =>
            {
                if (this == null || !isActiveAndEnabled)
                {
                    return;
                }

                BindUi();
                PopulatePlaceholders();
            }).StartingIn(0);
        }

        /// <summary>Updates launcher progress UI and primary CTA for the given pipeline phase.</summary>
        public void ApplyProgress(
            LauncherProgressPhase phase,
            float progress01,
            string errorText = null)
        {
            _phase = phase;

            if (_progressStatusLabel != null)
            {
                _progressStatusLabel.text = LauncherProgressRules.StatusLabel(
                    phase,
                    _warmingDetail,
                    _rangeLocalVersion,
                    _rangeRemoteVersion);
            }

            var percent = GameUpdateUiRules.ProgressPercent(_visualBar01);
            if (_progressPercentLabel != null)
            {
                _progressPercentLabel.text = LauncherProgressRules.ShouldShowProgressDetails(phase)
                    ? percent + "%"
                    : string.Empty;
            }

            _targetBar01 = Mathf.Clamp(progress01, 0f, 1f);
            if (!LauncherProgressRules.ShouldShowProgressDetails(phase))
            {
                _visualBar01 = _targetBar01;
            }

            ApplyVisualBarWidth();
            EnsureBarTicker();

            var showDetails = LauncherProgressRules.ShouldShowProgressDetails(phase);
            if (_progressDetails != null)
            {
                _progressDetails.EnableInClassList(ProgressDetailsHiddenClass, !showDetails);
            }

            SetErrorText(errorText);

            if (_playButton != null && !_isEntering && !_isUpdating)
            {
                _playButton.text = LauncherProgressRules.CtaLabel(phase);
                _playButton.SetEnabled(LauncherProgressRules.IsCtaEnabled(phase));
            }

            if (showDetails)
            {
                StartShineAnimation();
            }
            else
            {
                StopShineAnimation();
            }
        }

        private async UniTaskVoid RunBootstrapPipelineAsync()
        {
            PlayerProfileService.PrimeFromLocalPrefs();
            UnityServicesBootstrap.PrimeCachedPlayerNameFromPrefs();

            ApplyProgress(LauncherProgressPhase.Checking, 0f);
            SetUpdateRange(null, null);

            if (GameUpdateService.TryRestorePendingRestart() && GameUpdateService.HasPendingRestart)
            {
                var pendingRemote = string.IsNullOrWhiteSpace(GameUpdateService.PendingRemoteVersion)
                    ? GameUpdateService.RemoteManifest?.version
                    : GameUpdateService.PendingRemoteVersion;
                ShowReadyToRestart(GameLocalVersion.Current, pendingRemote);
                return;
            }

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
                _warmingDetail = $"Не удалось проверить обновления: {detail}";
                ApplyProgress(LauncherProgressPhase.Warming, 0f);
                await UniTask.Delay(TimeSpan.FromSeconds(1.2f), ignoreTimeScale: true);
            }

            await WarmSocialServicesAsync();
            if (this == null)
            {
                return;
            }

            ShowReadyToEnter();
            RefreshReturnToMatchButton();
        }

        private async UniTask WarmSocialServicesAsync()
        {
            try
            {
                SetWarming("Авторизация");
                if (!await TryAwaitWithTimeout(
                        UnityServicesBootstrap.EnsureInitializedAsync(),
                        UgsInitTimeoutSeconds))
                {
                    Debug.LogWarning(
                        $"Launcher: UGS init timed out after {UgsInitTimeoutSeconds:0}s, continuing without cloud.");
                    return;
                }

                SetWarming("Профиль");
                try
                {
                    await PlayerProfileService.LoadAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Launcher profile load skipped: {ex.Message}");
                }

                SetWarming("Друзья");
                if (UnityServicesBootstrap.IsReady)
                {
                    try
                    {
                        if (!await TryAwaitWithTimeout(
                                FriendsHubService.InitializeAsync(),
                                FriendsInitTimeoutSeconds))
                        {
                            Debug.LogWarning(
                                $"Launcher: Friends init timed out after {FriendsInitTimeoutSeconds:0}s.");
                        }
                    }
                    catch (Exception friendsEx)
                    {
                        Debug.LogWarning($"Launcher friends init skipped: {friendsEx.Message}");
                    }
                }

                SetWarming(GameChatRules.WarmingStatusLabel);
                if (UnityServicesBootstrap.IsReady)
                {
                    try
                    {
                        if (!await TryAwaitWithTimeout(
                                GameChatService.EnsureInitializedAsync(),
                                ChatInitTimeoutSeconds))
                        {
                            Debug.LogWarning(
                                $"Launcher: Chat init timed out after {ChatInitTimeoutSeconds:0}s.");
                        }
                    }
                    catch (Exception chatEx)
                    {
                        Debug.LogWarning($"Launcher chat init skipped: {chatEx.Message}");
                    }
                }
            }
            catch (Exception socialEx)
            {
                Debug.LogWarning($"Launcher social warm-up skipped: {socialEx.Message}");
            }
        }

        private void SetWarming(string detail)
        {
            _warmingDetail = detail;
            ApplyProgress(LauncherProgressPhase.Warming, 0f);
        }

        private static async UniTask<bool> TryAwaitWithTimeout(UniTask task, float timeoutSeconds)
        {
            var winner = await UniTask.WhenAny(
                task,
                UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), ignoreTimeScale: true));
            return winner == 0;
        }

        private void ShowUpdateAvailable(string localVersion, string remoteVersion, string errorText = null)
        {
            _isUpdating = false;
            _isReadyToRestart = false;
            _applyRemoteVersion = remoteVersion;
            SetUpdateRange(localVersion, remoteVersion);
            ApplyProgress(LauncherProgressPhase.UpdateAvailable, 0f, errorText);
        }

        private void ShowReadyToRestart(string localVersion, string remoteVersion)
        {
            _isUpdating = false;
            _isReadyToRestart = true;
            _applyRemoteVersion = remoteVersion;
            ApplyProgress(LauncherProgressPhase.ReadyToRestart, 1f);
            SetUpdateRange(localVersion, remoteVersion);
            if (_playButton != null)
            {
                _playButton.text = LauncherProgressRules.CtaLabel(LauncherProgressPhase.ReadyToRestart);
                _playButton.SetEnabled(true);
            }
        }

        private void ShowReadyToEnter()
        {
            _isUpdating = false;
            _isReadyToRestart = false;
            _warmingDetail = null;
            ApplyProgress(LauncherProgressPhase.Ready, 1f);
            SetUpdateRange(null, null);
            RefreshReturnToMatchButton();
        }

        private void ShowUpdaterOnlyIdle(bool checkFailed)
        {
            _isUpdating = false;
            _isReadyToRestart = false;
            if (checkFailed)
            {
                var detail = string.IsNullOrWhiteSpace(GameUpdateService.LastError)
                    ? "Попробуйте позже."
                    : GameUpdateService.LastError;
                _warmingDetail = $"Не удалось проверить обновления: {detail}";
                ApplyProgress(LauncherProgressPhase.Warming, 0f);
            }
            else
            {
                _warmingDetail = "Последняя версия уже установлена";
                ApplyProgress(LauncherProgressPhase.Warming, 1f);
            }

            SetUpdateRange(null, null);
            if (_playButton != null)
            {
                _playButton.SetEnabled(false);
            }
        }

        void RefreshReturnToMatchButton()
        {
            if (_returnToMatchButton == null)
            {
                return;
            }

            var show = _phase == LauncherProgressPhase.Ready
                       && !_isEntering
                       && PendingMatchReconnectStore.TryLoadActive(out _);
            _returnToMatchButton.EnableInClassList("ln-btn--hidden", !show);
            _returnToMatchButton.SetEnabled(show);
        }

        void OnReturnToMatchClicked()
        {
            if (_isEntering || _phase != LauncherProgressPhase.Ready)
            {
                return;
            }

            ReturnToMatchAsync().Forget();
        }

        async UniTaskVoid ReturnToMatchAsync()
        {
            _isEntering = true;
            if (_playButton != null)
            {
                _playButton.SetEnabled(false);
            }

            if (_returnToMatchButton != null)
            {
                _returnToMatchButton.SetEnabled(false);
            }

            _warmingDetail = "Возврат в матч…";
            ApplyProgress(LauncherProgressPhase.Warming, 1f);
            RefreshReturnToMatchButton();

            try
            {
                if (!await MatchNetworkSession.TryReturnToPendingMatchAsync())
                {
                    throw new InvalidOperationException("Не удалось вернуться в матч.");
                }

                await SceneManager.LoadSceneAsync(GameSceneNames.Lobby).ToUniTask();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Launcher] Return to match failed: {ex.Message}");
                PendingMatchReconnectStore.Clear();
                _isEntering = false;
                ShowReadyToEnter();
                SetErrorText("Матч уже недоступен.");
            }
        }

        private void OnApplyProgress(GameUpdateApplyProgress progress)
        {
            _barVisualChain = ContinueBarVisualAsync(_barVisualChain, progress);
        }

        private async UniTask ContinueBarVisualAsync(UniTask previous, GameUpdateApplyProgress progress)
        {
            try
            {
                await previous;
            }
            catch (System.OperationCanceledException)
            {
            }

            if (this == null)
            {
                return;
            }

            if (!GameUpdatePendingRestartRules.ShouldAcceptApplyProgress(_isReadyToRestart, progress.Phase))
            {
                return;
            }

            if (progress.Phase == GameUpdateApplyPhase.ReadyToRestart)
            {
                await CatchUpBarAsync(1f);
                return;
            }

            var phase = LauncherProgressRules.FromApplyPhase(progress.Phase);
            var bar01 = GameUpdateApplyProgressRules.MapBarProgress(progress.Phase, progress.Phase01);
            if (phase != _phase
                && LauncherProgressRules.ShouldShowProgressDetails(_phase)
                && LauncherProgressRules.ShouldShowProgressDetails(phase))
            {
                await CatchUpBarAsync(1f);
                if (this == null)
                {
                    return;
                }

                _visualBar01 = 0f;
                _targetBar01 = 0f;
                ApplyVisualBarWidth();
            }

            ApplyProgress(phase, bar01);
            SetUpdateRange(GameLocalVersion.Current, _applyRemoteVersion);
        }

        private async UniTask CatchUpBarAsync(float to)
        {
            _targetBar01 = Mathf.Clamp01(to);
            if (_progressFill == null)
            {
                _visualBar01 = _targetBar01;
                return;
            }

            _barCatchingUp = true;
            try
            {
                while (this != null && !LauncherProgressBarRules.HasCaughtUp(_visualBar01, _targetBar01))
                {
                    var dt = Time.unscaledDeltaTime;
                    if (dt <= 0f)
                    {
                        dt = 0.016f;
                    }

                    _visualBar01 = LauncherProgressBarRules.StepDisplayed(_visualBar01, _targetBar01, dt);
                    ApplyVisualBarWidth();
                    await UniTask.Yield();
                }

                if (this == null)
                {
                    return;
                }

                _visualBar01 = _targetBar01;
                ApplyVisualBarWidth();
            }
            finally
            {
                _barCatchingUp = false;
            }
        }

        private void EnsureBarTicker()
        {
            if (_barSchedule != null || _progressFill == null)
            {
                return;
            }

            _barSchedule = _progressFill.schedule.Execute(TickVisualBar).Every(16);
        }

        private void StopBarTicker()
        {
            _barSchedule?.Pause();
            _barSchedule = null;
        }

        private void TickVisualBar()
        {
            if (_barCatchingUp)
            {
                return;
            }

            _visualBar01 = LauncherProgressBarRules.StepDisplayed(_visualBar01, _targetBar01, 0.016f);
            ApplyVisualBarWidth();
        }

        private void ApplyVisualBarWidth()
        {
            if (_progressFill != null)
            {
                _progressFill.style.width = Length.Percent(_visualBar01 * 100f);
            }

            if (_progressPercentLabel != null && LauncherProgressRules.ShouldShowProgressDetails(_phase))
            {
                _progressPercentLabel.text = GameUpdateUiRules.ProgressPercent(_visualBar01) + "%";
            }
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
            ApplyProgress(LauncherProgressPhase.Downloading, 0f);
            SetUpdateRange(GameLocalVersion.Current, remote);
            if (_playButton != null)
            {
                _playButton.SetEnabled(false);
                _playButton.text = "ОБНОВЛЕНИЕ…";
            }

            try
            {
                var progress = new Progress<GameUpdateApplyProgress>(OnApplyProgress);
                await GameUpdateService.DownloadAndPrepareAsync(progress);
                if (this == null)
                {
                    return;
                }

                await _barVisualChain;
                if (this == null)
                {
                    return;
                }

                await CatchUpBarAsync(1f);
                if (this == null)
                {
                    return;
                }

                ShowReadyToRestart(GameLocalVersion.Current, remote);
            }
            catch (Exception ex)
            {
                _isUpdating = false;
                _isReadyToRestart = false;
                ShowUpdateAvailable(GameLocalVersion.Current, remote, "Ошибка обновления: " + ex.Message);
            }
        }

        private void RestartPreparedUpdate()
        {
            if (!_isReadyToRestart || !GameUpdateService.HasPendingRestart)
            {
                ShowUpdateAvailable(
                    GameLocalVersion.Current,
                    _applyRemoteVersion,
                    "Подготовленное обновление не найдено. Скачайте снова.");
                return;
            }

            try
            {
                if (_playButton != null)
                {
                    _playButton.SetEnabled(false);
                }

                _warmingDetail = "Перезапуск клиента";
                ApplyProgress(LauncherProgressPhase.Warming, 1f);
                GameUpdateService.ApplyPendingRestartAndQuit();
            }
            catch (Exception ex)
            {
                ShowUpdateAvailable(GameLocalVersion.Current, _applyRemoteVersion, "Ошибка перезапуска: " + ex.Message);
                if (_playButton != null)
                {
                    _playButton.SetEnabled(true);
                }
            }
        }

        private void SetUpdateRange(string localVersion, string remoteVersion)
        {
            _rangeLocalVersion = localVersion;
            _rangeRemoteVersion = remoteVersion;

            if (_progressStatusLabel != null && _phase == LauncherProgressPhase.UpdateAvailable)
            {
                _progressStatusLabel.text = LauncherProgressRules.StatusLabel(
                    LauncherProgressPhase.UpdateAvailable,
                    localVersion: _rangeLocalVersion,
                    remoteVersion: _rangeRemoteVersion);
            }
        }

        private void SetErrorText(string errorText)
        {
            if (_progressErrorLabel == null)
            {
                return;
            }

            var hasError = !string.IsNullOrWhiteSpace(errorText);
            _progressErrorLabel.text = hasError ? errorText : string.Empty;
            _progressErrorLabel.EnableInClassList(ProgressErrorHiddenClass, !hasError);
        }

        private void BindNewsCards()
        {
            if (_newsList == null)
            {
                return;
            }

            _newsList.Clear();
            var items = new[]
            {
                ("Новый лаунчер и быстрый вход", "Bootstrap показывает новости, чат и запуск в одном окне.", "28 ИЮЛ 2026", 0),
                ("Сетевой матч стал стабильнее", "Улучшена синхронизация юнитов после кратких лагов.", "25 ИЮЛ 2026", 1),
                ("Сезонный баланс рас", "Подкрутили стоимость ключевых юнитов в ранней фазе.", "22 ИЮЛ 2026", 2),
                ("Лента анонсов", "В центре лаунчера — карточки патчей и событий команды.", "18 ИЮЛ 2026", 0),
            };

            for (var i = 0; i < items.Length; i++)
            {
                var (title, desc, date, imageIndex) = items[i];
                _newsList.Add(CreateNewsCard(title, desc, date, imageIndex));
            }
        }

        private static VisualElement CreateNewsCard(string title, string description, string date, int imageIndex)
        {
            var card = new VisualElement { name = "NewsCard", pickingMode = PickingMode.Position };
            card.AddToClassList("ln-news-card");

            var image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.AddToClassList("ln-news-card__image");
            image.AddToClassList(NewsImageClasses[Mathf.Clamp(imageIndex, 0, NewsImageClasses.Length - 1)]);
            card.Add(image);

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("ln-news-card__body");

            var dateLabel = new Label(date) { pickingMode = PickingMode.Ignore };
            dateLabel.AddToClassList("ln-news-card__date");
            body.Add(dateLabel);

            var titleLabel = new Label(title) { pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList("ln-news-card__title");
            body.Add(titleLabel);

            var descLabel = new Label(description) { pickingMode = PickingMode.Ignore };
            descLabel.AddToClassList("ln-news-card__desc");
            body.Add(descLabel);

            card.Add(body);
            return card;
        }

        private void RegisterCallbacks(bool register)
        {
            if (register)
            {
                if (_playButton != null) _playButton.clicked += OnPrimaryCtaClicked;
                if (_returnToMatchButton != null) _returnToMatchButton.clicked += OnReturnToMatchClicked;
                if (_heroReadMoreButton != null) _heroReadMoreButton.clicked += OnReadMoreClicked;
                _menuChat?.RegisterCallbacks(true);
            }
            else
            {
                if (_playButton != null) _playButton.clicked -= OnPrimaryCtaClicked;
                if (_returnToMatchButton != null) _returnToMatchButton.clicked -= OnReturnToMatchClicked;
                if (_heroReadMoreButton != null) _heroReadMoreButton.clicked -= OnReadMoreClicked;
                _menuChat?.RegisterCallbacks(false);
            }
        }

        private void OnPrimaryCtaClicked()
        {
            if (_isEntering)
            {
                return;
            }

            switch (_phase)
            {
                case LauncherProgressPhase.UpdateAvailable:
                    ApplyUpdateAsync().Forget();
                    break;
                case LauncherProgressPhase.ReadyToRestart:
                    RestartPreparedUpdate();
                    break;
                case LauncherProgressPhase.Ready:
                    EnterMainMenuAsync().Forget();
                    break;
            }
        }

        private async UniTaskVoid EnterMainMenuAsync()
        {
            _isEntering = true;
            if (_playButton != null)
            {
                _playButton.SetEnabled(false);
            }

            _warmingDetail = "Вход в игру…";
            ApplyProgress(LauncherProgressPhase.Warming, 1f);

            try
            {
                await SceneManager.LoadSceneAsync(GameSceneNames.MainMenu).ToUniTask();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Launcher] Failed to load MainMenu: {ex.Message}");
                _isEntering = false;
                ShowReadyToEnter();
            }
        }

        private void StartShineAnimation()
        {
            if (_progressShine == null || !LauncherProgressRules.ShouldShowProgressDetails(_phase))
            {
                return;
            }

            if (_shineSchedule != null)
            {
                return;
            }

            _shineElapsed = 0f;
            _shineSchedule = _progressShine.schedule.Execute(TickShine).Every(16);
        }

        private void StopShineAnimation()
        {
            _shineSchedule?.Pause();
            _shineSchedule = null;
            if (_progressShine != null)
            {
                _progressShine.style.translate = new Translate(0, 0);
            }
        }

        private void TickShine()
        {
            if (_progressShine == null || _progressFill == null)
            {
                return;
            }

            var track = _progressFill.parent;
            var trackWidth = track != null ? track.resolvedStyle.width : 0f;
            if (trackWidth <= 1f)
            {
                return;
            }

            _shineElapsed += 0.016f;
            var t = (_shineElapsed % ShinePeriodSeconds) / ShinePeriodSeconds;
            var shineWidth = Mathf.Max(24f, _progressShine.resolvedStyle.width);
            var x = Mathf.Lerp(-shineWidth, trackWidth, t);
            _progressShine.style.translate = new Translate(x, 0);
        }

        private void OnReadMoreClicked() => PlaytestLog.Info("Launcher", "ReadMorePlaceholder");
    }
}
