using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Applies startup windowed resolution (borderless chrome on Windows) and Main Menu fullscreen.
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
            ApplyCursorPolicy(scene.name);

            if (GameDisplayRules.ShouldUseBorderlessChrome(scene.name))
            {
#if !UNITY_EDITOR
                // SetResolution applies end-of-frame; refresh chrome after Unity settles.
                FinalizeStartupWindowAsync().Forget();
#endif
                return;
            }

            if (!GameDisplayRules.ShouldEnterFullscreen(scene.name))
            {
                return;
            }

            ApplyMainMenuFullscreen();
        }

        /// <summary>
        /// Pins the OS mouse inside the player window (<see cref="CursorLockMode.Confined"/>).
        /// This is not camera pan logic — the cursor physically cannot leave the window.
        /// </summary>
        static void ApplyCursorPolicy(string sceneName)
        {
            Cursor.visible = true;
            Cursor.lockState = GameDisplayRules.ResolveCursorLockMode(sceneName);
        }

        /// <summary>1280×720 windowed, centered — used during Bootstrap and on quit.</summary>
        public static void ApplyStartupWindow()
        {
#if !UNITY_EDITOR
            Screen.SetResolution(
                GameDisplayRules.StartupWidth,
                GameDisplayRules.StartupHeight,
                GameDisplayRules.StartupFullScreenMode);
            FinalizeStartupWindowAsync().Forget();
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

        static async UniTaskVoid FinalizeStartupWindowAsync()
        {
#if UNITY_EDITOR
            await UniTask.CompletedTask;
#else
            // Resolution changes apply at end of frame; wait before touching Win32 chrome.
            await UniTask.DelayFrame(1);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            GameNativeWindowChrome.TryApplyBorderless(
                GameDisplayRules.StartupWidth,
                GameDisplayRules.StartupHeight);
            CenterStartupWindow();
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
