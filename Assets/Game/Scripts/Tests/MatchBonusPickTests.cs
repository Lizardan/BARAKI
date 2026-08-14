using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>PRE-001: bonus pick state on MatchController + snapshot v13 round-trip.</summary>
    public sealed class MatchBonusPickTests
    {
        [Test]
        public void StartMatch_OpensPickWindow()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            Assert.AreEqual(BonusPickRules.OverlayDurationSeconds, controller.BonusPickDeadlineSeconds, 0.01f);
            Assert.AreEqual(BonusPickRules.NoneSlot, controller.GetBonusPickSlot(0));
            Assert.AreEqual(BonusPickRules.NoneSlot, controller.GetBonusPickSlot(1));
        }

        [Test]
        public void TrySetBonusPick_AppliesExactlyOnce()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            Assert.IsTrue(controller.TrySetBonusPick(0, 3));
            Assert.AreEqual(3, controller.GetBonusPickSlot(0));

            Assert.IsFalse(controller.TrySetBonusPick(0, 5));
            Assert.AreEqual(3, controller.GetBonusPickSlot(0));
        }

        [Test]
        public void TrySetBonusPick_RejectsInvalidSlot()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            Assert.IsFalse(controller.TrySetBonusPick(0, BonusPickRules.NoneSlot));
            Assert.IsFalse(controller.TrySetBonusPick(0, BonusPickRules.SlotCount + 1));
            Assert.IsFalse(controller.TrySetBonusPick(2, 1));
        }

        [Test]
        public void TrySetBonusPick_RejectedAfterDeadline()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            controller.Tick(BonusPickRules.OverlayDurationSeconds + 1f);

            Assert.AreEqual(0f, controller.BonusPickDeadlineSeconds, 0.01f);
            Assert.IsFalse(controller.TrySetBonusPick(0, 1));
        }

        [Test]
        public void Tick_Timeout_FillsRandomPicksForAll()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            controller.Tick(BonusPickRules.OverlayDurationSeconds + 1f);

            Assert.AreEqual(0f, controller.BonusPickDeadlineSeconds, 0.01f);
            for (var slot = 0; slot < controller.Players.Count; slot++)
            {
                Assert.IsTrue(BonusPickRules.IsValidSlot(controller.GetBonusPickSlot(slot)));
            }
        }

        [Test]
        public void Timeout_DoesNotOverwriteManualPick()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            Assert.IsTrue(controller.TrySetBonusPick(0, 7));
            controller.Tick(BonusPickRules.OverlayDurationSeconds + 1f);

            Assert.AreEqual(7, controller.GetBonusPickSlot(0));
            Assert.IsTrue(BonusPickRules.IsValidSlot(controller.GetBonusPickSlot(1)));
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_RestoresPicksAndDeadline()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            Assert.IsTrue(host.TrySetBonusPick(1, 4));
            host.Tick(20f);

            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(host));

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Deserialize(bytes));

            Assert.AreEqual(4, client.GetBonusPickSlot(1));
            Assert.AreEqual(host.BonusPickDeadlineSeconds, client.BonusPickDeadlineSeconds, 0.01f);
        }
    }
}
