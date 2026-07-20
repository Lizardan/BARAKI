using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CenterMarchRetargetRulesTests
    {
        [Test]
        public void ResolveNextAliveClockwise_SkipsEliminatedSlots()
        {
            var players = CreatePlayers(4);
            players[1].IsEliminated = true;
            players[2].IsEliminated = true;

            Assert.AreEqual(3, CenterMarchRetargetRules.ResolveNextAliveClockwise(1, players, ownerSlot: 0));
            Assert.AreEqual(3, CenterMarchRetargetRules.ResolveNextAliveClockwise(2, players, ownerSlot: 0));
            Assert.AreEqual(0, CenterMarchRetargetRules.ResolveNextAliveClockwise(3, players, ownerSlot: 1));
        }

        [Test]
        public void ResolveNextAliveClockwise_SkipsOwnerSlot()
        {
            var players = CreatePlayers(4);
            players[2].IsEliminated = true;
            players[3].IsEliminated = true;

            Assert.AreEqual(1, CenterMarchRetargetRules.ResolveNextAliveClockwise(2, players, ownerSlot: 0));
        }

        [Test]
        public void ResolveNextAliveClockwise_ReturnsNullWhenNoAliveRemain()
        {
            var players = CreatePlayers(2);
            players[0].IsEliminated = true;
            players[1].IsEliminated = true;

            Assert.IsNull(CenterMarchRetargetRules.ResolveNextAliveClockwise(0, players));
        }

        [Test]
        public void CenterPath_IsOpenTowardEnemyNotClosedLoop()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Center, out var lane));

            Assert.IsFalse(lane.Path.IsClosedLoop);
            var enemyMain = Flat(layout.Slots[lane.OpponentSlot].GetBuildingWorldPosition(GameIds.Buildings.Main));
            Assert.Less(Vector3.Distance(Flat(lane.Path.End), enemyMain), 0.5f);
        }

        [Test]
        public void EliminateCenterTarget_RetargetsOpponentClockwiseAndRebuildsPath()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var oldOpponent = lane.OpponentSlot;
            Assert.AreEqual(controller.Layout.Slots[attacker].CenterPrimaryTargetSlot, oldOpponent);

            RuinAllBuildings(controller, oldOpponent);

            Assert.IsTrue(controller.Players[oldOpponent].IsEliminated);
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out lane));
            var expected = CenterMarchRetargetRules.ResolveNextAliveClockwise(oldOpponent, controller.Players);
            Assert.AreEqual(expected, lane.OpponentSlot);
            Assert.AreNotEqual(oldOpponent, lane.OpponentSlot);

            var newMain = Flat(controller.Layout.Slots[lane.OpponentSlot]
                .GetBuildingWorldPosition(GameIds.Buildings.Main));
            Assert.Less(Vector3.Distance(Flat(lane.Path.End), newMain), 0.5f);
        }

        [Test]
        public void EliminateCenterTarget_RemountsCenterUnitsTowardNewEnemy()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var oldOpponent = lane.OpponentSlot;

            var stats = new UnitCombatStats(
                UnitRole.Melee, 200f, 0f, 1f, 1f, 1f, 1.5f, 8f, 1);
            var meet = lane.Path.TotalLength * 0.85f;
            var unit = controller.Combat.SpawnUnit(
                attacker, GameIds.Lanes.Center, UnitRole.Melee, stats, meet);
            unit.WorldPosition = lane.Path.EvaluateDistance(meet);
            var positionBefore = Flat(unit.WorldPosition);

            RuinAllBuildings(controller, oldOpponent);

            Assert.AreEqual(positionBefore, Flat(unit.WorldPosition),
                "Retarget must not teleport the unit onto the new path.");

            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out lane));
            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, GameIds.Lanes.Center, out var route));
            Assert.AreEqual(lane.Path.TotalLength, route.TotalLength, 0.01f);

            // Ruin new target buildings too so unit only marches (no attack lock).
            RuinAllBuildings(controller, lane.OpponentSlot);

            const float deltaTime = 0.25f;
            var prev = Flat(unit.WorldPosition);
            for (var i = 0; i < 60; i++)
            {
                controller.Combat.Tick(deltaTime);
                var now = Flat(unit.WorldPosition);
                Assert.LessOrEqual(
                    Vector3.Distance(prev, now),
                    unit.MarchMoveSpeed * deltaTime * 1.05f,
                    $"Teleport on tick {i}");
                prev = now;
            }

            // Second wipe may retarget again; unit should still keep moving on center.
            Assert.Greater(Vector3.Distance(positionBefore, Flat(unit.WorldPosition)), 8f);
            Assert.AreEqual(GameIds.Lanes.Center, unit.LaneId);
        }

        static List<MatchPlayerState> CreatePlayers(int count)
        {
            var players = new List<MatchPlayerState>(count);
            for (var i = 0; i < count; i++)
            {
                players.Add(new MatchPlayerState(i, GameIds.Races.Human, startingGold: 0));
            }

            return players;
        }

        static void RuinAllBuildings(MatchController controller, int slot)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == slot && building.IsIntact)
                {
                    controller.Buildings.TryApplyDamage(building.InstanceId, 99999f, attackerOwnerSlot: 0);
                }
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
