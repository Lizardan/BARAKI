namespace Game.Core
{
    /// <summary>Display policy: windowed bootstrap, fullscreen from Main Menu.</summary>
    public static class GameDisplayRules
    {
        public const int StartupWidth = 1280;
        public const int StartupHeight = 720;

        public static bool RunInBackground => true;

        public static UnityEngine.FullScreenMode StartupFullScreenMode =>
            UnityEngine.FullScreenMode.Windowed;

        public static UnityEngine.FullScreenMode MainMenuFullScreenMode =>
            UnityEngine.FullScreenMode.FullScreenWindow;

        public static bool ShouldEnterFullscreen(string sceneName) =>
            sceneName == GameSceneNames.MainMenu;
    }
}
