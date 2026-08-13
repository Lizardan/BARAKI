using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchDisconnectHoldRulesTests
    {
        [Test]
        public void ShouldPauseMatch_WhenReservedOrOverlayActive()
        {
            Assert.IsTrue(MatchDisconnectHoldRules.ShouldPauseMatch(
                true, 1, MatchDisconnectHoldRules.OverlayPhase.Waiting, false));
            Assert.IsTrue(MatchDisconnectHoldRules.ShouldPauseMatch(
                true, 0, MatchDisconnectHoldRules.OverlayPhase.Returned, false));
            Assert.IsTrue(MatchDisconnectHoldRules.ShouldPauseMatch(
                true, 0, MatchDisconnectHoldRules.OverlayPhase.None, true));
            Assert.IsFalse(MatchDisconnectHoldRules.ShouldPauseMatch(
                true, 0, MatchDisconnectHoldRules.OverlayPhase.None, false));
            Assert.IsFalse(MatchDisconnectHoldRules.ShouldPauseMatch(
                false, 1, MatchDisconnectHoldRules.OverlayPhase.Waiting, false));
        }

        [Test]
        public void OverlayReadDelayAndPersistInterval_ArePositive()
        {
            Assert.AreEqual(1f, MatchDisconnectHoldRules.OverlayReadDelaySeconds);
            Assert.Greater(MatchDisconnectHoldRules.LocalPersistIntervalSeconds, 0f);
        }

        [Test]
        public void CanUnpause_AfterReadDelayWhenNobodyReserved()
        {
            Assert.IsTrue(MatchDisconnectHoldRules.CanUnpause(
                true, 0, MatchDisconnectHoldRules.OverlayPhase.Returned, false, true));
            Assert.IsFalse(MatchDisconnectHoldRules.CanUnpause(
                true, 0, MatchDisconnectHoldRules.OverlayPhase.Returned, false, false));
            Assert.IsFalse(MatchDisconnectHoldRules.CanUnpause(
                true, 1, MatchDisconnectHoldRules.OverlayPhase.Returned, false, true));
            Assert.IsFalse(MatchDisconnectHoldRules.CanUnpause(
                true, 0, MatchDisconnectHoldRules.OverlayPhase.Waiting, false, true));
        }

        [Test]
        public void KickHost_GoesToMigrating()
        {
            Assert.AreEqual(
                MatchDisconnectHoldRules.OverlayPhase.Migrating,
                MatchDisconnectHoldRules.NextPhaseAfterKick(true));
            Assert.AreEqual(
                MatchDisconnectHoldRules.OverlayPhase.Kicked,
                MatchDisconnectHoldRules.NextPhaseAfterKick(false));
        }

        [Test]
        public void ShouldShowKickButton_OnlyWhileWaitingWithReserved()
        {
            Assert.IsTrue(MatchDisconnectHoldRules.ShouldShowKickButton(
                MatchDisconnectHoldRules.OverlayPhase.Waiting, 1));
            Assert.IsFalse(MatchDisconnectHoldRules.ShouldShowKickButton(
                MatchDisconnectHoldRules.OverlayPhase.Returned, 1));
            Assert.IsFalse(MatchDisconnectHoldRules.ShouldShowKickButton(
                MatchDisconnectHoldRules.OverlayPhase.Waiting, 0));
        }

        [Test]
        public void FormatStatus_UsesDisplayName()
        {
            Assert.AreEqual("Alpha завис", MatchDisconnectHoldRules.FormatWaiting("Alpha"));
            Assert.AreEqual("Alpha вернулся", MatchDisconnectHoldRules.FormatReturned("Alpha"));
            Assert.AreEqual("Alpha исключён", MatchDisconnectHoldRules.FormatKicked("Alpha"));
            Assert.AreEqual("Смена хоста…", MatchDisconnectHoldRules.FormatMigrating());
            Assert.AreEqual("Игрок завис", MatchDisconnectHoldRules.FormatWaiting(" "));
        }
    }
}
