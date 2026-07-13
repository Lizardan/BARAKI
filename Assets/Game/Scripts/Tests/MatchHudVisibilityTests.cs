using Game.Gameplay.Match;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchHudVisibilityTests
    {
        [Test]
        public void ShouldClearRunningHud_WhenControllerMissing_ReturnsTrue()
        {
            Assert.IsTrue(MatchHudVisibility.ShouldClearRunningHud(hasController: false, isRunning: false, MatchPhase.Lobby));
        }

        [Test]
        public void ShouldClearRunningHud_WhenRunning_ReturnsFalse()
        {
            Assert.IsFalse(MatchHudVisibility.ShouldClearRunningHud(hasController: true, isRunning: true, MatchPhase.Early));
        }

        [Test]
        public void ShouldClearRunningHud_WhenPhaseEnd_ReturnsFalse()
        {
            Assert.IsFalse(MatchHudVisibility.ShouldClearRunningHud(hasController: true, isRunning: false, MatchPhase.End));
        }

        [Test]
        public void ShouldClearRunningHud_WhenLobbyNotRunning_ReturnsTrue()
        {
            Assert.IsTrue(MatchHudVisibility.ShouldClearRunningHud(hasController: true, isRunning: false, MatchPhase.Lobby));
        }
    }
}
