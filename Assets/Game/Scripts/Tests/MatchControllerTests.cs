using System.Linq;
using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchControllerTests
    {
        [Test]
        public void StartMatch_N2_UsesDuelTopologyAndSixLanes()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            Assert.AreEqual(MatchPhase.Start, controller.Phase);
            Assert.AreEqual(TopologyKind.Duel, controller.Layout.Topology);
            Assert.AreEqual(2, controller.Players.Count);
            Assert.AreEqual(6, controller.Layout.Lanes.Count);
            Assert.IsNotNull(controller.Graph);
        }

        [Test]
        public void StartMatch_GrantsStartingGoldPerRace()
        {
            var config = new MatchConfig(
                2,
                new[] { GameIds.Races.Human, GameIds.Races.Bug });

            var controller = new MatchController();
            controller.StartMatch(config);

            Assert.AreEqual(250, controller.Players[0].Gold);
            Assert.AreEqual(500, controller.Players[1].Gold);
        }

        [Test]
        public void BeginEarlyPhase_TransitionsToEarlyAndActivatesWaves()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            Assert.AreEqual(MatchPhase.Start, controller.Phase);
            Assert.IsFalse(controller.WaveScheduler.IsActive);

            controller.BeginEarlyPhase();

            Assert.AreEqual(MatchPhase.Early, controller.Phase);
            Assert.IsTrue(controller.WaveScheduler.IsActive);
            Assert.AreEqual(0f, controller.MatchTimeSeconds);
        }

        [Test]
        public void Tick_MatchTime_TransitionsEarlyMidLate()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            controller.Tick(MatchRules.EarlyEndSeconds - 0.1f);
            Assert.AreEqual(MatchPhase.Early, controller.Phase);

            controller.Tick(0.2f);
            Assert.AreEqual(MatchPhase.Mid, controller.Phase);

            controller.Tick(MatchRules.MidEndSeconds - MatchRules.EarlyEndSeconds);
            Assert.AreEqual(MatchPhase.Late, controller.Phase);
        }

        [Test]
        public void EndMatch_SetsWinnerAndPhaseEnd()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var ended = false;
            var winner = -1;
            controller.MatchEnded += slot =>
            {
                ended = true;
                winner = slot;
            };

            controller.EndMatch(2);

            Assert.IsTrue(ended);
            Assert.AreEqual(2, winner);
            Assert.AreEqual(MatchPhase.End, controller.Phase);
            Assert.AreEqual(2, controller.WinnerSlot);
        }

        [Test]
        public void PhaseChanged_FiresOnTransitions()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var transitions = 0;
            controller.PhaseChanged += (_, _) => transitions++;

            controller.BeginEarlyPhase();
            Assert.AreEqual(1, transitions);
            Assert.AreEqual(MatchRules.ToPhaseId(MatchPhase.Early), MatchRules.ToPhaseId(controller.Phase));
        }

        [Test]
        public void MatchConfig_FromSetup_UsesDefaultRacesWhenNull()
        {
            var setup = new MatchSetup(4);
            var config = MatchConfig.FromSetup(setup);

            Assert.AreEqual(4, config.PlayerCount);
            Assert.AreEqual(4, config.RaceIds.Count);
            Assert.AreEqual(GameIds.Races.Human, config.RaceIds[0]);
            Assert.AreEqual(GameIds.Races.Bug, config.RaceIds[1]);
        }

        [Test]
        public void StartMatch_N4_RingTopologyAndPlayerSlots()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            Assert.AreEqual(TopologyKind.Ring, controller.Layout.Topology);
            Assert.AreEqual(4, controller.Players.Count);
            Assert.IsTrue(controller.Players.All(player => player.Gold > 0));
        }

        [Test]
        public void StartMatch_InitializesBuildingsForAllPlayers()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            Assert.AreEqual(32, controller.Buildings.Buildings.Count);
            Assert.AreEqual(8, controller.Buildings.CountIntactBuildings(0));
        }

        [Test]
        public void Elimination_EndsMatchWhenOnlyOnePlayerRemains()
        {
            var controller = new MatchController();
            var ended = false;
            var winner = -1;
            controller.MatchEnded += slot =>
            {
                ended = true;
                winner = slot;
            };

            controller.StartMatch(MatchConfig.MvpDefault(2));

            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == 1)
                {
                    controller.Buildings.TryApplyDamage(building.InstanceId, 99999f, 0);
                }
            }

            Assert.IsTrue(ended);
            Assert.AreEqual(0, winner);
            Assert.AreEqual(MatchPhase.End, controller.Phase);
            Assert.IsTrue(controller.Players[1].IsEliminated);
        }
    }
}
