using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Display policy: borderless windowed bootstrap, fullscreen from Main Menu.</summary>
    public static class GameDisplayRules
    {
        public const int StartupWidth = 1280;
        public const int StartupHeight = 720;

        /// <summary>Unity PlayerPrefs key for last window width (standalone).</summary>
        public const string ScreenWidthPrefsKey = "Screenmanager Resolution Width";

        /// <summary>Unity PlayerPrefs key for last window height (standalone).</summary>
        public const string ScreenHeightPrefsKey = "Screenmanager Resolution Height";

        /// <summary>Unity PlayerPrefs key for last <see cref="FullScreenMode"/>.</summary>
        public const string ScreenFullscreenModePrefsKey = "Screenmanager Fullscreen mode";

        /// <summary>Unity PlayerPrefs key for last window X (standalone).</summary>
        public const string ScreenWindowPositionXPrefsKey = "Screenmanager Window Position X";

        /// <summary>Unity PlayerPrefs key for last window Y (standalone).</summary>
        public const string ScreenWindowPositionYPrefsKey = "Screenmanager Window Position Y";

        public static bool RunInBackground => true;

        /// <summary>
        /// Unity mode for Bootstrap: still <see cref="FullScreenMode.Windowed"/>.
        /// OS title bar is stripped separately via <see cref="GameNativeWindowChrome"/>.
        /// </summary>
        public static FullScreenMode StartupFullScreenMode => FullScreenMode.Windowed;

        public static FullScreenMode MainMenuFullScreenMode => FullScreenMode.FullScreenWindow;

        public static int StartupFullscreenModePrefsValue => (int)StartupFullScreenMode;

        public static bool ShouldEnterFullscreen(string sceneName) =>
            sceneName == GameSceneNames.MainMenu;

        /// <summary>Bootstrap launcher: 1280×720 without OS window frame.</summary>
        public static bool ShouldUseBorderlessChrome(string sceneName) =>
            sceneName == GameSceneNames.Bootstrap;

        public static bool ShouldConfineCursor(string sceneName) =>
            sceneName != GameSceneNames.Bootstrap;

        /// <summary>
        /// Hard OS confine to the game window (not camera edge logic).
        /// Bootstrap keeps free cursor (launcher UI / eventual window drag).
        /// </summary>
        public static CursorLockMode ResolveCursorLockMode(string sceneName) =>
            ShouldConfineCursor(sceneName) ? CursorLockMode.Confined : CursorLockMode.None;

        /// <summary>
        /// Centers a window of the given size inside the display work area (taskbar-aware).
        /// </summary>
        public static Vector2Int GetCenteredWindowPosition(
            RectInt workArea,
            int windowWidth,
            int windowHeight)
        {
            var x = workArea.x + Math.Max(0, (workArea.width - windowWidth) / 2);
            var y = workArea.y + Math.Max(0, (workArea.height - windowHeight) / 2);
            return new Vector2Int(x, y);
        }
    }
}
