using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameDisplayRulesTests
    {
        [Test]
        public void Startup_IsWindowed_1280x720()
        {
            Assert.AreEqual(1280, GameDisplayRules.StartupWidth);
            Assert.AreEqual(720, GameDisplayRules.StartupHeight);
            Assert.AreEqual(FullScreenMode.Windowed, GameDisplayRules.StartupFullScreenMode);
        }

        [Test]
        public void MainMenu_UsesFullScreenWindow()
        {
            Assert.AreEqual(
                FullScreenMode.FullScreenWindow,
                GameDisplayRules.MainMenuFullScreenMode);
        }

        [Test]
        public void ShouldEnterFullscreen_OnlyOnMainMenu()
        {
            Assert.IsTrue(GameDisplayRules.ShouldEnterFullscreen(GameSceneNames.MainMenu));
            Assert.IsFalse(GameDisplayRules.ShouldEnterFullscreen(GameSceneNames.Bootstrap));
            Assert.IsFalse(GameDisplayRules.ShouldEnterFullscreen(GameSceneNames.Lobby));
            Assert.IsFalse(GameDisplayRules.ShouldEnterFullscreen(GameSceneNames.Game));
        }

        [Test]
        public void ShouldUseBorderlessChrome_OnlyOnBootstrap()
        {
            Assert.IsTrue(GameDisplayRules.ShouldUseBorderlessChrome(GameSceneNames.Bootstrap));
            Assert.IsFalse(GameDisplayRules.ShouldUseBorderlessChrome(GameSceneNames.MainMenu));
            Assert.IsFalse(GameDisplayRules.ShouldUseBorderlessChrome(GameSceneNames.Lobby));
            Assert.IsFalse(GameDisplayRules.ShouldUseBorderlessChrome(GameSceneNames.Game));
        }

        [Test]
        public void ShouldConfineCursor_EverywhereExceptBootstrap()
        {
            Assert.IsFalse(GameDisplayRules.ShouldConfineCursor(GameSceneNames.Bootstrap));
            Assert.IsTrue(GameDisplayRules.ShouldConfineCursor(GameSceneNames.MainMenu));
            Assert.IsTrue(GameDisplayRules.ShouldConfineCursor(GameSceneNames.Lobby));
            Assert.IsTrue(GameDisplayRules.ShouldConfineCursor(GameSceneNames.Game));
        }

        [Test]
        public void ResolveCursorLockMode_ConfinesOutsideBootstrap()
        {
            Assert.AreEqual(CursorLockMode.None, GameDisplayRules.ResolveCursorLockMode(GameSceneNames.Bootstrap));
            Assert.AreEqual(CursorLockMode.Confined, GameDisplayRules.ResolveCursorLockMode(GameSceneNames.MainMenu));
            Assert.AreEqual(CursorLockMode.Confined, GameDisplayRules.ResolveCursorLockMode(GameSceneNames.Lobby));
            Assert.AreEqual(CursorLockMode.Confined, GameDisplayRules.ResolveCursorLockMode(GameSceneNames.Game));
        }

        [Test]
        public void RunInBackground_IsRequired()
        {
            Assert.IsTrue(GameDisplayRules.RunInBackground);
        }

        [Test]
        public void StartupWindowPlayerPrefs_MatchWindowedResolution()
        {
            Assert.AreEqual("Screenmanager Resolution Width", GameDisplayRules.ScreenWidthPrefsKey);
            Assert.AreEqual("Screenmanager Resolution Height", GameDisplayRules.ScreenHeightPrefsKey);
            Assert.AreEqual(
                "Screenmanager Fullscreen mode",
                GameDisplayRules.ScreenFullscreenModePrefsKey);
            Assert.AreEqual(
                "Screenmanager Window Position X",
                GameDisplayRules.ScreenWindowPositionXPrefsKey);
            Assert.AreEqual(
                "Screenmanager Window Position Y",
                GameDisplayRules.ScreenWindowPositionYPrefsKey);
            Assert.AreEqual(
                (int)FullScreenMode.Windowed,
                GameDisplayRules.StartupFullscreenModePrefsValue);
        }

        [Test]
        public void UnityWindowClassName_IsStandalonePlayerClass()
        {
            Assert.AreEqual("UnityWndClass", GameNativeWindowChrome.UnityWindowClassName);
        }

        [Test]
        public void ToBorderlessStyle_KeepsMinimizeBoxForTaskbarToggle()
        {
            const uint captionedOverlapped = 0x16CF0000;
            var style = GameNativeWindowChrome.ToBorderlessStyle(captionedOverlapped);
            Assert.AreEqual(0u, style & GameNativeWindowChrome.WsCaption);
            Assert.AreEqual(0u, style & GameNativeWindowChrome.WsThickFrame);
            Assert.AreEqual(0u, style & GameNativeWindowChrome.WsMaximizeBox);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsMinimizeBox);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsSysMenu);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsPopup);
        }

        [Test]
        public void ToBorderlessStyle_RestoresMinimizeBoxIfStripped()
        {
            var stripped = GameNativeWindowChrome.WsPopup
                           | GameNativeWindowChrome.WsVisible
                           | GameNativeWindowChrome.WsClipSiblings
                           | GameNativeWindowChrome.WsClipChildren;
            var style = GameNativeWindowChrome.ToBorderlessStyle(stripped);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsMinimizeBox);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsSysMenu);
        }

        [Test]
        public void EnsureTaskbarMinimizeStyle_DoesNotStripCaption()
        {
            const uint captioned = GameNativeWindowChrome.WsCaption | GameNativeWindowChrome.WsVisible;
            var style = GameNativeWindowChrome.EnsureTaskbarMinimizeStyle(captioned);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsCaption);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsMinimizeBox);
            Assert.AreNotEqual(0u, style & GameNativeWindowChrome.WsSysMenu);
        }

        [Test]
        public void ToTaskbarAppExStyle_ForcesAppWindow()
        {
            var exStyle = GameNativeWindowChrome.ToTaskbarAppExStyle(GameNativeWindowChrome.WsExToolWindow);
            Assert.AreEqual(0u, exStyle & GameNativeWindowChrome.WsExToolWindow);
            Assert.AreNotEqual(0u, exStyle & GameNativeWindowChrome.WsExAppWindow);
        }

        [Test]
        public void TryEnableTaskbarMinimize_IsNoOpInEditor()
        {
            Assert.IsFalse(GameNativeWindowChrome.TryEnableTaskbarMinimize());
        }

        [Test]
        public void GetCenteredWindowPosition_CentersInsideWorkArea()
        {
            var workArea = new RectInt(0, 0, 1920, 1080);
            var position = GameDisplayRules.GetCenteredWindowPosition(workArea, 1280, 720);
            Assert.AreEqual(new Vector2Int(320, 180), position);
        }

        [Test]
        public void GetCenteredWindowPosition_UsesWorkAreaOrigin()
        {
            var workArea = new RectInt(100, 50, 1600, 900);
            var position = GameDisplayRules.GetCenteredWindowPosition(workArea, 1280, 720);
            Assert.AreEqual(new Vector2Int(260, 140), position);
        }

        [Test]
        public void MatchesStartupResolution_OnlyExactWindowed1280x720()
        {
            Assert.IsTrue(
                GameDisplayRules.MatchesStartupResolution(1280, 720, FullScreenMode.Windowed));
            Assert.IsFalse(
                GameDisplayRules.MatchesStartupResolution(1920, 1080, FullScreenMode.Windowed));
            Assert.IsFalse(
                GameDisplayRules.MatchesStartupResolution(
                    1280,
                    720,
                    FullScreenMode.FullScreenWindow));
        }

        [Test]
        public void MatchesWindowPosition_RequiresExactPoint()
        {
            Assert.IsTrue(
                GameDisplayRules.MatchesWindowPosition(new Vector2Int(320, 180), new Vector2Int(320, 180)));
            Assert.IsFalse(
                GameDisplayRules.MatchesWindowPosition(new Vector2Int(320, 180), new Vector2Int(320, 181)));
        }
    }
}
