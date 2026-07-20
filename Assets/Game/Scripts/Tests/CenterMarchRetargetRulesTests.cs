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
        public void ResolveFlankLaneId_UsesLeftOrRightNeighbor()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var owner = 0;
            var left = layout.Slots[owner].LeftOpponentSlot;
            var right = layout.Slots[owner].RightOpponentSlot;

            Assert.AreEqual(GameIds.Lanes.Left, CenterMarchRetargetRules.ResolveFlankLaneId(owner, left, layout));
            Assert.AreEqual(GameIds.Lanes.Right, CenterMarchRetargetRules.ResolveFlankLaneId(owner, right, layout));
        }

        [Test]
        public void HasReachedRouteEnd_NearFinish_IsTrue()
        {
            Assert.IsTrue(CenterMarchRetargetRules.HasReachedRouteEnd(96f, 100f));
            Assert.IsFalse(CenterMarchRetargetRules.HasReachedRouteEnd(85f, 100f));
            Assert.IsTrue(CenterMarchRetargetRules.HasReachedRouteEnd(
                50f,
                100f,
                worldPosition: new Vector3(0f, 0f, 0f),
                routeEnd: new Vector3(2f, 0f, 0f)));
        }

        [Test]
        public void HasPassedMidHalfway_Boundaries()
        {
            const float meet = 40f;
            const float total = 100f;
            var halfway = CenterMarchRetargetRules.GetMidHalfwayCommitDistance(meet, total);
            Assert.AreEqual(70f, halfway, 0.01f);

            Assert.IsFalse(CenterMarchRetargetRules.HasPassedMidHalfway(meet - 1f, meet, total));
            Assert.IsFalse(CenterMarchRetargetRules.HasPassedMidHalfway(halfway - 0.1f, meet, total));
            Assert.IsTrue(CenterMarchRetargetRules.HasPassedMidHalfway(halfway, meet, total));
            Assert.IsTrue(CenterMarchRetargetRules.HasPassedMidHalfway(total, meet, total));
        }

        [Test]
        public void ResolveEffectiveMarchProgress_UsesWorldWhenAheadOfStoredProgress()
        {
            var path = new LanePath(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 100f),
            });
            var world = path.EvaluateDistance(80f);
            var effective = CenterMarchRetargetRules.ResolveEffectiveMarchProgress(
                marchProgressDistance: 35f,
                world,
                path);
            Assert.Greater(effective, 75f);
            Assert.IsTrue(CenterMarchRetargetRules.HasPassedMidHalfway(35f, world, path));
        }

        [Test]
        public void EliminateCenterTarget_ChaseLagProgress_StillCommitsWhenWorldPastHalfway()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var oldOpponent = lane.OpponentSlot;
            var oldPath = lane.Path;
            var meet = CenterMarchRetargetRules.GetCenterMeetDistance(oldPath);
            var halfway = CenterMarchRetargetRules.GetMidHalfwayCommitDistance(meet, oldPath.TotalLength);

            // Simulate ranged chase: body is deep on enemy half, stored progress still early.
            var worldProgress = Mathf.Lerp(halfway, oldPath.TotalLength - 5f, 0.5f);
            var staleProgress = meet * 0.5f;
            Assert.Less(staleProgress, halfway);

            var stats = new UnitCombatStats(
                UnitRole.Ranged, 200f, 0f, 1f, 1f, 1f, 8f, 8f, 1);
            var unit = controller.Combat.SpawnUnit(
                attacker, GameIds.Lanes.Center, UnitRole.Ranged, stats, staleProgress);
            unit.WorldPosition = oldPath.EvaluateDistance(worldProgress);
            unit.MarchProgressDistance = staleProgress;

            RuinAllBuildings(controller, oldOpponent);

            Assert.IsNotNull(unit.CommittedMarchPath,
                "World position past mid-halfway must commit even if MarchProgressDistance lagged from chase.");
            Assert.AreEqual(GameIds.Lanes.Center, unit.LaneId);
        }

        [Test]
        public void GetCenterMeetDistance_NearMapOrigin()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Center, out var lane));
            var meet = CenterMarchRetargetRules.GetCenterMeetDistance(lane.Path);
            var point = Flat(lane.Path.EvaluateDistance(meet));
            Assert.Less(point.magnitude, 1f, "Meet should sit near map center.");
            Assert.Greater(meet, 1f);
            Assert.Less(meet, lane.Path.TotalLength - 1f);
        }

        [Test]
        public void EliminateCenterTarget_UnitAtMidFinish_RemountsToFlankWithoutOwnBase()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var oldOpponent = lane.OpponentSlot;
            var next = CenterMarchRetargetRules.ResolveNextAliveClockwise(oldOpponent, controller.Players, attacker);
            Assert.IsTrue(next.HasValue);

            var stats = new UnitCombatStats(
                UnitRole.Melee, 200f, 0f, 1f, 1f, 1f, 1.5f, 8f, 1);
            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, GameIds.Lanes.Center, out var oldRoute));
            var unit = controller.Combat.SpawnUnit(
                attacker, GameIds.Lanes.Center, UnitRole.Melee, stats, oldRoute.TotalLength);
            unit.WorldPosition = oldRoute.Path.End;
            unit.MarchProgressDistance = oldRoute.TotalLength;
            var positionBefore = Flat(unit.WorldPosition);

            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, GameIds.Lanes.Left, out var leftRoute));
            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, GameIds.Lanes.Right, out var rightRoute));
            var expectedFlank = CenterMarchRetargetRules.ResolveFlankLaneIdFromPosition(
                attacker,
                next.Value,
                unit.WorldPosition,
                controller.Layout,
                leftRoute,
                rightRoute);

            RuinAllBuildings(controller, oldOpponent);

            Assert.AreEqual(expectedFlank, unit.LaneId);
            Assert.AreEqual(positionBefore, Flat(unit.WorldPosition),
                "Flank remount must not teleport.");
            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, expectedFlank, out var flankRoute));
            Assert.Greater(flankRoute.TotalLength, 1f);

            var ownBase = Flat(controller.Layout.Slots[attacker].BasePosition);
            var enemyMain = Flat(controller.Layout.Slots[next.Value]
                .GetBuildingWorldPosition(GameIds.Buildings.Main));
            Assert.IsFalse(
                CenterMarchRetargetRules.ForwardArcPassesOwnBase(
                    flankRoute,
                    unit.WorldPosition,
                    enemyMain,
                    ownBase),
                "Chosen flank must not march through own base.");
        }

        [Test]
        public void EliminateCenterTarget_BeforeHalfway_StaysOnNewCenter()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var oldOpponent = lane.OpponentSlot;
            var meet = CenterMarchRetargetRules.GetCenterMeetDistance(lane.Path);
            var halfway = CenterMarchRetargetRules.GetMidHalfwayCommitDistance(meet, lane.Path.TotalLength);
            var progress = Mathf.Lerp(0f, halfway, 0.25f);

            var stats = new UnitCombatStats(
                UnitRole.Melee, 200f, 0f, 1f, 1f, 1f, 1.5f, 8f, 1);
            var unit = controller.Combat.SpawnUnit(
                attacker, GameIds.Lanes.Center, UnitRole.Melee, stats, progress);
            unit.WorldPosition = lane.Path.EvaluateDistance(progress);

            RuinAllBuildings(controller, oldOpponent);

            Assert.AreEqual(GameIds.Lanes.Center, unit.LaneId);
            Assert.IsNull(unit.CommittedMarchPath);
        }

        [Test]
        public void EliminateCenterTarget_PastHalfway_CommitsOldMidThenFlanksWithoutOwnBase()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var oldOpponent = lane.OpponentSlot;
            var next = CenterMarchRetargetRules.ResolveNextAliveClockwise(oldOpponent, controller.Players, attacker);
            Assert.IsTrue(next.HasValue);

            var oldPath = lane.Path;
            var meet = CenterMarchRetargetRules.GetCenterMeetDistance(oldPath);
            var halfway = CenterMarchRetargetRules.GetMidHalfwayCommitDistance(meet, oldPath.TotalLength);
            var progress = Mathf.Lerp(halfway, oldPath.TotalLength - CenterMarchRetargetRules.RouteEndArrivalTolerance - 1f, 0.4f);
            var oldEnd = Flat(oldPath.End);

            var stats = new UnitCombatStats(
                UnitRole.Melee, 200f, 0f, 1f, 1f, 1f, 1.5f, 8f, 1);
            var unit = controller.Combat.SpawnUnit(
                attacker, GameIds.Lanes.Center, UnitRole.Melee, stats, progress);
            unit.WorldPosition = oldPath.EvaluateDistance(progress);
            var positionBefore = Flat(unit.WorldPosition);

            RuinAllBuildings(controller, oldOpponent);

            Assert.AreEqual(GameIds.Lanes.Center, unit.LaneId);
            Assert.IsNotNull(unit.CommittedMarchPath);
            Assert.AreEqual(next.Value, unit.MarchFocusOpponentSlot);
            Assert.AreEqual(positionBefore, Flat(unit.WorldPosition));

            // Do not wipe the next foe — that would retarget again. Just march the commit out.
            const float deltaTime = 0.25f;
            for (var i = 0; i < 160; i++)
            {
                controller.Combat.Tick(deltaTime);
                if (unit.LaneId != GameIds.Lanes.Center)
                {
                    break;
                }
            }

            Assert.AreNotEqual(GameIds.Lanes.Center, unit.LaneId, "After old mid finish should remount flank.");
            Assert.IsNull(unit.CommittedMarchPath);
            Assert.Less(
                Vector3.Distance(Flat(unit.WorldPosition), oldEnd),
                12f,
                "Should arrive near old mid finish before flanking.");

            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, unit.LaneId, out var flankRoute));
            var ownBase = Flat(controller.Layout.Slots[attacker].BasePosition);
            var enemyMain = Flat(controller.Layout.Slots[next.Value]
                .GetBuildingWorldPosition(GameIds.Buildings.Main));
            Assert.IsFalse(
                CenterMarchRetargetRules.ForwardArcPassesOwnBase(
                    flankRoute,
                    unit.WorldPosition,
                    enemyMain,
                    ownBase),
                "Flank after commit must not go through own base.");
        }

        [Test]
        public void EliminateFlankTarget_WhileMarching_RemountsTowardNextAliveNotOwnBase()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var midOpponent = lane.OpponentSlot;
            var flankTarget = CenterMarchRetargetRules.ResolveNextAliveClockwise(
                midOpponent, controller.Players, attacker);
            Assert.IsTrue(flankTarget.HasValue);

            // Wipe mid foe first so waves retarget — then put a unit on flank toward the next.
            RuinAllBuildings(controller, midOpponent);

            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, GameIds.Lanes.Left, out var leftRoute));
            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, GameIds.Lanes.Right, out var rightRoute));

            // Place unit on the ring near the dead mid foe, as if coming from mid finish.
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out lane));
            // Rebuild a position: dead mid Main (still in layout).
            var nearDeadMid = Flat(controller.Layout.Slots[midOpponent]
                .GetBuildingWorldPosition(GameIds.Buildings.Main));

            var stats = new UnitCombatStats(
                UnitRole.Melee, 200f, 0f, 1f, 1f, 1f, 1.5f, 8f, 1);
            var chosenFlank = CenterMarchRetargetRules.ResolveFlankLaneIdFromPosition(
                attacker,
                flankTarget.Value,
                nearDeadMid,
                controller.Layout,
                leftRoute,
                rightRoute);
            var unit = controller.Combat.SpawnUnit(
                attacker, chosenFlank, UnitRole.Melee, stats, 0f);
            unit.WorldPosition = nearDeadMid;
            unit.MarchFocusOpponentSlot = flankTarget.Value;
            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, chosenFlank, out var route));
            unit.MarchProgressDistance = route.ProjectDistance(nearDeadMid);

            var nextAfterFlank = CenterMarchRetargetRules.ResolveNextAliveClockwise(
                flankTarget.Value, controller.Players, attacker);
            Assert.IsTrue(nextAfterFlank.HasValue);
            Assert.AreNotEqual(flankTarget.Value, nextAfterFlank.Value);

            RuinAllBuildings(controller, flankTarget.Value);

            Assert.AreEqual(nextAfterFlank.Value, unit.MarchFocusOpponentSlot);
            Assert.IsTrue(
                unit.LaneId == GameIds.Lanes.Left || unit.LaneId == GameIds.Lanes.Right);

            Assert.IsTrue(controller.Combat.TryGetRoute(attacker, unit.LaneId, out var newFlank));
            var ownBase = Flat(controller.Layout.Slots[attacker].BasePosition);
            var livingMain = Flat(controller.Layout.Slots[nextAfterFlank.Value]
                .GetBuildingWorldPosition(GameIds.Buildings.Main));
            Assert.IsFalse(
                CenterMarchRetargetRules.ForwardArcPassesOwnBase(
                    newFlank,
                    unit.WorldPosition,
                    livingMain,
                    ownBase),
                "After flank target dies, remount must not march through own base.");

            // One step should not dive toward own base.
            var before = Flat(unit.WorldPosition);
            controller.Combat.Tick(0.25f);
            var step = Flat(unit.WorldPosition) - before;
            if (step.sqrMagnitude > 0.0001f)
            {
                var toOwnBase = ownBase - before;
                Assert.Less(
                    Vector3.Dot(step.normalized, toOwnBase.normalized),
                    0.85f,
                    "Must not march toward own base after retarget.");
            }
        }

        [Test]
        public void EliminateCenterTarget_UnitMidMid_StaysOnCenter()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));
            controller.BeginEarlyPhase();

            var attacker = 0;
            Assert.IsTrue(controller.Graph.TryGetLane(attacker, GameIds.Lanes.Center, out var lane));
            var oldOpponent = lane.OpponentSlot;
            var meet = CenterMarchRetargetRules.GetCenterMeetDistance(lane.Path);

            var stats = new UnitCombatStats(
                UnitRole.Melee, 200f, 0f, 1f, 1f, 1f, 1.5f, 8f, 1);
            var unit = controller.Combat.SpawnUnit(
                attacker, GameIds.Lanes.Center, UnitRole.Melee, stats, meet);
            unit.WorldPosition = lane.Path.EvaluateDistance(meet);

            RuinAllBuildings(controller, oldOpponent);

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
