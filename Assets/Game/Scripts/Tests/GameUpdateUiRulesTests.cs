using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameUpdateUiRulesTests
    {
        [Test]
        public void FormatVersionLabel_AddsVPrefix()
        {
            Assert.AreEqual("v0.1.2", GameUpdateUiRules.FormatVersionLabel("0.1.2"));
            Assert.AreEqual("v0.1.2", GameUpdateUiRules.FormatVersionLabel("v0.1.2"));
        }

        [Test]
        public void ProgressPercent_ClampsAndRounds()
        {
            Assert.AreEqual(0, GameUpdateUiRules.ProgressPercent(-1f));
            Assert.AreEqual(50, GameUpdateUiRules.ProgressPercent(0.5f));
            Assert.AreEqual(100, GameUpdateUiRules.ProgressPercent(1.2f));
            Assert.AreEqual("37%", GameUpdateUiRules.FormatProgressLabel(0.374f));
        }
    }
}
