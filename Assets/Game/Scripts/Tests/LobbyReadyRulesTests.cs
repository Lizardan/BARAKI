using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LobbyReadyRulesTests
    {
        [Test]
        public void CanHostStart_False_WhenAnySlotEmpty()
        {
            var lobby = new MatchLobbyState(2, "ABCD", "Host");
            lobby.SetReady(0, true);
            Assert.IsFalse(LobbyReadyRules.CanHostStart(lobby));
        }

        [Test]
        public void CanHostStart_False_WhenOccupiedButNotReady()
        {
            var lobby = new MatchLobbyState(2, "ABCD", "Host");
            lobby.OccupyNext("P2");
            lobby.SetReady(0, true);
            Assert.IsFalse(LobbyReadyRules.CanHostStart(lobby));
        }

        [Test]
        public void CanHostStart_True_WhenAllOccupiedAndReady()
        {
            var lobby = new MatchLobbyState(2, "ABCD", "Host");
            lobby.OccupyNext("P2");
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);
            Assert.IsTrue(LobbyReadyRules.CanHostStart(lobby));
        }

        [Test]
        public void FillStandIns_EnablesStart()
        {
            var lobby = new MatchLobbyState(4, "WXYZ", "Host");
            lobby.SetReady(0, true);
            lobby.FillEmptySlotsWithLocalStandIns();
            Assert.IsTrue(LobbyReadyRules.CanHostStart(lobby));
        }

        [Test]
        public void TryMarkMatchStarted_Idempotent()
        {
            var lobby = new MatchLobbyState(2, "ABCD", "Host");
            lobby.FillEmptySlotsWithLocalStandIns();
            lobby.SetReady(0, true);
            Assert.IsTrue(lobby.TryMarkMatchStarted());
            Assert.IsFalse(lobby.TryMarkMatchStarted());
        }
    }
}
