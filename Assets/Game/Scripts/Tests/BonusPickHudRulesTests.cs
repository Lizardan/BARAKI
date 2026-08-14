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
        public void TryRequestPick_NoBridge_ReturnsFalse()
        {
            Assert.IsFalse(BonusPickNetworkFacade.TryRequestPick(3));
        }
    }
}
