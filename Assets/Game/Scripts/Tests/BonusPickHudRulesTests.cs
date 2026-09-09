using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>PRE-001: bonus overlay data-source rules (two windows: snapshot vs controller).</summary>
    public sealed class BonusPickHudRulesTests
    {
        [Test]
        public void TryGetSnapshotBonusPick_FindsLocalSlot()
        {
            var snapshot = new MatchSnapshot
            {
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, BonusPickSlot = 7, BonusPickSlot2 = 2 },
                    new MatchPlayerSnapshot { Slot = 1, BonusPickSlot = 0 },
                },
            };

            Assert.IsTrue(BonusPickHudRules.TryGetSnapshotBonusPick(snapshot, 0, panel: 0, out var auto));
            Assert.AreEqual(7, auto);
            Assert.IsTrue(BonusPickHudRules.TryGetSnapshotBonusPick(snapshot, 0, panel: 1, out var pick2));
            Assert.AreEqual(2, pick2);
        }

        [Test]
        public void TryGetSnapshotBonusPick_MissingSlot_ReturnsNone()
        {
            var snapshot = new MatchSnapshot
            {
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, BonusPickSlot = 3 },
                },
            };

            Assert.IsFalse(BonusPickHudRules.TryGetSnapshotBonusPick(snapshot, 4, panel: 0, out var slot));
            Assert.AreEqual(BonusPickRules.NoneSlot, slot);
        }

        [Test]
        public void TryGetSnapshotBonusPick_NullSnapshot_False()
        {
            Assert.IsFalse(BonusPickHudRules.TryGetSnapshotBonusPick(null, 0, panel: 0, out var slot));
            Assert.AreEqual(BonusPickRules.NoneSlot, slot);
        }

        [Test]
        public void TryGetSnapshotBonusOffer_ReturnsOffer()
        {
            var snapshot = new MatchSnapshot
            {
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, BonusPickOfferSlots = new[] { 1, 3, 5 } },
                },
            };

            Assert.IsTrue(BonusPickHudRules.TryGetSnapshotBonusOffer(snapshot, 0, out var offer));
            CollectionAssert.AreEqual(new[] { 1, 3, 5 }, offer);
        }

        [Test]
        public void TryGetSnapshotBonusOffer_MissingSlot_Empty()
        {
            var snapshot = new MatchSnapshot { Players = new[] { new MatchPlayerSnapshot { Slot = 0 } } };

            Assert.IsFalse(BonusPickHudRules.TryGetSnapshotBonusOffer(snapshot, 7, out var offer));
            Assert.AreEqual(0, offer.Length);
        }

        [Test]
        public void TryGetSnapshotDeadline_ReturnsValue()
        {
            var snapshot = new MatchSnapshot { BonusPickDeadlineSeconds = 42.5f };

            Assert.IsTrue(BonusPickHudRules.TryGetSnapshotDeadline(snapshot, out var deadline));
            Assert.AreEqual(42.5f, deadline, 0.01f);
        }

        [Test]
        public void TryGetSnapshotDeadline_Null_FalseAndZero()
        {
            Assert.IsFalse(BonusPickHudRules.TryGetSnapshotDeadline(null, out var deadline));
            Assert.AreEqual(0f, deadline, 0.01f);
        }

        [Test]
        public void IsPickWindowOpen_OpenWhileNoChoiceAndDeadlineCounting()
        {
            Assert.IsTrue(BonusPickHudRules.IsPickWindowOpen(60f, BonusPickRules.NoneSlot));
        }

        [Test]
        public void IsPickWindowOpen_ClosedAfterDeadline()
        {
            Assert.IsFalse(BonusPickHudRules.IsPickWindowOpen(0f, BonusPickRules.NoneSlot));
        }

        [Test]
        public void IsPickWindowOpen_ClosedAfterChoice()
        {
            Assert.IsFalse(BonusPickHudRules.IsPickWindowOpen(30f, 5));
        }

        [Test]
        public void ResolveOverlay_ClientWithoutSnapshot_FallsBackToController()
        {
            BonusPickHudRules.ResolveOverlay(
                useSnapshot: true,
                snapshot: null,
                localSlot: 0,
                controllerDeadline: 55f,
                controllerAutoPick: 3,
                controllerPick2: BonusPickRules.NoneSlot,
                controllerOffer: new[] { 1, 2 },
                out var deadline,
                out var autoPick,
                out var pick2,
                out var offer);

            Assert.AreEqual(55f, deadline, 0.01f);
            Assert.AreEqual(3, autoPick);
            Assert.AreEqual(BonusPickRules.NoneSlot, pick2);
            CollectionAssert.AreEqual(new[] { 1, 2 }, offer);
            Assert.IsTrue(BonusPickHudRules.IsPickWindowOpen(deadline, pick2));
        }

        [Test]
        public void ResolveOverlay_ClientWithSnapshot_PrefersSnapshot()
        {
            var snapshot = new MatchSnapshot
            {
                BonusPickDeadlineSeconds = 12f,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 1,
                        BonusPickSlot = 4,
                        BonusPickSlot2 = 9,
                        BonusPickOfferSlots = new[] { 5, 6 },
                    },
                },
            };

            BonusPickHudRules.ResolveOverlay(
                useSnapshot: true,
                snapshot,
                localSlot: 1,
                controllerDeadline: 60f,
                controllerAutoPick: 3,
                controllerPick2: BonusPickRules.NoneSlot,
                controllerOffer: new[] { 1, 2 },
                out var deadline,
                out var autoPick,
                out var pick2,
                out var offer);

            Assert.AreEqual(12f, deadline, 0.01f);
            Assert.AreEqual(4, autoPick);
            Assert.AreEqual(9, pick2);
            CollectionAssert.AreEqual(new[] { 5, 6 }, offer);
        }

        [Test]
        public void ResolveOverlay_HostIgnoresSnapshot()
        {
            var snapshot = new MatchSnapshot { BonusPickDeadlineSeconds = 1f };
            BonusPickHudRules.ResolveOverlay(
                useSnapshot: false,
                snapshot,
                localSlot: 0,
                controllerDeadline: 40f,
                controllerAutoPick: 7,
                controllerPick2: BonusPickRules.NoneSlot,
                controllerOffer: new[] { 8 },
                out var deadline,
                out var autoPick,
                out var pick2,
                out var offer);

            Assert.AreEqual(40f, deadline, 0.01f);
            Assert.AreEqual(7, autoPick);
            Assert.AreEqual(BonusPickRules.NoneSlot, pick2);
            CollectionAssert.AreEqual(new[] { 8 }, offer);
        }

        [Test]
        public void TryRequestPick_NoBridge_ReturnsFalse()
        {
            Assert.IsFalse(BonusPickNetworkFacade.TryRequestPick(3));
        }
    }
}