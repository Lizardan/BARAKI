using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameSessionTests
    {
        [TearDown]
        public void TearDown()
        {
            GameSession.Reset();
        }

        [Test]
        public void Begin_SetsPlayingAndActiveSetup()
        {
            var setup = new MatchSetup(playerCount: 4, localPlayerSlot: 2);

            GameSession.Begin(setup);

            Assert.IsTrue(GameSession.IsPlaying);
            Assert.AreEqual(2, GameSession.ActiveSetup.LocalPlayerSlot);
            Assert.AreEqual(4, GameSession.ActiveSetup.PlayerCount);
        }

        [Test]
        public void Begin_WhenAlreadyPlaying_DoesNotReplaceSetup()
        {
            GameSession.Begin(new MatchSetup(playerCount: 4, localPlayerSlot: 0));
            GameSession.Begin(new MatchSetup(playerCount: 4, localPlayerSlot: 3));

            Assert.AreEqual(0, GameSession.ActiveSetup.LocalPlayerSlot);
        }

        [Test]
        public void UpdateActiveSetup_WhilePlaying_ReplacesSetup()
        {
            GameSession.Begin(new MatchSetup(playerCount: 4, localPlayerSlot: 0));

            GameSession.UpdateActiveSetup(new MatchSetup(playerCount: 4, localPlayerSlot: 2));

            Assert.IsTrue(GameSession.IsPlaying);
            Assert.AreEqual(2, GameSession.ActiveSetup.LocalPlayerSlot);
        }

        [Test]
        public void UpdateActiveSetup_WhenNotPlaying_DoesNothing()
        {
            GameSession.UpdateActiveSetup(new MatchSetup(playerCount: 4, localPlayerSlot: 1));

            Assert.IsFalse(GameSession.IsPlaying);
            Assert.IsNull(GameSession.ActiveSetup);
        }

        [Test]
        public void Reset_ClearsPlayingAndSetup()
        {
            GameSession.Begin(MatchSetup.Default);
            GameSession.Reset();

            Assert.IsFalse(GameSession.IsPlaying);
            Assert.IsNull(GameSession.ActiveSetup);
        }
    }
}
