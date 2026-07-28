using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SessionFlowRulesTests
    {
        [Test]
        public void Resolve_NetworkConnecting_BeatsMainMenuScene()
        {
            var state = SessionFlowRules.Resolve(
                GameSceneNames.MainMenu,
                isNetworked: true,
                hasNetworkLobby: false,
                matchStarted: false,
                matchSimStarted: false);

            Assert.AreEqual(SessionFlowState.Connecting, state);
        }

        [Test]
        public void Resolve_Lobby_WhenNetworkLobbyAndNotStarted()
        {
            var state = SessionFlowRules.Resolve(
                GameSceneNames.Lobby,
                isNetworked: true,
                hasNetworkLobby: true,
                matchStarted: false,
                matchSimStarted: false);

            Assert.AreEqual(SessionFlowState.Lobby, state);
        }

        [Test]
        public void Resolve_RacePick_WhenMatchStartedButSimNotStarted()
        {
            var state = SessionFlowRules.Resolve(
                GameSceneNames.Game,
                isNetworked: true,
                hasNetworkLobby: true,
                matchStarted: true,
                matchSimStarted: false);

            Assert.AreEqual(SessionFlowState.RacePick, state);
        }

        [Test]
        public void Resolve_Match_WhenSimStarted()
        {
            var state = SessionFlowRules.Resolve(
                GameSceneNames.Game,
                isNetworked: true,
                hasNetworkLobby: true,
                matchStarted: true,
                matchSimStarted: true);

            Assert.AreEqual(SessionFlowState.Match, state);
        }

        [Test]
        public void Resolve_Bootstrap_FromSceneWhenOffline()
        {
            Assert.AreEqual(
                SessionFlowState.Bootstrap,
                SessionFlowRules.Resolve(
                    GameSceneNames.Bootstrap,
                    isNetworked: false,
                    hasNetworkLobby: false,
                    matchStarted: false,
                    matchSimStarted: false));
        }

        [Test]
        public void Resolve_MainMenu_FromSceneWhenOffline()
        {
            Assert.AreEqual(
                SessionFlowState.MainMenu,
                SessionFlowRules.Resolve(
                    GameSceneNames.MainMenu,
                    isNetworked: false,
                    hasNetworkLobby: false,
                    matchStarted: false,
                    matchSimStarted: false));
        }

        [TestCase(0f, SessionFlowRules.ElapsedLt5)]
        [TestCase(299f, SessionFlowRules.ElapsedLt5)]
        [TestCase(300f, "5+")]
        [TestCase(599f, "5+")]
        [TestCase(600f, "10+")]
        [TestCase(901f, "15+")]
        public void ResolveElapsedBucket_UsesFiveMinuteSteps(float seconds, string expected)
        {
            Assert.AreEqual(expected, SessionFlowRules.ResolveElapsedBucket(seconds));
        }

        [Test]
        public void FormatElapsedBucketForUi_UsesRussianPhrases()
        {
            Assert.AreEqual("меньше 5 мин", SessionFlowRules.FormatElapsedBucketForUi(SessionFlowRules.ElapsedLt5));
            Assert.AreEqual("5+ мин", SessionFlowRules.FormatElapsedBucketForUi("5+"));
            Assert.AreEqual("10+ мин", SessionFlowRules.FormatElapsedBucketForUi("10+"));
        }
    }
}
