using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchRematchRulesTests
    {
        [Test]
        public void IsMatchInProgressForHostMigration_FalseWhenEnded()
        {
            Assert.IsFalse(MatchRematchRules.IsMatchInProgressForHostMigration(MatchPhase.End));
            Assert.IsTrue(MatchRematchRules.IsMatchInProgressForHostMigration(MatchPhase.Early));
            Assert.IsTrue(MatchRematchRules.IsMatchInProgressForHostMigration(MatchPhase.Mid));
        }

        [Test]
        public void ShouldReturnEveryoneToLobby_OnlyWhenNetworked()
        {
            Assert.IsTrue(MatchRematchRules.ShouldReturnEveryoneToLobby(isNetworked: true));
            Assert.IsFalse(MatchRematchRules.ShouldReturnEveryoneToLobby(isNetworked: false));
        }
    }
}
