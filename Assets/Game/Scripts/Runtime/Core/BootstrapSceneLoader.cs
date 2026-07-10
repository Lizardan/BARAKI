using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Loads MainMenu after bootstrap systems are ready.
    /// </summary>
    public class BootstrapSceneLoader : MonoBehaviour
    {
        private void OnEnable()
        {
            GameManager.OnInitialized += OnGameManagerInitialized;

            if (GameManager.Instance != null && GameManager.Instance.IsInitialized)
            {
                OnGameManagerInitialized();
            }
        }

        private void OnDisable()
        {
            GameManager.OnInitialized -= OnGameManagerInitialized;
        }

        private async void OnGameManagerInitialized()
        {
            await Awaitable.NextFrameAsync();

            var active = SceneManager.GetActiveScene().name;
            if (active is GameSceneNames.MainMenu or GameSceneNames.Lobby or GameSceneNames.Game)
            {
                return;
            }

            await SceneManager.LoadSceneAsync(GameSceneNames.MainMenu);
        }
    }
}
