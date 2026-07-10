using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RacePickSessionTests
    {
        [Test]
        public void ConfirmOfflinePick_FillsOtherSlotsWithRandomPlayableRaces()
        {
            var session = new RacePickSession(4, localPlayerSlot: 0);
            session.SetLocalPick(GameIds.Races.Human);
            session.ConfirmOfflinePick(new Random(42));

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(GameIds.Races.Human, session.GetRaceId(0));

            for (var slot = 1; slot < 4; slot++)
            {
                Assert.IsTrue(RacePickRules.IsPlayable(session.GetRaceId(slot)));
            }
        }

        [Test]
        public void ConfirmOfflinePick_WithoutLocalPick_Throws()
        {
            var session = new RacePickSession(2, localPlayerSlot: 0);
            Assert.Throws<InvalidOperationException>(() => session.ConfirmOfflinePick(new Random(1)));
        }

        [Test]
        public void ToRaceIdsArray_RequiresCompleteSession()
        {
            var session = new RacePickSession(2, localPlayerSlot: 1);
            session.SetLocalPick(GameIds.Races.Bug);
            Assert.Throws<InvalidOperationException>(() => session.ToRaceIdsArray());

            session.ConfirmOfflinePick(new Random(7));
            var raceIds = session.ToRaceIdsArray();
            Assert.AreEqual(2, raceIds.Length);
            Assert.AreEqual(GameIds.Races.Bug, raceIds[1]);
        }

        [Test]
        public void PickRandomRace_ReturnsPlayableRace()
        {
            var raceId = RacePickRules.PickRandomRace(new Random(3));
            Assert.IsTrue(RacePickRules.IsPlayable(raceId));
        }
    }
}
