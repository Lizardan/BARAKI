using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchCommandResultRulesTests
    {
        [Test]
        public void ClassifyResearchFailure_NotEnoughGold()
        {
            var result = MatchCommandResultRules.ClassifyResearchFailure(
                buildingValid: true,
                hasQueueSpace: true,
                enoughGold: false);
            Assert.AreEqual(MatchCommandResult.NotEnoughGold, result);
            Assert.IsFalse(string.IsNullOrEmpty(MatchCommandResultRules.FormatFeedback(result)));
        }

        [Test]
        public void ClassifyResearchFailure_QueueFull()
        {
            Assert.AreEqual(
                MatchCommandResult.QueueFull,
                MatchCommandResultRules.ClassifyResearchFailure(true, false, true));
        }

        [Test]
        public void FromTrySuccess_MapsBool()
        {
            Assert.AreEqual(MatchCommandResult.Ok, MatchCommandResultRules.FromTrySuccess(true));
            Assert.AreEqual(MatchCommandResult.NotAllowed, MatchCommandResultRules.FromTrySuccess(false));
        }
    }
}
