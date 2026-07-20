using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Applies startup windowed resolution and Main Menu fullscreen.
    /// Keeps the player running when the window loses focus.
    /// Persists windowed prefs on quit so the next cold start is not Fullscreen Window.
    /// Centers the windowed client after FullScreenWindow would leave it at top-left.
    /// </summary>
    public static class GameDisplayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            // Earliest managed hook: stamp windowed boot prefs before most systems run.
            PersistStartupWindowPreferences();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Application.runInBackground = GameDisplayRules.RunInBackground;
            PersistStartupWindowPreferences();
            ApplyStartupWindow();

            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnQuitting()
        {
            // Main Menu leaves FullScreenWindow in PlayerPrefs — rewrite to windowed
            // so the next launch opens as 1280×720 instead of borderless fullscreen.
            ApplyStartupWindow();
            PersistStartupWindowPreferences();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!GameDisplayRules.ShouldEnterFullscreen(scene.name))
            {
                return;
            }

            ApplyMainMenuFullscreen();
        }

        /// <summary>1280×720 windowed, centered — used during Bootstrap and on quit.</summary>
        public static void ApplyStartupWindow()
        {
#if !UNITY_EDITOR
            Screen.SetResolution(
                GameDisplayRules.StartupWidth,
                GameDisplayRules.StartupHeight,
                GameDisplayRules.StartupFullScreenMode);
            CenterStartupWindow();
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

        /// <summary>
        /// Writes Unity standalone screen PlayerPrefs to the startup window policy.
        /// </summary>
        public static void PersistStartupWindowPreferences()
        {
#if !UNITY_EDITOR
            PlayerPrefs.SetInt(GameDisplayRules.ScreenWidthPrefsKey, GameDisplayRules.StartupWidth);
            PlayerPrefs.SetInt(GameDisplayRules.ScreenHeightPrefsKey, GameDisplayRules.StartupHeight);
            PlayerPrefs.SetInt(
                GameDisplayRules.ScreenFullscreenModePrefsKey,
                GameDisplayRules.StartupFullscreenModePrefsValue);

            var display = Screen.mainWindowDisplayInfo;
            if (display.width > 0 && display.height > 0)
            {
                var position = GameDisplayRules.GetCenteredWindowPosition(
                    display.workArea,
                    GameDisplayRules.StartupWidth,
                    GameDisplayRules.StartupHeight);
                PlayerPrefs.SetInt(GameDisplayRules.ScreenWindowPositionXPrefsKey, position.x);
                PlayerPrefs.SetInt(GameDisplayRules.ScreenWindowPositionYPrefsKey, position.y);
            }

            PlayerPrefs.Save();
#endif
        }

        static void CenterStartupWindow()
        {
            var display = Screen.mainWindowDisplayInfo;
            if (display.width <= 0 || display.height <= 0)
            {
                return;
            }

            var position = GameDisplayRules.GetCenteredWindowPosition(
                display.workArea,
                GameDisplayRules.StartupWidth,
                GameDisplayRules.StartupHeight);
            Screen.MoveMainWindowTo(display, position);

            PlayerPrefs.SetInt(GameDisplayRules.ScreenWindowPositionXPrefsKey, position.x);
            PlayerPrefs.SetInt(GameDisplayRules.ScreenWindowPositionYPrefsKey, position.y);
            PlayerPrefs.Save();
        }
    }
}
