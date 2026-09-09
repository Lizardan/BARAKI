using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// PRE-001: two-window bonus state on MatchController + snapshot round-trip.
    /// Panel 0 = auto-fate pick, panel 1 = choice from the offered subset.
    /// The window opens (auto roll + 60s deadline) during the early-phase transition,
    /// never at match start — so tests open it explicitly via <see cref="OpenWindow"/>.
    /// </summary>
    public sealed class MatchBonusPickTests
    {
        private static MatchConfig AutoFateConfig() => MatchConfig.MvpDefault(2, autoFateBonuses: true);

        private static MatchController OpenWindow()
        {
            var controller = new MatchController();
            controller.StartMatch(AutoFateConfig());
            controller.BeginEarlyPhase();
            return controller;
        }

        [Test]
        public void StartMatch_DoesNotOpenWindowYet()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            // The two windows appear only after the base-focus fly-in (early phase).
            Assert.AreEqual(0f, controller.BonusPickDeadlineSeconds, 0.01f);
            for (var slot = 0; slot < controller.Players.Count; slot++)
            {
                Assert.AreEqual(BonusPickRules.NoneSlot, controller.GetBonusPickSlot(slot));
                Assert.AreEqual(BonusPickRules.NoneSlot, controller.GetBonusPickSlot(slot, panel: 1));
                Assert.AreEqual(0, controller.GetBonusPickOffer(slot).Length);
            }
        }

        [Test]
        public void BeginEarlyPhase_OpensPickWindow()
        {
            var controller = OpenWindow();

            Assert.AreEqual(BonusPickRules.OverlayDurationSeconds, controller.BonusPickDeadlineSeconds, 0.01f);

            // Auto-fate pick (panel 0) is set for every player once the windows open; choice (panel 1) is pending.
            for (var slot = 0; slot < controller.Players.Count; slot++)
            {
                var auto = controller.GetBonusPickSlot(slot);
                var offer = controller.GetBonusPickOffer(slot);
                Assert.IsTrue(BonusPickRules.IsValidSlot(auto), $"slot {slot} auto");
                Assert.AreEqual(BonusPickRules.NoneSlot, controller.GetBonusPickSlot(slot, panel: 1));
                Assert.AreEqual(BonusPickRules.OfferSize, offer.Length);
                Assert.IsFalse(BonusPickRules.IsSlotInOffer(offer, auto), "auto pick must not be offered");
            }
        }

        [Test]
        public void BeginEarlyPhase_IsIdempotent()
        {
            var controller = OpenWindow();
            controller.BeginEarlyPhase();
            controller.BeginEarlyPhase();

            Assert.AreEqual(BonusPickRules.OverlayDurationSeconds, controller.BonusPickDeadlineSeconds, 0.01f);
            Assert.IsTrue(BonusPickRules.IsValidSlot(controller.GetBonusPickSlot(0)));
            Assert.AreEqual(BonusPickRules.OfferSize, controller.GetBonusPickOffer(0).Length);
        }

        [Test]
        public void TrySetBonusPick_AppliesExactlyOnceFromOffer()
        {
            var controller = OpenWindow();

            var pick = controller.GetBonusPickOffer(0)[0];
            Assert.IsTrue(controller.TrySetBonusPick(0, pick));
            Assert.AreEqual(pick, controller.GetBonusPickSlot(0, panel: 1));

            // Second pick in the same window is rejected.
            var other = controller.GetBonusPickOffer(0)[1];
            Assert.IsFalse(controller.TrySetBonusPick(0, other));
            Assert.AreEqual(pick, controller.GetBonusPickSlot(0, panel: 1));
        }

        [Test]
        public void TrySetBonusPick_RejectsSlotOutsideOffer()
        {
            var controller = OpenWindow();

            // The auto-fate pick is never part of the offer.
            var auto = controller.GetBonusPickSlot(0);
            Assert.IsFalse(controller.TrySetBonusPick(0, auto));
            Assert.AreEqual(BonusPickRules.NoneSlot, controller.GetBonusPickSlot(0, panel: 1));
        }

        [Test]
        public void TrySetBonusPick_RejectsInvalidSlot()
        {
            var controller = OpenWindow();

            Assert.IsFalse(controller.TrySetBonusPick(0, BonusPickRules.NoneSlot));
            Assert.IsFalse(controller.TrySetBonusPick(0, BonusPickRules.SlotCount + 1));
            Assert.IsFalse(controller.TrySetBonusPick(2, 1));
        }

        [Test]
        public void TrySetBonusPick_RejectedAfterDeadline()
        {
            var controller = OpenWindow();

            controller.Tick(BonusPickRules.OverlayDurationSeconds + 1f);

            Assert.AreEqual(0f, controller.BonusPickDeadlineSeconds, 0.01f);
            Assert.IsFalse(controller.TrySetBonusPick(0, 1));
        }

        [Test]
        public void Tick_Timeout_FillsRandomPickFromOfferForAll()
        {
            var controller = OpenWindow();

            controller.Tick(BonusPickRules.OverlayDurationSeconds + 1f);

            Assert.AreEqual(0f, controller.BonusPickDeadlineSeconds, 0.01f);
            for (var slot = 0; slot < controller.Players.Count; slot++)
            {
                var pick = controller.GetBonusPickSlot(slot, panel: 1);
                Assert.IsTrue(
                    BonusPickRules.IsSlotInOffer(controller.GetBonusPickOffer(slot), pick),
                    $"slot {slot} pick {pick}");
            }
        }

        [Test]
        public void Timeout_DoesNotOverwritePick()
        {
            var controller = OpenWindow();

            var pick = controller.GetBonusPickOffer(0)[0];
            Assert.IsTrue(controller.TrySetBonusPick(0, pick));
            controller.Tick(BonusPickRules.OverlayDurationSeconds + 1f);

            Assert.AreEqual(pick, controller.GetBonusPickSlot(0, panel: 1));
            Assert.IsTrue(BonusPickRules.IsSlotInOffer(
                controller.GetBonusPickOffer(1),
                controller.GetBonusPickSlot(1, panel: 1)));
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_RestoresPicksAndDeadline()
        {
            var host = OpenWindow();
            var pick = host.GetBonusPickOffer(1)[1];
            Assert.IsTrue(host.TrySetBonusPick(1, pick));
            host.Tick(20f);

            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(host));

            // A pure client hasn't reached the early phase itself; the first snapshot carries the state.
            var client = new MatchController();
            client.StartMatch(AutoFateConfig());
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Deserialize(bytes));

            Assert.AreEqual(host.GetBonusPickSlot(1), client.GetBonusPickSlot(1));
            Assert.AreEqual(pick, client.GetBonusPickSlot(1, panel: 1));
            CollectionAssert.AreEqual(host.GetBonusPickOffer(1), client.GetBonusPickOffer(1));
            Assert.AreEqual(host.BonusPickDeadlineSeconds, client.BonusPickDeadlineSeconds, 0.01f);
        }
    }
}