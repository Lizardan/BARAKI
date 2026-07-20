using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchNetworkSimTickRulesTests
    {
        [Test]
        public void ConsumeSteps_AccumulatesToFixedHz()
        {
            var accumulator = 0f;
            var steps = MatchNetworkSimTickRules.ConsumeSteps(ref accumulator, 0.05f);
            Assert.AreEqual(1, steps);
            Assert.Greater(accumulator, 0f);
            Assert.Less(accumulator, MatchNetworkSimTickRules.FixedDeltaSeconds);
        }

        [Test]
        public void ConsumeSteps_CapsMaxStepsPerFrame()
        {
            var accumulator = 0f;
            var steps = MatchNetworkSimTickRules.ConsumeSteps(ref accumulator, 1f);
            Assert.AreEqual(MatchNetworkSimTickRules.MaxStepsPerFrame, steps);
            Assert.LessOrEqual(accumulator, MatchNetworkSimTickRules.FixedDeltaSeconds);
        }
    }
}
