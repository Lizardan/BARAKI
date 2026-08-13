using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchConnectionPayloadRulesTests
    {
        [Test]
        public void BuildInitial_WithPlayerId_RoundTrips()
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(
                MatchConnectionPayloadRules.BuildInitial(true, "Hero", "player-9"));

            Assert.IsTrue(MatchConnectionPayloadRules.TryReadDisplayName(payload, out var name));
            Assert.AreEqual("Hero", name);
            Assert.IsTrue(MatchConnectionPayloadRules.TryReadPlayerId(payload, out var playerId));
            Assert.AreEqual("player-9", playerId);
        }

        [Test]
        public void TryReadPlayerId_LegacyPayload_ReturnsEmpty()
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(
                MatchConnectionPayloadRules.BuildInitial(false, "Guest"));

            Assert.IsTrue(MatchConnectionPayloadRules.TryReadDisplayName(payload, out var name));
            Assert.AreEqual("Guest", name);
            Assert.IsFalse(MatchConnectionPayloadRules.TryReadPlayerId(payload, out _));
        }

        [Test]
        public void TryReadDisplayName_ReconnectPayload_ReturnsFalse()
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(
                MatchConnectionPayloadRules.BuildReconnect("room:3:player-9"));

            Assert.IsFalse(MatchConnectionPayloadRules.TryReadDisplayName(payload, out _));
        }

        [Test]
        public void TryReadReconnectToken_RoundTrips()
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(
                MatchConnectionPayloadRules.BuildReconnect("room:3:player-9"));
            Assert.IsTrue(MatchConnectionPayloadRules.TryReadReconnectToken(payload, out var token));
            Assert.AreEqual("room:3:player-9", token);
        }
    }
}
