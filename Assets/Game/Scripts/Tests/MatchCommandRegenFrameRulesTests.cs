using Game.Gameplay.Match;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchCommandRegenFrameRulesTests
    {
        [Test]
        public void GetFill01_JustSpent_IsZero_AlmostDone_IsNearOne()
        {
            Assert.AreEqual(0f, MatchCommandRegenFrameRules.GetFill01(30f, 30f));
            Assert.AreEqual(0.5f, MatchCommandRegenFrameRules.GetFill01(15f, 30f), 0.001f);
            Assert.Greater(MatchCommandRegenFrameRules.GetFill01(0.1f, 30f), 0.99f);
            Assert.AreEqual(0f, MatchCommandRegenFrameRules.GetFill01(0f, 30f));
        }

        [Test]
        public void TryGetNextRegenRemaining_ReturnsSoonestTimer()
        {
            var state = new BarracksCallChargeState();
            state.Initialize(BarracksManualCallRules.GetDefaultSquadCounts(1));
            Assert.IsFalse(state.TryGetNextRegenRemaining(Game.Gameplay.Data.UnitRole.Melee, out _));

            Assert.IsTrue(state.TrySpend(Game.Gameplay.Data.UnitRole.Melee));
            Assert.IsTrue(state.TryGetNextRegenRemaining(Game.Gameplay.Data.UnitRole.Melee, out var remaining));
            Assert.AreEqual(BarracksManualCallRules.RegenSeconds, remaining, 0.001f);

            state.Tick(10f);
            Assert.IsTrue(state.TryGetNextRegenRemaining(Game.Gameplay.Data.UnitRole.Melee, out remaining));
            Assert.AreEqual(BarracksManualCallRules.RegenSeconds - 10f, remaining, 0.001f);
        }
    }
}
