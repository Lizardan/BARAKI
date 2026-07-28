using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameUpdateApplyProgressRulesTests
    {
        [Test]
        public void MapBarProgress_DownloadIsFullRange()
        {
            Assert.AreEqual(0f, GameUpdateApplyProgressRules.MapBarProgress(GameUpdateApplyPhase.Downloading, 0f), 0.0001f);
            Assert.AreEqual(0.5f, GameUpdateApplyProgressRules.MapBarProgress(GameUpdateApplyPhase.Downloading, 0.5f), 0.0001f);
            Assert.AreEqual(1f, GameUpdateApplyProgressRules.MapBarProgress(GameUpdateApplyPhase.Downloading, 1f), 0.0001f);
        }

        [Test]
        public void MapBarProgress_InstallResetsToFullRange()
        {
            Assert.AreEqual(0f, GameUpdateApplyProgressRules.MapBarProgress(GameUpdateApplyPhase.Installing, 0f), 0.0001f);
            Assert.AreEqual(0.55f, GameUpdateApplyProgressRules.MapBarProgress(GameUpdateApplyPhase.Installing, 0.55f), 0.0001f);
            Assert.AreEqual(1f, GameUpdateApplyProgressRules.MapBarProgress(GameUpdateApplyPhase.Installing, 1f), 0.0001f);
        }

        [Test]
        public void MapBarProgress_ReadyToRestartIsFull()
        {
            Assert.AreEqual(1f, GameUpdateApplyProgressRules.MapBarProgress(GameUpdateApplyPhase.ReadyToRestart, 0f), 0.0001f);
        }

        [Test]
        public void FormatSideStatus_MatchesPhase()
        {
            Assert.AreEqual(
                "Загрузка v0.1.4",
                GameUpdateApplyProgressRules.FormatSideStatus(GameUpdateApplyPhase.Downloading, "0.1.4"));
            Assert.AreEqual(
                "Установка обновления",
                GameUpdateApplyProgressRules.FormatSideStatus(GameUpdateApplyPhase.Installing, "0.1.4"));
            Assert.AreEqual(
                "Готово к перезапуску",
                GameUpdateApplyProgressRules.FormatSideStatus(GameUpdateApplyPhase.ReadyToRestart, "0.1.4"));
        }

        [Test]
        public void FormatProgressLabel_ShowsPercentDuringPhases()
        {
            Assert.AreEqual(
                "42%",
                GameUpdateApplyProgressRules.FormatProgressLabel(GameUpdateApplyPhase.Downloading, 0.42f));
            Assert.AreEqual(
                "100%",
                GameUpdateApplyProgressRules.FormatProgressLabel(GameUpdateApplyPhase.Installing, 1f));
            Assert.AreEqual(
                GameUpdateUiRules.RestartButtonLabel,
                GameUpdateApplyProgressRules.FormatProgressLabel(GameUpdateApplyPhase.ReadyToRestart, 1f));
        }
    }
}
