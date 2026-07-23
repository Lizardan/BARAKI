using Game.Core;
using Game.Editor;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UpdaterReleaseRulesTests
    {
        [Test]
        public void InstallerFileName_IsStableDownloadName()
        {
            Assert.AreEqual("BARAKI-Setup.exe", UpdaterReleaseRules.InstallerFileName);
        }

        [Test]
        public void InstalledExecutable_StaysMainGameExecutable()
        {
            Assert.AreEqual("BARAKI.exe", UpdaterReleaseRules.InstalledExecutableFileName);
        }

        [Test]
        public void TagForVersion_UsesUpdaterPrefix()
        {
            Assert.AreEqual("updater-v0.1.8", UpdaterReleaseRules.TagForVersion("0.1.8"));
            Assert.AreEqual("updater-v0.1.8", UpdaterReleaseRules.TagForVersion("v0.1.8"));
        }

        [Test]
        public void IsUpdaterTag_DoesNotMatchFullReleaseTag()
        {
            Assert.IsTrue(UpdaterReleaseRules.IsUpdaterTag("updater-v0.1.8"));
            Assert.IsFalse(UpdaterReleaseRules.IsUpdaterTag("v0.1.8"));
        }

        [Test]
        public void BuildRules_UseOnlyBootstrapScene()
        {
            CollectionAssert.AreEqual(
                new[] { "Assets/Game/Scenes/Bootstrap.unity" },
                UpdaterBuildRules.Scenes);
        }

        [Test]
        public void BuildRules_EnableUpdaterOnlyDefine()
        {
            CollectionAssert.Contains(
                UpdaterBuildRules.ExtraScriptingDefines,
                "BARAKI_UPDATER_ONLY");
        }

        [Test]
        public void BootstrapFlow_UpdaterOnlyNeverOffersEnterGame()
        {
            Assert.IsFalse(BootstrapUpdateFlowRules.ShouldOfferEnterGame(
                isUpdaterOnlyBuild: true,
                updateRequired: false));
            Assert.IsFalse(BootstrapUpdateFlowRules.ShouldOfferEnterGame(
                isUpdaterOnlyBuild: true,
                updateRequired: true));
        }

        [Test]
        public void BootstrapFlow_FullBuildOffersEnterGameWhenNoUpdateRequired()
        {
            Assert.IsTrue(BootstrapUpdateFlowRules.ShouldOfferEnterGame(
                isUpdaterOnlyBuild: false,
                updateRequired: false));
            Assert.IsFalse(BootstrapUpdateFlowRules.ShouldOfferEnterGame(
                isUpdaterOnlyBuild: false,
                updateRequired: true));
        }
    }
}
