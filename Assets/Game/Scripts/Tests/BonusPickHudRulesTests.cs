using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>PRE-001: bonus overlay data-source rules (snapshot vs controller).</summary>
    public sealed class BonusPickHudRulesTests
    {
        [Test]
        public void TryGetSnapshotBonusPick_FindsLocalSlot()
        {
            var snapshot = new MatchSnapshot
            {
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, BonusPickSlot = 7 },
                    new MatchPlayerSnapshot { Slot = 1, BonusPickSlot = 0 },
                },
            };

            Assert.IsTrue(BonusPickHudRules.TryGetSnapshotBonusPick(snapshot, 0, out var slot));
            Assert.AreEqual(7, slot);
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

            Assert.IsFalse(BonusPickHudRules.TryGetSnapshotBonusPick(snapshot, 4, out var slot));
            Assert.AreEqual(BonusPickRules.NoneSlot, slot);
        }

        [Test]
        public void TryGetSnapshotBonusPick_NullSnapshot_False()
        {
            Assert.IsFalse(BonusPickHudRules.TryGetSnapshotBonusPick(null, 0, out var slot));
            Assert.AreEqual(BonusPickRules.NoneSlot, slot);
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
        public void IsPickWindowOpen_OpenWhileNoPickAndDeadlineCounting()
        {
            Assert.IsTrue(BonusPickHudRules.IsPickWindowOpen(60f, BonusPickRules.NoneSlot));
        }

        [Test]
        public void IsPickWindowOpen_ClosedAfterDeadline()
        {
            Assert.IsFalse(BonusPickHudRules.IsPickWindowOpen(0f, BonusPickRules.NoneSlot));
        }

        [Test]
        public void IsPickWindowOpen_ClosedAfterOwnPick()
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
                controllerPick: BonusPickRules.NoneSlot,
                out var deadline,
                out var ownPick);

            Assert.AreEqual(55f, deadline, 0.01f);
            Assert.AreEqual(BonusPickRules.NoneSlot, ownPick);
            Assert.IsTrue(BonusPickHudRules.IsPickWindowOpen(deadline, ownPick));
        }

        [Test]
        public void ResolveOverlay_ClientWithSnapshot_PrefersSnapshot()
        {
            var snapshot = new MatchSnapshot
            {
                BonusPickDeadlineSeconds = 12f,
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 1, BonusPickSlot = 4 },
                },
            };

            BonusPickHudRules.ResolveOverlay(
                useSnapshot: true,
                snapshot,
                localSlot: 1,
                controllerDeadline: 60f,
                controllerPick: BonusPickRules.NoneSlot,
                out var deadline,
                out var ownPick);

            Assert.AreEqual(12f, deadline, 0.01f);
            Assert.AreEqual(4, ownPick);
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
                controllerPick: BonusPickRules.NoneSlot,
                out var deadline,
                out var ownPick);

            Assert.AreEqual(40f, deadline, 0.01f);
            Assert.AreEqual(BonusPickRules.NoneSlot, ownPick);
        }

        [Test]
        public void TryRequestPick_NoBridge_ReturnsFalse()
        {
            Assert.IsFalse(BonusPickNetworkFacade.TryRequestPick(3));
        }
    }
}
