using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BarracksWaveSchedulerTests
    {
        [Test]
        public void GetWaveInterval_L1_ReturnsBaseInterval()
        {
            var interval = BarracksWaveRules.GetWaveIntervalSeconds(1, false, GameIds.Races.Human);
            Assert.AreEqual(35f, interval, 0.01f);
        }

        [Test]
        public void GetWaveInterval_L2_ReturnsFasterSpawn()
        {
            var interval = BarracksWaveRules.GetWaveIntervalSeconds(2, false, GameIds.Races.Human);
            Assert.AreEqual(33.33f, interval, 0.05f);
        }

        [Test]
        public void GetWaveInterval_Ruins_UsesBaseInterval()
        {
            var interval = BarracksWaveRules.GetWaveIntervalSeconds(4, true, GameIds.Races.Human);
            Assert.AreEqual(35f, interval, 0.01f);
        }

        [Test]
        public void GetWaveInterval_BugRace_AppliesBroodSurge()
        {
            var interval = BarracksWaveRules.GetWaveIntervalSeconds(1, false, GameIds.Races.Bug);
            Assert.AreEqual(31.82f, interval, 0.05f);
        }

        [Test]
        public void Initialize_AllL1HumanBarracks_StartWithSameInterval()
        {
            var scheduler = new BarracksWaveScheduler();
            var players = new List<MatchPlayerState>
            {
                new(0, GameIds.Races.Human, 500),
                new(1, GameIds.Races.Human, 500),
            };
            scheduler.Initialize(players);

            foreach (var barracks in scheduler.Barracks)
            {
                Assert.AreEqual(35f, barracks.TimeUntilNextWaveSeconds, 0.01f);
                Assert.AreEqual(35f, barracks.WaveIntervalSeconds, 0.01f);
            }
        }

        [Test]
        public void Initialize_BugBarracks_StartWithFasterInterval()
        {
            var scheduler = new BarracksWaveScheduler();
            scheduler.Initialize(new List<MatchPlayerState> { new(0, GameIds.Races.Bug, 500) });

            foreach (var barracks in scheduler.Barracks)
            {
                Assert.AreEqual(31.82f, barracks.TimeUntilNextWaveSeconds, 0.05f);
            }
        }

        [Test]
        public void Initialize_N4_CreatesTwelveBarracksTimers()
        {
            var scheduler = new BarracksWaveScheduler();
            scheduler.Initialize(CreatePlayers(4));

            Assert.AreEqual(12, scheduler.Barracks.Count);
        }

        [Test]
        public void Tick_FiresWaveAfterInterval()
        {
            var scheduler = new BarracksWaveScheduler();
            scheduler.Initialize(CreatePlayers(2));
            scheduler.Activate();

            var center = FindBarracks(scheduler, 0, GameIds.Buildings.BarracksCenter);
            center.TimeUntilNextWaveSeconds = 1f;

            var fired = false;
            scheduler.WaveFired += _ => fired = true;

            scheduler.Tick(1.1f);

            Assert.IsTrue(fired);
        }

        [Test]
        public void SetBarracksRuins_UsesFrozenSquadAndBaseInterval()
        {
            var scheduler = new BarracksWaveScheduler();
            scheduler.Initialize(CreatePlayers(2));
            scheduler.SetBarracksLevel(0, GameIds.Buildings.BarracksLeft, 3);
            scheduler.SetBarracksRuins(0, GameIds.Buildings.BarracksLeft);

            var barracks = FindBarracks(scheduler, 0, GameIds.Buildings.BarracksLeft);
            Assert.IsTrue(barracks.IsRuins);
            Assert.AreEqual(3, barracks.EffectiveSquadLevel);
            Assert.AreEqual(35f, barracks.WaveIntervalSeconds, 0.01f);
        }

        [Test]
        public void MatchController_ActivatesSchedulerWhenEarlyPhaseBegins()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();

            Assert.AreEqual(MatchPhase.Early, controller.Phase);
            Assert.IsTrue(controller.WaveScheduler.IsActive);
        }

        [Test]
        public void MatchController_DoesNotTickWavesDuringStartPhase()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var fired = false;
            controller.WaveFired += _ => fired = true;
            controller.Tick(1f);

            Assert.AreEqual(MatchPhase.Start, controller.Phase);
            Assert.IsFalse(fired);
        }

        private static List<MatchPlayerState> CreatePlayers(int count)
        {
            var players = new List<MatchPlayerState>(count);
            for (var slot = 0; slot < count; slot++)
            {
                var raceId = slot % 2 == 0 ? GameIds.Races.Human : GameIds.Races.Bug;
                players.Add(new MatchPlayerState(slot, raceId, 500));
            }

            return players;
        }

        private static BarracksWaveState FindBarracks(
            BarracksWaveScheduler scheduler,
            int ownerSlot,
            string barracksId)
        {
            for (var i = 0; i < scheduler.Barracks.Count; i++)
            {
                var barracks = scheduler.Barracks[i];
                if (barracks.OwnerSlot == ownerSlot && barracks.BarracksId == barracksId)
                {
                    return barracks;
                }
            }

            return null;
        }
    }
}
