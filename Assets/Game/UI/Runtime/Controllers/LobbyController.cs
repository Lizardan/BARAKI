using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Bindings;
using Game.UI.ViewModels;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LobbyController : MonoBehaviour
    {
        private const float FadeDuration = 0.35f;

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        private VisualElement _lobbyScreen;
        private LobbyViewModel _viewModel;
        private UIBindingScope _bindingScope;
        private bool _isTransitioning;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            _viewModel = new LobbyViewModel();
            _root = _uiDocument.rootVisualElement;
            _lobbyScreen = _root.Q<VisualElement>("LobbyScreen");

            var startButton = _root.Q<Button>("StartButton");
            var backButton = _root.Q<Button>("BackButton");

            _bindingScope = new UIBindingScope(_root);
            _bindingScope.Add(_viewModel.StartMatchCommand.BindTo(startButton));
            _bindingScope.Add(_viewModel.BackCommand.BindTo(backButton));
            _bindingScope.Add(_viewModel.StartMatchCommand.Subscribe(_ => OnStartMatch()));
            _bindingScope.Add(_viewModel.BackCommand.Subscribe(_ => OnBack()));
        }

        private void OnEnable()
        {
            _root?.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnDestroy()
        {
            _bindingScope?.Dispose();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_isTransitioning)
            {
                return;
            }

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Space)
            {
                evt.StopPropagation();
                OnStartMatch();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                OnBack();
            }
        }

        private void OnStartMatch()
        {
            if (_isTransitioning)
            {
                return;
            }

            StartMatchAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnBack()
        {
            if (_isTransitioning)
            {
                return;
            }

            LoadSceneAsync(GameSceneNames.MainMenu, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask StartMatchAsync(System.Threading.CancellationToken cancellationToken)
        {
            _isTransitioning = true;
            await FadeOutAsync(cancellationToken);
            GameSession.Begin(MatchSetup.Default);
            await SceneManager.LoadSceneAsync(GameSceneNames.Game).ToUniTask(cancellationToken: cancellationToken);
            _isTransitioning = false;
        }

        private async UniTask LoadSceneAsync(string sceneName, System.Threading.CancellationToken cancellationToken)
        {
            _isTransitioning = true;
            await FadeOutAsync(cancellationToken);
            await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: cancellationToken);
            _isTransitioning = false;
        }

        private async UniTask FadeOutAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (_lobbyScreen == null)
            {
                return;
            }

            _lobbyScreen.AddToClassList("lobby--hidden");
            var elapsed = 0f;
            _lobbyScreen.style.opacity = 1f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.deltaTime;
                _lobbyScreen.style.opacity = 1f - Mathf.Clamp01(elapsed / FadeDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
