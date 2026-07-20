using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchResearchQueueTests
    {
        [Test]
        public void TryEnqueue_AllowsUpToThreeAndRejectsFourth()
        {
            var queue = new MatchResearchQueue();
            Assert.IsTrue(queue.TryEnqueue(Make(1, "A")));
            Assert.IsTrue(queue.TryEnqueue(Make(1, "B")));
            Assert.IsTrue(queue.TryEnqueue(Make(1, "C")));
            Assert.IsFalse(queue.TryEnqueue(Make(1, "D")));
            Assert.AreEqual(3, queue.GetCount(1));
        }

        [Test]
        public void Tick_CompletesHeadAndPromotesNext()
        {
            var queue = new MatchResearchQueue();
            queue.TryEnqueue(Make(1, "A", duration: 10f));
            queue.TryEnqueue(Make(1, "B", duration: 20f));

            var completed = queue.Tick(10f);
            Assert.AreEqual(1, completed.Count);
            Assert.AreEqual("A", completed[0].UpgradeId);
            Assert.IsTrue(queue.TryGetActive(1, out var active));
            Assert.AreEqual("B", active.UpgradeId);
            Assert.AreEqual(20f, active.RemainingSeconds, 0.001f);
        }

        [Test]
        public void CountUpgrade_CountsMatchingIds()
        {
            var queue = new MatchResearchQueue();
            queue.TryEnqueue(Make(1, GameIds.Upgrades.MainPassiveGold));
            queue.TryEnqueue(Make(1, GameIds.Upgrades.MainPassiveGold));
            queue.TryEnqueue(Make(1, "OTHER"));
            Assert.AreEqual(2, queue.CountUpgrade(1, GameIds.Upgrades.MainPassiveGold));
        }

        static BuildingResearchState Make(int buildingId, string upgradeId, float duration = 25f) =>
            new(buildingId, ownerSlot: 0, buildingId: "B", upgradeId, costPaid: 1, duration);
    }
}
