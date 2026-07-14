using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class NetworkLobbyRulesTests
    {
        [Test]
        public void FindNextFreeSlot_ReturnsFirstUnoccupiedSlot()
        {
            var occupied = new[] { true, false, true, false };

            Assert.AreEqual(1, NetworkLobbySlotRules.FindNextFreeSlot(occupied, 4));
        }

        [Test]
        public void FindNextFreeSlot_FullLobby_ReturnsMinusOne()
        {
            var occupied = new[] { true, true };

            Assert.AreEqual(-1, NetworkLobbySlotRules.FindNextFreeSlot(occupied, 2));
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void IsHostSlot_OnlySlotZeroIsHost(int slot, bool expected)
        {
            Assert.AreEqual(expected, NetworkLobbySlotRules.IsHostSlot(slot));
        }

        [Test]
        public void CanDesignatedHostStart_OnlyHostWhenReady()
        {
            Assert.IsTrue(NetworkLobbySlotRules.CanDesignatedHostStart(0, matchStarted: false, lobbyReady: true));
            Assert.IsFalse(NetworkLobbySlotRules.CanDesignatedHostStart(1, matchStarted: false, lobbyReady: true));
            Assert.IsFalse(NetworkLobbySlotRules.CanDesignatedHostStart(0, matchStarted: true, lobbyReady: true));
            Assert.IsFalse(NetworkLobbySlotRules.CanDesignatedHostStart(0, matchStarted: false, lobbyReady: false));
        }

        [Test]
        public void FindNextFreeSlot_EmptyLobby_FirstClientGetsHostSlot()
        {
            var occupied = new[] { false, false };

            Assert.AreEqual(NetworkLobbySlotRules.HostSlot, NetworkLobbySlotRules.FindNextFreeSlot(occupied, 2));
        }
    }
}
