using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchHudPassiveGoldRulesTests
    {
        [Test]
        public void GetFill01_MapsRemainingToProgress()
        {
            Assert.AreEqual(0f, MatchHudPassiveGoldRules.GetFill01(30f, 30f), 0.001f);
            Assert.AreEqual(0.5f, MatchHudPassiveGoldRules.GetFill01(15f, 30f), 0.001f);
            Assert.AreEqual(1f, MatchHudPassiveGoldRules.GetFill01(0f, 30f), 0.001f);
        }
    }
}
