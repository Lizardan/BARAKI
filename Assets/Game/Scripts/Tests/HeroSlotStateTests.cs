using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HeroSlotStateTests
    {
        [Test]
        public void TickCooldowns_SmallDelta_DoesNotThrowAndDecrements()
        {
            var slot = new HeroSlotState(1);
            slot.LastDeployBarracksInstanceId = 42;
            slot.StartDeathCooldown(HeroRules.DeathCooldownSeconds);

            Assert.DoesNotThrow(() => slot.TickCooldowns(0.05f));
            Assert.AreEqual(HeroRules.DeathCooldownSeconds - 0.05f, slot.GetDeathCooldown(42), 0.001f);
            Assert.AreEqual(0f, slot.GetDeathCooldown(99));
        }
    }
}
