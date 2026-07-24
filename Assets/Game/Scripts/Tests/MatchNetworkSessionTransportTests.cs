using Game.Gameplay.Networking;
using NUnit.Framework;
using System.Text;

namespace Game.Tests
{
    public sealed class MatchNetworkSessionTransportTests
    {
        [Test]
        public void TryStartTransportAsync_LocalEndpoint_SucceedsWithoutNetwork()
        {
            MatchNetworkSession.ApplyHandle(new MatchSessionHandle(
                roomCode: "ABCD",
                playerCount: 2,
                localPlayerSlot: 0,
                transportEndpoint: "local://ABCD"));

            Assert.IsFalse(MatchNetworkSession.IsNetworked);
            Assert.IsTrue(MatchNetworkSession.TryStartTransportAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void ConnectionPayload_RoundTripsDisplayName()
        {
            var payload = Encoding.UTF8.GetBytes(
                MatchConnectionPayloadRules.BuildInitial(isHost: false, "Lizardan"));

            Assert.IsTrue(MatchConnectionPayloadRules.TryReadDisplayName(payload, out var displayName));
            Assert.AreEqual("Lizardan", displayName);
        }

        [Test]
        public void ConnectionPayload_ReconnectDoesNotOverrideDisplayName()
        {
            var payload = Encoding.UTF8.GetBytes(
                MatchConnectionPayloadRules.BuildReconnect("ROOM:1"));

            Assert.IsFalse(MatchConnectionPayloadRules.TryReadDisplayName(payload, out _));
        }
    }
}
