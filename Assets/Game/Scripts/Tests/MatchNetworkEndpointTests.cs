using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchNetworkEndpointTests
    {
        [TestCase("ws://127.0.0.1:7777", "127.0.0.1", 7777, false)]
        [TestCase("wss://match.example.com", "match.example.com", 443, true)]
        [TestCase("WS://localhost:9000", "localhost", 9000, false)]
        public void TryParse_WebSocketEndpoint_ReturnsConnection(
            string value,
            string expectedHost,
            int expectedPort,
            bool expectedSecure)
        {
            Assert.IsTrue(MatchNetworkEndpoint.TryParse(value, out var endpoint));
            Assert.IsTrue(endpoint.IsNetworked);
            Assert.AreEqual(expectedHost, endpoint.Host);
            Assert.AreEqual(expectedPort, endpoint.Port);
            Assert.AreEqual(expectedSecure, endpoint.IsSecure);
        }

        [Test]
        public void TryParse_LocalEndpoint_ReturnsOfflineRoomCode()
        {
            Assert.IsTrue(MatchNetworkEndpoint.TryParse("local://ABCD", out var endpoint));
            Assert.IsTrue(endpoint.IsLocal);
            Assert.IsFalse(endpoint.IsNetworked);
            Assert.AreEqual("ABCD", endpoint.LocalCode);
        }

        [Test]
        public void TryParse_RelayHostAndClient_ReturnsRelayCodes()
        {
            Assert.IsTrue(MatchNetworkEndpoint.TryParse("relay-host://ABC123", out var host));
            Assert.IsTrue(host.IsRelayHost);
            Assert.IsTrue(host.IsNetworked);
            Assert.AreEqual("ABC123", host.LocalCode);

            Assert.IsTrue(MatchNetworkEndpoint.TryParse("relay://ABC123", out var client));
            Assert.IsTrue(client.IsRelay);
            Assert.IsFalse(client.IsRelayHost);
            Assert.AreEqual("ABC123", client.LocalCode);
        }

        [TestCase("")]
        [TestCase("http://127.0.0.1:7777")]
        [TestCase("ws://")]
        [TestCase("local://")]
        [TestCase("ws://localhost:70000")]
        public void TryParse_InvalidEndpoint_ReturnsFalse(string value)
        {
            Assert.IsFalse(MatchNetworkEndpoint.TryParse(value, out _));
        }
    }
}
