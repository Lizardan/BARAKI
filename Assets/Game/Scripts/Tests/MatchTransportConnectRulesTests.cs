using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchTransportConnectRulesTests
    {
        [Test]
        public void IsConnectComplete_RequiresClientAndLobby()
        {
            Assert.IsFalse(MatchTransportConnectRules.IsConnectComplete(false, false));
            Assert.IsFalse(MatchTransportConnectRules.IsConnectComplete(true, false));
            Assert.IsFalse(MatchTransportConnectRules.IsConnectComplete(false, true));
            Assert.IsTrue(MatchTransportConnectRules.IsConnectComplete(true, true));
        }

        [Test]
        public void HasTimedOut_UsesConfiguredSeconds()
        {
            Assert.IsFalse(MatchTransportConnectRules.HasTimedOut(14.9f, 15f));
            Assert.IsTrue(MatchTransportConnectRules.HasTimedOut(15f, 15f));
            Assert.IsTrue(MatchTransportConnectRules.HasTimedOut(20f, 15f));
        }

        [Test]
        public void ConnectFailedMessage_MentionsRelay()
        {
            StringAssert.Contains("Relay", MatchTransportConnectRules.ConnectFailedMessage);
        }
    }
}
