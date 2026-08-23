using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LauncherProgressBarRulesTests
    {
        [Test]
        public void StepDisplayed_CatchesUpTowardOne()
        {
            var next = LauncherProgressBarRules.StepDisplayed(0.5f, 1f, 0.25f);
            Assert.Greater(next, 0.5f);
            Assert.Less(next, 1f);
            Assert.AreEqual(0.5f + LauncherProgressBarRules.CatchUpPerSecond * 0.25f, next, 0.0001f);
        }

        [Test]
        public void StepDisplayed_DoesNotOvershootTarget()
        {
            var next = LauncherProgressBarRules.StepDisplayed(0.99f, 1f, 1f);
            Assert.AreEqual(1f, next, 0.0001f);
        }

        [Test]
        public void HasCaughtUp_TrueAtTarget()
        {
            Assert.IsFalse(LauncherProgressBarRules.HasCaughtUp(0.5f, 1f));
            Assert.IsTrue(LauncherProgressBarRules.HasCaughtUp(1f, 1f));
            Assert.IsTrue(LauncherProgressBarRules.HasCaughtUp(0.997f, 1f));
        }

        [Test]
        public void StepDisplayed_ReachesFullBarFromHalfway()
        {
            var displayed = 0.5f;
            var steps = 0;
            while (!LauncherProgressBarRules.HasCaughtUp(displayed, 1f) && steps < 200)
            {
                displayed = LauncherProgressBarRules.StepDisplayed(displayed, 1f, 0.016f);
                steps++;
            }

            Assert.Greater(steps, 1);
            Assert.IsTrue(LauncherProgressBarRules.HasCaughtUp(displayed, 1f));
        }
    }
}
