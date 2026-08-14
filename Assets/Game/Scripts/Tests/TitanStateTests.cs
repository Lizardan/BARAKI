using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TitanStateTests
    {
        [Test]
        public void TickCooldowns_SmallDelta_DoesNotThrowAndDecrements()
        {
            var titan = new TitanState();
            titan.LastSummonBarracksInstanceId = 42;
            titan.StartDeathCooldown(TitanRules.DeathCooldownSeconds);

            Assert.DoesNotThrow(() => titan.TickCooldowns(0.05f));
            Assert.AreEqual(TitanRules.DeathCooldownSeconds - 0.05f, titan.GetDeathCooldown(42), 0.001f);
            Assert.AreEqual(0f, titan.GetDeathCooldown(99));
        }
    }
}
