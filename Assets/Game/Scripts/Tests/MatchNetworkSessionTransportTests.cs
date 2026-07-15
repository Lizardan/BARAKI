using Game.Gameplay.Networking;
using NUnit.Framework;

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
    }
}
