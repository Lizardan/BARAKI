using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameUpdatePendingRestartRulesTests
    {
        [Test]
        public void FormatAndParseMarker_RoundTrips()
        {
            var text = GameUpdatePendingRestartRules.FormatMarker(@"C:\staging\payload", "0.1.37");
            Assert.IsTrue(GameUpdatePendingRestartRules.TryParseMarker(text, out var dir, out var version));
            Assert.AreEqual(@"C:\staging\payload", dir);
            Assert.AreEqual("0.1.37", version);
        }

        [Test]
        public void TryParseMarker_RejectsEmpty()
        {
            Assert.IsFalse(GameUpdatePendingRestartRules.TryParseMarker("", out _, out _));
            Assert.IsFalse(GameUpdatePendingRestartRules.TryParseMarker("   ", out _, out _));
        }

        [Test]
        public void ShouldAcceptApplyProgress_BlocksLateInstallAfterReady()
        {
            Assert.IsFalse(GameUpdatePendingRestartRules.ShouldAcceptApplyProgress(
                isReadyToRestart: true,
                GameUpdateApplyPhase.Installing));
            Assert.IsFalse(GameUpdatePendingRestartRules.ShouldAcceptApplyProgress(
                isReadyToRestart: true,
                GameUpdateApplyPhase.Downloading));
            Assert.IsTrue(GameUpdatePendingRestartRules.ShouldAcceptApplyProgress(
                isReadyToRestart: true,
                GameUpdateApplyPhase.ReadyToRestart));
            Assert.IsTrue(GameUpdatePendingRestartRules.ShouldAcceptApplyProgress(
                isReadyToRestart: false,
                GameUpdateApplyPhase.Installing));
        }
    }
}
