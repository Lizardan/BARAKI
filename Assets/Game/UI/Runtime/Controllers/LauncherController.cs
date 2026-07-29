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
        [SerializeField] private string _previewLocalVersion = "0.1.2";
        [SerializeField] private string _previewRemoteVersion = "0.1.4";
        [SerializeField] private string _previewWarmingDetail = "Авторизация";
        [SerializeField] private string _previewError = "";
#endif

        private VisualElement _root;
        private Button _playButton;
        private Button _heroReadMoreButton;
        private Button _chatSendButton;
        private TextField _chatInput;
        private VisualElement _newsList;
        private VisualElement _chatMessages;
        private Label _clientVersionLabel;
        private Label _progressStatusLabel;
        private Label _progressPercentLabel;
        private VisualElement _progressDetails;
        private VisualElement _progressFill;
        private VisualElement _progressShine;
        private Label _progressErrorLabel;
        private LauncherProgressPhase _phase = LauncherProgressPhase.Ready;
        private IVisualElementScheduledItem _shineSchedule;
        private float _shineElapsed;
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
            _heroReadMoreButton = _root.Q<Button>("HeroReadMoreButton");
            _chatSendButton = _root.Q<Button>("ChatSendButton");
            _chatInput = _root.Q<TextField>("ChatInput");
            _newsList = _root.Q<VisualElement>("NewsList");
            _chatMessages = _root.Q<VisualElement>("ChatMessages");
            _clientVersionLabel = _root.Q<Label>("ClientVersionLabel");
            _progressStatusLabel = _root.Q<Label>("ProgressStatusLabel");
            _progressPercentLabel = _root.Q<Label>("ProgressPercentLabel");
            _progressDetails = _root.Q<VisualElement>("ProgressDetails");
            _progressFill = _root.Q<VisualElement>("ProgressFill");
            _progressShine = _root.Q<VisualElement>("ProgressShine");
            _progressErrorLabel = _root.Q<Label>("ProgressErrorLabel");

            if (_chatInput != null && _chatInput.textEdition != null)
            {
                _chatInput.value = string.Empty;
                _chatInput.textEdition.placeholder = "Написать в чат…";
            }
        }

        private void PopulatePlaceholders()
        {
            if (_clientVersionLabel != null)
            {
                _clientVersionLabel.text = GameUpdateUiRules.FormatVersionLabel(GameLocalVersion.Current);
            }

            BindNewsCards();
            BindChatMessages();
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

            var percent = GameUpdateUiRules.ProgressPercent(progress01);
            if (_progressPercentLabel != null)
            {
                _progressPercentLabel.text = LauncherProgressRules.ShouldShowProgressDetails(phase)
                    ? percent + "%"
                    : string.Empty;
            }

            if (_progressFill != null)
            {
                _progressFill.style.width = Length.Percent(Mathf.Clamp(progress01, 0f, 1f) * 100f);
            }

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

        private void OnApplyProgress(GameUpdateApplyProgress progress)
        {
            if (progress.Phase == GameUpdateApplyPhase.ReadyToRestart)
            {
                return;
            }

            var phase = LauncherProgressRules.FromApplyPhase(progress.Phase);
            var bar01 = GameUpdateApplyProgressRules.MapBarProgress(progress.Phase, progress.Phase01);
            ApplyProgress(phase, bar01);
            SetUpdateRange(GameLocalVersion.Current, _applyRemoteVersion);
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
            }

            try
            {
                var progress = new Progress<GameUpdateApplyProgress>(OnApplyProgress);
                await GameUpdateService.DownloadAndPrepareAsync(progress);
                if (this == null)
                {
                    return;
                }

                _isUpdating = false;
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

            var titleLabel = new Label(title) { pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList("ln-news-card__title");
            body.Add(titleLabel);

            var descLabel = new Label(description) { pickingMode = PickingMode.Ignore };
            descLabel.AddToClassList("ln-news-card__desc");
            body.Add(descLabel);

            var dateLabel = new Label(date) { pickingMode = PickingMode.Ignore };
            dateLabel.AddToClassList("ln-news-card__date");
            body.Add(dateLabel);

            card.Add(body);
            return card;
        }

        private void BindChatMessages()
        {
            if (_chatMessages == null)
            {
                return;
            }

            _chatMessages.Clear();
        }

        private static VisualElement CreateChatMessage(string nick, string time, string text)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ln-chat-message");

            var avatar = new VisualElement { pickingMode = PickingMode.Ignore };
            avatar.AddToClassList("ln-chat-message__avatar");
            var avatarLabel = new Label(string.IsNullOrEmpty(nick) ? "?" : nick.Substring(0, 1).ToUpperInvariant())
            {
                pickingMode = PickingMode.Ignore,
            };
            avatarLabel.AddToClassList("ln-chat-message__avatar-text");
            avatar.Add(avatarLabel);
            row.Add(avatar);

            var content = new VisualElement { pickingMode = PickingMode.Ignore };
            content.AddToClassList("ln-chat-message__content");

            var meta = new VisualElement { pickingMode = PickingMode.Ignore };
            meta.AddToClassList("ln-chat-message__meta");

            var nickLabel = new Label(nick) { pickingMode = PickingMode.Ignore };
            nickLabel.AddToClassList("ln-chat-message__nick");
            meta.Add(nickLabel);

            var timeLabel = new Label(time) { pickingMode = PickingMode.Ignore };
            timeLabel.AddToClassList("ln-chat-message__time");
            meta.Add(timeLabel);

            content.Add(meta);

            var textLabel = new Label(text) { pickingMode = PickingMode.Ignore };
            textLabel.AddToClassList("ln-chat-message__text");
            content.Add(textLabel);

            row.Add(content);
            return row;
        }

        private void RegisterCallbacks(bool register)
        {
            if (register)
            {
                if (_playButton != null) _playButton.clicked += OnPrimaryCtaClicked;
                if (_heroReadMoreButton != null) _heroReadMoreButton.clicked += OnReadMoreClicked;
                if (_chatSendButton != null) _chatSendButton.clicked += OnChatSendClicked;
                if (_chatInput != null) _chatInput.RegisterCallback<KeyDownEvent>(OnChatInputKeyDown);
            }
            else
            {
                if (_playButton != null) _playButton.clicked -= OnPrimaryCtaClicked;
                if (_heroReadMoreButton != null) _heroReadMoreButton.clicked -= OnReadMoreClicked;
                if (_chatSendButton != null) _chatSendButton.clicked -= OnChatSendClicked;
                if (_chatInput != null) _chatInput.UnregisterCallback<KeyDownEvent>(OnChatInputKeyDown);
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

        private void OnReadMoreClicked() => Debug.Log("[Launcher] Read More (placeholder)");

        private void OnChatSendClicked() => TrySendChatMessage();

        private void OnChatInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                TrySendChatMessage();
                evt.StopPropagation();
            }
        }

        private void TrySendChatMessage()
        {
            if (_chatInput == null || _chatMessages == null)
            {
                return;
            }

            var text = _chatInput.value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var time = DateTime.Now.ToString("HH:mm");
            _chatMessages.Add(CreateChatMessage("Вы", time, text));
            _chatInput.value = string.Empty;

            var scroll = _root?.Q<ScrollView>("ChatScroll");
            scroll?.ScrollTo(_chatMessages[_chatMessages.childCount - 1]);
        }
    }
}
