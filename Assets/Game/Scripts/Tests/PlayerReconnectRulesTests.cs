using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class PlayerReconnectRulesTests
    {
        [Test]
        public void CanReconnect_WithinGrace()
        {
            Assert.IsTrue(PlayerReconnectRules.CanReconnect(true, true, 30f));
            Assert.IsFalse(PlayerReconnectRules.CanReconnect(true, true, 120f));
            Assert.IsFalse(PlayerReconnectRules.CanReconnect(false, true, 10f));
        }

        [Test]
        public void SessionToken_RoundTrip_WithPlayerId()
        {
            var token = PlayerReconnectRules.BuildSessionToken("matchA", 3, "player-42");
            Assert.IsTrue(PlayerReconnectRules.TryParseSessionToken(
                token,
                out var matchId,
                out var slot,
                out var playerId));
            Assert.AreEqual("matchA", matchId);
            Assert.AreEqual(3, slot);
            Assert.AreEqual("player-42", playerId);
        }

        [Test]
        public void SessionToken_RoundTrip_WithoutPlayerId()
        {
            var token = PlayerReconnectRules.BuildSessionToken("matchA", 3);
            Assert.IsTrue(PlayerReconnectRules.TryParseSessionToken(
                token,
                out var matchId,
                out var slot,
                out var playerId));
            Assert.AreEqual("matchA", matchId);
            Assert.AreEqual(3, slot);
            Assert.IsTrue(string.IsNullOrEmpty(playerId));
        }

        [Test]
        public void TryParseSessionToken_Malformed_ReturnsFalse()
        {
            Assert.IsFalse(PlayerReconnectRules.TryParseSessionToken("", out _, out _, out _));
            Assert.IsFalse(PlayerReconnectRules.TryParseSessionToken("room", out _, out _, out _));
            Assert.IsFalse(PlayerReconnectRules.TryParseSessionToken("room:x", out _, out _, out _));
            Assert.IsFalse(PlayerReconnectRules.TryParseSessionToken(":3", out _, out _, out _));
        }

        [Test]
        public void CanClaimSlot_RejectsMismatchedPlayerId()
        {
            Assert.IsTrue(PlayerReconnectRules.CanClaimSlot("player-1", "player-1"));
            Assert.IsTrue(PlayerReconnectRules.CanClaimSlot("player-1", string.Empty));
            Assert.IsFalse(PlayerReconnectRules.CanClaimSlot(string.Empty, "player-1"));
            Assert.IsFalse(PlayerReconnectRules.CanClaimSlot("player-1", "player-2"));
        }
    }
}
