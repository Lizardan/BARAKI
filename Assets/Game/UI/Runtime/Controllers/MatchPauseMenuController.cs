using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Pause menu opened with Esc. The menu itself never pauses the game —
    /// a dedicated Pause button toggles the network-wide user pause.
    /// Also hosts the camera pad (base orientation) moved off the HUD.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchPauseMenuController : MonoBehaviour
    {
        private const string PauseOverlayHiddenClass = "match-hud__pause--hidden";
        private const string ResultsHiddenClass = "match-hud__results--hidden";
        private const string PausedButtonText = "Продолжить";
        private const string ResumeButtonText = "Пауза";
        private const float TopSortingOrder = 300f;

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        private VisualElement _pauseOverlay;
        private VisualElement _resultsOverlay;
        private Button _pauseToggleButton;
        private Button _quitMatchButton;
        private Button _quitGameButton;
        private float _defaultSortingOrder;
        private bool _isOpen;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            _root = _uiDocument.rootVisualElement;
            _defaultSortingOrder = _uiDocument.sortingOrder;
            _pauseOverlay = _root.Q<VisualElement>("PauseOverlay");
            _resultsOverlay = _root.Q<VisualElement>("ResultsOverlay");
            _pauseToggleButton = _root.Q<Button>("PauseToggleButton");
            _quitMatchButton = _root.Q<Button>("QuitMatchButton");
            _quitGameButton = _root.Q<Button>("QuitGameButton");
        }

        private void OnEnable()
        {
            if (_pauseToggleButton != null)
            {
                _pauseToggleButton.clicked += OnPauseToggleClicked;
            }

            if (_quitMatchButton != null)
            {
                _quitMatchButton.clicked += OnQuitMatchClicked;
            }

            if (_quitGameButton != null)
            {
                _quitGameButton.clicked += OnQuitGameClicked;
            }

            MatchPauseGate.PausedChanged += OnPauseChanged;
            RefreshToggleButton();
        }

        private void OnDisable()
        {
            if (_pauseToggleButton != null)
            {
                _pauseToggleButton.clicked -= OnPauseToggleClicked;
            }

            if (_quitMatchButton != null)
            {
                _quitMatchButton.clicked -= OnQuitMatchClicked;
            }

            if (_quitGameButton != null)
            {
                _quitGameButton.clicked -= OnQuitGameClicked;
            }

            MatchPauseGate.PausedChanged -= OnPauseChanged;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ToggleMenu();
            }
        }

        private void ToggleMenu()
        {
            if (_isOpen)
            {
                SetMenuOpen(false);
                return;
            }

            if (MatchPauseGate.IsMigrationPaused || MatchPauseGate.IsDisconnectHoldPaused)
            {
                return;
            }

            if (_resultsOverlay != null && !_resultsOverlay.ClassListContains(ResultsHiddenClass))
            {
                return;
            }

            SetMenuOpen(true);
        }

        private void SetMenuOpen(bool open)
        {
            _isOpen = open;
            if (_pauseOverlay != null)
            {
                _pauseOverlay.EnableInClassList(PauseOverlayHiddenClass, !open);
            }

            if (_uiDocument != null)
            {
                _uiDocument.sortingOrder = open ? TopSortingOrder : _defaultSortingOrder;
            }
        }

        private void OnPauseToggleClicked()
        {
            MatchNetworkCommands.RequestSetPaused(!MatchPauseGate.IsUserPaused);
        }

        private void OnPauseChanged()
        {
            RefreshToggleButton();
        }

        private void RefreshToggleButton()
        {
            if (_pauseToggleButton != null)
            {
                _pauseToggleButton.text = MatchPauseGate.IsUserPaused ? PausedButtonText : ResumeButtonText;
            }
        }

        private void OnQuitMatchClicked()
        {
            LeaveAndLoadAsync(GameSceneNames.MainMenu).Forget();
        }

        private void OnQuitGameClicked()
        {
            MatchNetworkSession.LeaveMatch();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private async UniTaskVoid LeaveAndLoadAsync(string sceneName)
        {
            MatchNetworkSession.LeaveMatch();
            await SceneManager.LoadSceneAsync(sceneName)
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }
}
