using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameUpdateVersionRulesTests
    {
        [Test]
        public void IsUpdateRequired_WhenLocalOlder_ReturnsTrue()
        {
            Assert.IsTrue(GameUpdateVersionRules.IsUpdateRequired("0.3.0", "0.3.1"));
            Assert.IsFalse(GameUpdateVersionRules.CanPlay("0.3.0", "0.3.1"));
        }

        [Test]
        public void IsUpdateRequired_WhenLocalEqualOrNewer_ReturnsFalse()
        {
            Assert.IsFalse(GameUpdateVersionRules.IsUpdateRequired("0.3.1", "0.3.1"));
            Assert.IsFalse(GameUpdateVersionRules.IsUpdateRequired("0.4.0", "0.3.1"));
            Assert.IsTrue(GameUpdateVersionRules.CanPlay("0.3.1", "0.3.1"));
        }

        [Test]
        public void TryParseSemVer_AcceptsVPrefix()
        {
            Assert.IsTrue(GameUpdateVersionRules.TryParseSemVer("v1.2.3", out var version));
            Assert.AreEqual(new System.Version(1, 2, 3), version);
        }
    }
}
