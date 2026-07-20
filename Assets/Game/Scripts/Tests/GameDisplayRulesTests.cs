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
    }
}
