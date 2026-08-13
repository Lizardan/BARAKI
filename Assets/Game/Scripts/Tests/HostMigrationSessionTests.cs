using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HostMigrationSessionTests
    {
        [TearDown]
        public void TearDown() => HostMigrationSession.Clear();

        [Test]
        public void Begin_PreservesQueuedKick()
        {
            HostMigrationSession.QueueKick(3);
            HostMigrationSession.Begin(0, 1, lastGoodBytes: null, previousRelayJoinCode: "old");

            Assert.IsTrue(HostMigrationSession.IsRebinding);
            Assert.AreEqual(3, HostMigrationSession.PendingKickSlot);
        }

        [Test]
        public void RequestKickDisconnected_WithoutLobby_QueuesKick()
        {
            HostMigrationSession.Clear();
            MatchNetworkSession.RequestKickDisconnected(4);
            Assert.AreEqual(4, HostMigrationSession.PendingKickSlot);
        }

        [Test]
        public void ConsumePendingKick_ReturnsAndClearsQueue()
        {
            HostMigrationSession.QueueKick(2);
            Assert.AreEqual(2, HostMigrationSession.ConsumePendingKick());
            Assert.AreEqual(-1, HostMigrationSession.PendingKickSlot);
        }

        [Test]
        public void CountReservedSlots_IncludesPreviousHost()
        {
            var snapshot = new[]
            {
                new HostMigrationSlotSnapshot(true, true, "Host", "p0", 1f),
                new HostMigrationSlotSnapshot(true, false, "Next", "p1", 0f),
            };
            HostMigrationSession.Begin(0, 1, null, "relay", snapshot);
            Assert.AreEqual(1, HostMigrationSession.CountReservedSlots());
        }
    }
}
