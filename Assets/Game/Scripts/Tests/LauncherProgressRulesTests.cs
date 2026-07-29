using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LauncherProgressRulesTests
    {
        [Test]
        public void StatusLabel_MapsKnownPhases()
        {
            Assert.AreEqual("Загрузка…", LauncherProgressRules.StatusLabel(LauncherProgressPhase.Downloading));
            Assert.AreEqual("Установка…", LauncherProgressRules.StatusLabel(LauncherProgressPhase.Installing));
            Assert.AreEqual("Проверка версии…", LauncherProgressRules.StatusLabel(LauncherProgressPhase.Checking));
            Assert.AreEqual("Доступно обновление", LauncherProgressRules.StatusLabel(LauncherProgressPhase.UpdateAvailable));
            Assert.AreEqual("Готово к перезапуску", LauncherProgressRules.StatusLabel(LauncherProgressPhase.ReadyToRestart));
            Assert.AreEqual("Авторизация", LauncherProgressRules.StatusLabel(LauncherProgressPhase.Warming, "Авторизация"));
            Assert.AreEqual("Готово к запуску", LauncherProgressRules.StatusLabel(LauncherProgressPhase.Ready));
        }

        [Test]
        public void StatusLabel_UpdateAvailable_AppendsRemoteVersionInBlue()
        {
            var text = LauncherProgressRules.StatusLabel(
                LauncherProgressPhase.UpdateAvailable,
                localVersion: "0.1.2",
                remoteVersion: "0.1.4");

            Assert.AreEqual(
                "Доступно обновление <color=#4A9EFF>v0.1.4</color>",
                text);
        }

        [Test]
        public void CtaLabel_MapsInteractivePhases()
        {
            Assert.AreEqual(LauncherProgressRules.CtaPlayLabel, LauncherProgressRules.CtaLabel(LauncherProgressPhase.Ready));
            Assert.AreEqual(LauncherProgressRules.CtaUpdateLabel, LauncherProgressRules.CtaLabel(LauncherProgressPhase.UpdateAvailable));
            Assert.AreEqual(GameUpdateUiRules.RestartButtonLabel, LauncherProgressRules.CtaLabel(LauncherProgressPhase.ReadyToRestart));
            Assert.AreEqual(LauncherProgressRules.CtaPlayLabel, LauncherProgressRules.CtaLabel(LauncherProgressPhase.Downloading));
        }

        [Test]
        public void ShouldShowProgressDetails_OnlyWhileDownloadingOrInstalling()
        {
            Assert.IsTrue(LauncherProgressRules.ShouldShowProgressDetails(LauncherProgressPhase.Downloading));
            Assert.IsTrue(LauncherProgressRules.ShouldShowProgressDetails(LauncherProgressPhase.Installing));
            Assert.IsFalse(LauncherProgressRules.ShouldShowProgressDetails(LauncherProgressPhase.Checking));
            Assert.IsFalse(LauncherProgressRules.ShouldShowProgressDetails(LauncherProgressPhase.UpdateAvailable));
            Assert.IsFalse(LauncherProgressRules.ShouldShowProgressDetails(LauncherProgressPhase.Ready));
            Assert.IsFalse(LauncherProgressRules.ShouldShowProgressDetails(LauncherProgressPhase.Warming));
        }

        [Test]
        public void IsCtaEnabled_ForReadyUpdateAndRestart()
        {
            Assert.IsTrue(LauncherProgressRules.IsCtaEnabled(LauncherProgressPhase.Ready));
            Assert.IsTrue(LauncherProgressRules.IsCtaEnabled(LauncherProgressPhase.UpdateAvailable));
            Assert.IsTrue(LauncherProgressRules.IsCtaEnabled(LauncherProgressPhase.ReadyToRestart));
            Assert.IsFalse(LauncherProgressRules.IsCtaEnabled(LauncherProgressPhase.Downloading));
            Assert.IsFalse(LauncherProgressRules.IsCtaEnabled(LauncherProgressPhase.Checking));
            Assert.IsFalse(LauncherProgressRules.IsCtaEnabled(LauncherProgressPhase.Warming));
        }

        [Test]
        public void FromApplyPhase_MapsServicePhases()
        {
            Assert.AreEqual(
                LauncherProgressPhase.Downloading,
                LauncherProgressRules.FromApplyPhase(GameUpdateApplyPhase.Downloading));
            Assert.AreEqual(
                LauncherProgressPhase.Installing,
                LauncherProgressRules.FromApplyPhase(GameUpdateApplyPhase.Installing));
            Assert.AreEqual(
                LauncherProgressPhase.ReadyToRestart,
                LauncherProgressRules.FromApplyPhase(GameUpdateApplyPhase.ReadyToRestart));
        }
    }
}
