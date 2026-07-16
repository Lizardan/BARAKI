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
        public void SessionToken_RoundTrip()
        {
            var token = PlayerReconnectRules.BuildSessionToken("matchA", 3);
            Assert.IsTrue(PlayerReconnectRules.TryParseSessionToken(token, out var matchId, out var slot));
            Assert.AreEqual("matchA", matchId);
            Assert.AreEqual(3, slot);
        }
    }
}
