using Game.Core;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchmakerEnsureResponseTests
    {
        [Test]
        public void Parse_ReadsEnsurePayload()
        {
            const string json =
                "{\"match_id\":\"m1\",\"wss_url\":\"wss://ex.trycloudflare.com\",\"join_token\":\"m1:1\",\"slot\":1,\"player_count\":2,\"room_code\":\"ABCD1234\"}";

            var parsed = MatchmakerEnsureResponse.Parse(json);

            Assert.AreEqual("m1", parsed.MatchId);
            Assert.AreEqual("wss://ex.trycloudflare.com", parsed.WssUrl);
            Assert.AreEqual("m1:1", parsed.JoinToken);
            Assert.AreEqual(1, parsed.Slot);
            Assert.AreEqual(2, parsed.PlayerCount);
            Assert.AreEqual("ABCD1234", parsed.RoomCode);
        }

        [Test]
        public void Parse_ReadsStableWorkerProxyWssUrl()
        {
            const string json =
                "{\"match_id\":\"m1\",\"wss_url\":\"wss://baraki-matchmaker.lizard268.workers.dev\",\"join_token\":\"m1:0\",\"slot\":0,\"player_count\":2,\"room_code\":\"INSTANCE1\"}";

            var parsed = MatchmakerEnsureResponse.Parse(json);

            Assert.AreEqual("wss://baraki-matchmaker.lizard268.workers.dev", parsed.WssUrl);
            Assert.IsTrue(MatchNetworkEndpoint.TryParse(parsed.WssUrl, out var endpoint));
            Assert.AreEqual("baraki-matchmaker.lizard268.workers.dev", endpoint.Host);
            Assert.AreEqual(443, endpoint.Port);
            Assert.IsTrue(endpoint.IsSecure);
        }
    }
}
