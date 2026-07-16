using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HostMigrationRulesTests
    {
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
