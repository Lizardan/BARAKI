using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RacePickSessionTests
    {
        [Test]
        public void ConfirmOfflinePick_FillsOtherSlotsWithSelectableRaces()
        {
            var session = new RacePickSession(4, localPlayerSlot: 0);
            session.SetLocalPick(GameIds.Races.Human);
            session.ConfirmOfflinePick(new Random(42));

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(GameIds.Races.Human, session.GetRaceId(0));

            for (var slot = 1; slot < 4; slot++)
            {
                Assert.IsTrue(RacePickRules.IsSelectable(session.GetRaceId(slot)));
            }
        }

        [Test]
        public void ConfirmOfflinePick_WithoutLocalPick_Throws()
        {
            var session = new RacePickSession(2, localPlayerSlot: 0);
            Assert.Throws<InvalidOperationException>(() => session.ConfirmOfflinePick(new Random(1)));
        }

        [Test]
        public void SetLocalPick_Bug_NotSelectable_Throws()
        {
            var session = new RacePickSession(2, localPlayerSlot: 0);
            Assert.Throws<ArgumentException>(() => session.SetLocalPick(GameIds.Races.Bug));
        }

        [Test]
        public void ToRaceIdsArray_RequiresCompleteSession()
        {
            var session = new RacePickSession(2, localPlayerSlot: 1);
            session.SetLocalPick(GameIds.Races.Human);
            Assert.Throws<InvalidOperationException>(() => session.ToRaceIdsArray());

            session.ConfirmOfflinePick(new Random(7));
            var raceIds = session.ToRaceIdsArray();
            Assert.AreEqual(2, raceIds.Length);
            Assert.AreEqual(GameIds.Races.Human, raceIds[1]);
        }

        [Test]
        public void PickRandomRace_ReturnsSelectableRace()
        {
            var raceId = RacePickRules.PickRandomRace(new Random(3));
            Assert.IsTrue(RacePickRules.IsSelectable(raceId));
            Assert.IsFalse(RacePickRules.IsSelectable(GameIds.Races.Bug));
            Assert.IsTrue(RacePickRules.IsPlayable(GameIds.Races.Bug));
        }

        [Test]
        public void NetworkRules_RejectsBugPick()
        {
            var picks = new string[2];
            Assert.IsFalse(RacePickNetworkRules.TryApplyPick(picks, 0, GameIds.Races.Bug));
            Assert.IsTrue(RacePickNetworkRules.TryApplyPick(picks, 0, GameIds.Races.Human));
            Assert.IsTrue(RacePickNetworkRules.TryApplyPick(picks, 1, GameIds.Races.Human));
            Assert.IsTrue(RacePickNetworkRules.IsComplete(picks));
        }

        [Test]
        public void NetworkRules_IsComplete_FalseUntilEverySlotPicked()
        {
            var picks = new string[2];
            picks[0] = GameIds.Races.Human;

            Assert.IsFalse(RacePickNetworkRules.IsComplete(picks));

            picks[1] = GameIds.Races.Human;

            Assert.IsTrue(RacePickNetworkRules.IsComplete(picks));
        }
    }
}
