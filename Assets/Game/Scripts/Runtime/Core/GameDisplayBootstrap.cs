using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Applies startup windowed resolution and Main Menu fullscreen.
    /// Keeps the player running when the window loses focus.
    /// </summary>
    public static class GameDisplayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Application.runInBackground = GameDisplayRules.RunInBackground;
            ApplyStartupWindow();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!GameDisplayRules.ShouldEnterFullscreen(scene.name))
            {
                return;
            }

            ApplyMainMenuFullscreen();
        }

        /// <summary>1280×720 windowed — used during Bootstrap.</summary>
        public static void ApplyStartupWindow()
        {
#if !UNITY_EDITOR
            Screen.SetResolution(
                GameDisplayRules.StartupWidth,
                GameDisplayRules.StartupHeight,
                GameDisplayRules.StartupFullScreenMode);
#endif
        }

        /// <summary>Fullscreen Window when entering Main Menu.</summary>
        public static void ApplyMainMenuFullscreen()
        {
#if !UNITY_EDITOR
            var desktop = Screen.currentResolution;
            Screen.SetResolution(
                desktop.width,
                desktop.height,
                GameDisplayRules.MainMenuFullScreenMode);
#endif
        }
    }
}
