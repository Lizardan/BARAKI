using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HostMigrationRulesTests
    {
        [Test]
        public void BuildEligibleOccupied_SkipsReserved()
        {
            var eligible = HostMigrationRules.BuildEligibleOccupied(
                new[] { true, true, true },
                new[] { true, false, true });
            Assert.IsFalse(eligible[0]);
            Assert.IsTrue(eligible[1]);
            Assert.IsFalse(eligible[2]);
        }

        [Test]
        public void ShouldHoldPauseAfterMigration_WhenReservedRemain()
        {
            Assert.IsTrue(HostMigrationRules.ShouldHoldPauseAfterMigration(1));
            Assert.IsFalse(HostMigrationRules.ShouldHoldPauseAfterMigration(0));
        }

        [Test]
        public void ElectNewHostSlot_PicksNextOccupied()
        {
            var slots = new[] { true, false, true, true };
            Assert.AreEqual(2, HostMigrationRules.ElectNewHostSlot(0, slots));
            Assert.AreEqual(0, HostMigrationRules.ElectNewHostSlot(3, slots));
        }

        [Test]
        public void ElectNewHostSlot_WhenAlone_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, HostMigrationRules.ElectNewHostSlot(0, new[] { true, false, false }));
        }

        [Test]
        public void ElectNewHostSlot_NegativePreviousSlot_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, HostMigrationRules.ElectNewHostSlot(-1, new[] { true, true, true }));
        }

        [Test]
        public void ElectNewHostSlot_PreviousSlotOutOfRange_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, HostMigrationRules.ElectNewHostSlot(7, new[] { true, true }));
            Assert.AreEqual(-1, HostMigrationRules.ElectNewHostSlot(2, new[] { true, true }));
        }

        [Test]
        public void IsValidHostSlot_AcceptsInRangeOnly()
        {
            Assert.IsTrue(HostMigrationRules.IsValidHostSlot(0, 4));
            Assert.IsTrue(HostMigrationRules.IsValidHostSlot(3, 4));
            Assert.IsFalse(HostMigrationRules.IsValidHostSlot(-1, 4));
            Assert.IsFalse(HostMigrationRules.IsValidHostSlot(4, 4));
        }

        [Test]
        public void ShouldBeginMigrationAfterGrace_RequiresGraceElapsed()
        {
            Assert.IsFalse(HostMigrationRules.ShouldBeginMigrationAfterGrace(0.5f, 1.5f));
            Assert.IsTrue(HostMigrationRules.ShouldBeginMigrationAfterGrace(1.5f, 1.5f));
            Assert.IsTrue(HostMigrationRules.ShouldBeginMigrationAfterGrace(3f, 1.5f));
            Assert.IsFalse(HostMigrationRules.ShouldBeginMigrationAfterGrace(-1f, 1.5f));
        }

        [Test]
        public void HasEnoughClientsRejoined_WhenAtOrAboveExpected()
        {
            Assert.IsFalse(HostMigrationRules.HasEnoughClientsRejoined(0, 2));
            Assert.IsFalse(HostMigrationRules.HasEnoughClientsRejoined(1, 2));
            Assert.IsTrue(HostMigrationRules.HasEnoughClientsRejoined(2, 2));
            Assert.IsTrue(HostMigrationRules.HasEnoughClientsRejoined(3, 2));
        }

        [Test]
        public void HasClientWaitTimedOut_AfterTimeout()
        {
            Assert.IsFalse(HostMigrationRules.HasClientWaitTimedOut(1f, 5f));
            Assert.IsTrue(HostMigrationRules.HasClientWaitTimedOut(5f, 5f));
        }

        [Test]
        public void ShouldPauseMatch_OnlyWhenHostDropsMidMatch()
        {
            Assert.IsTrue(HostMigrationRules.ShouldPauseMatch(true, true));
            Assert.IsFalse(HostMigrationRules.ShouldPauseMatch(true, false));
            Assert.IsFalse(HostMigrationRules.ShouldPauseMatch(false, true));
        }

        [Test]
        public void NextPhase_AdvancesOrAborts()
        {
            Assert.AreEqual(
                HostMigrationRules.MigrationPhase.PausedAwaitingHost,
                HostMigrationRules.NextPhase(HostMigrationRules.MigrationPhase.Playing, true));
            Assert.AreEqual(
                HostMigrationRules.MigrationPhase.Aborted,
                HostMigrationRules.NextPhase(HostMigrationRules.MigrationPhase.TransferringState, false));
        }

        [Test]
        public void CanResume_RequiresAllFlags()
        {
            Assert.IsTrue(HostMigrationRules.CanResume(
                HostMigrationRules.MigrationPhase.Resuming,
                newHostReady: true,
                allClientsReconnected: true,
                stateApplied: true));
            Assert.IsFalse(HostMigrationRules.CanResume(
                HostMigrationRules.MigrationPhase.Resuming,
                newHostReady: true,
                allClientsReconnected: false,
                stateApplied: true));
        }
    }
}
