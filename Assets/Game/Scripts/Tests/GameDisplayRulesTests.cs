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
    }
}
