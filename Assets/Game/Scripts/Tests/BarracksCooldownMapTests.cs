using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BarracksCooldownMapTests
    {
        [Test]
        public void Tick_SmallDelta_DoesNotThrowAndDecrements()
        {
            var map = new BarracksCooldownMap();
            map.Set(42, 300f);

            Assert.DoesNotThrow(() => map.Tick(0.05f));
            Assert.AreEqual(299.95f, map.Get(42), 0.001f);
        }

        [Test]
        public void Tick_ExpiresAndClears()
        {
            var map = new BarracksCooldownMap();
            map.Set(7, 300f);
            map.Tick(300f);
            Assert.AreEqual(0f, map.Get(7));
        }

        [Test]
        public void Tick_IndependentPerBarracks()
        {
            var map = new BarracksCooldownMap();
            map.Set(1, 10f);
            map.Set(2, 0.02f);
            map.Tick(0.05f);
            Assert.AreEqual(9.95f, map.Get(1), 0.001f);
            Assert.AreEqual(0f, map.Get(2));
        }
    }
}
