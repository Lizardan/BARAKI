using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class NetworkUnitVisualRulesTests
    {
        [Test]
        public void StepToward_MovesCloserWithoutOvershootingPastTargetOnLargeDt()
        {
            var current = Vector3.zero;
            var target = new Vector3(10f, 0f, 0f);
            var next = NetworkUnitVisualRules.StepToward(current, target, 0.05f);
            Assert.Greater(next.x, current.x);
            Assert.Less(next.x, target.x);
        }

        [Test]
        public void ShouldLerpPositions_OnlyOnClient()
        {
            Assert.IsFalse(NetworkUnitVisualRules.ShouldLerpPositions(MatchTickMode.Offline));
            Assert.IsFalse(NetworkUnitVisualRules.ShouldLerpPositions(MatchTickMode.Server));
            Assert.IsTrue(NetworkUnitVisualRules.ShouldLerpPositions(MatchTickMode.Client));
        }
    }
}
