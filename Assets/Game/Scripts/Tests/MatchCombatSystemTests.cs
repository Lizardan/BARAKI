using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchCombatSystemTests
    {
        ICombatUnitCatalog _catalog;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Editor.RaceContentBuilder.EnsureContent();
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(Editor.RaceContentBuilder.CatalogPath);
            _catalog = new RaceCatalogCombatCatalog(raceCatalog);
        }

        [Test]
        public void HandleWave_SpawnsInFrontOfBarracks_AllFourPlayers()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            var barracksByLane = new Dictionary<string, string>
            {
                [GameIds.Lanes.Left] = GameIds.Buildings.BarracksLeft,
                [GameIds.Lanes.Center] = GameIds.Buildings.BarracksCenter,
                [GameIds.Lanes.Right] = GameIds.Buildings.BarracksRight,
            };

            const float minBarracksClearance = CombatFormationRules.BarracksFootprintExtent * 0.5f;

            for (var slot = 0; slot < 4; slot++)
            {
                foreach (var laneEntry in barracksByLane)
                {
                    var combat = new MatchCombatSystem();
                    combat.Reset(controller.Players, controller.Graph, randomSeed: 100 + slot * 10);

                    var wave = new BarracksWaveFired(
                        slot,
                        laneEntry.Value,
                        laneEntry.Key,
                        GameIds.Races.Human,
                        squadLevel: 1,
                        squadId: "test");

                    combat.HandleWave(wave, _catalog);
                    Assert.AreEqual(4, combat.Units.Count, $"Slot {slot} lane {laneEntry.Key} should spawn L1 squad.");

                    controller.Graph.TryGetLane(slot, laneEntry.Key, out var lane);
                    var route = LaneRoute.FromPath(lane.Path);
                    var barracksPosition = controller.Layout.Slots[slot]
                        .GetBuildingWorldPosition(laneEntry.Value);

                    for (var i = 0; i < combat.Units.Count; i++)
                    {
                        var unit = combat.Units[i];
                        Assert.IsTrue(combat.TryGetUnitWorldPosition(unit, out var position));

                        var offset = position - barracksPosition;
                        offset.y = 0f;
                        Assert.Greater(
                            offset.magnitude,
                            minBarracksClearance,
                            $"Slot {slot} {laneEntry.Key} unit {unit.UnitId} spawned inside barracks.");

                        var forward = route.EvaluateDirectionAtDistance(unit.MarchSpawnDistance);
                        forward.y = 0f;
                        Assert.Greater(
                            Vector3.Dot(offset.normalized, forward.normalized),
                            0.15f,
                            $"Slot {slot} {laneEntry.Key} unit {unit.UnitId} should spawn ahead along march direction.");
                    }

                    for (var i = 0; i < combat.Units.Count; i++)
                    {
                        for (var j = i + 1; j < combat.Units.Count; j++)
                        {
                            Assert.IsTrue(combat.TryGetUnitWorldPosition(combat.Units[i], out var a));
                            Assert.IsTrue(combat.TryGetUnitWorldPosition(combat.Units[j], out var b));
                            Assert.GreaterOrEqual(
                                HorizontalDistance(a, b),
                                CombatFormationRules.MinUnitSeparation * 0.65f,
                                $"Slot {slot} {laneEntry.Key} wave units should not stack on spawn.");
                        }
                    }
                }
            }
        }

        [Test]
        public void HandleWave_NorthLeftBarracks_MarchesForwardWithoutStalling()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, randomSeed: 77);

            var wave = new BarracksWaveFired(
                1,
                GameIds.Buildings.BarracksLeft,
                GameIds.Lanes.Left,
                GameIds.Races.Human,
                squadLevel: 1,
                squadId: "test");

            combat.HandleWave(wave, _catalog);
            Assert.AreEqual(4, combat.Units.Count);

            controller.Graph.TryGetLane(1, GameIds.Lanes.Left, out var lane);
            var route = LaneRoute.FromPath(lane.Path);
            var startDistances = new float[combat.Units.Count];
            for (var i = 0; i < combat.Units.Count; i++)
            {
                startDistances[i] = combat.Units[i].MarchProgressDistance;
            }

            for (var i = 0; i < 60; i++)
            {
                combat.Tick(0.05f);
            }

            for (var i = 0; i < combat.Units.Count; i++)
            {
                var unit = combat.Units[i];
                Assert.Greater(
                    unit.MarchProgressDistance,
                    startDistances[i] + 4f,
                    $"North left barracks unit {unit.UnitId} should march forward immediately.");
                Assert.Greater(
                    unit.MarchProgressDistance,
                    route.ProjectDistanceForward(unit.WorldPosition, unit.MarchProgressDistance) - 2.5f,
                    $"North left barracks unit {unit.UnitId} progress should track lane position.");
            }
        }

        [Test]
        public void HandleWave_SpawnsSquadUnitsOnLane()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var wave = new BarracksWaveFired(
                0,
                GameIds.Buildings.BarracksCenter,
                GameIds.Lanes.Center,
                GameIds.Races.Human,
                squadLevel: 1,
                squadId: "test");

            combat.HandleWave(wave, _catalog);

            Assert.AreEqual(4, combat.Units.Count);
            foreach (var unit in combat.Units)
            {
                Assert.AreEqual(0, unit.OwnerSlot);
                Assert.AreEqual(GameIds.Lanes.Center, unit.LaneId);
                Assert.AreEqual(RaceMarchSpeedRules.BaseMarchSpeed, unit.MarchMoveSpeed, 0.001f);
            }
        }

        [Test]
        public void HandleWave_BugSquadL4_AllRolesShareFrenzyMarchSpeed()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var wave = new BarracksWaveFired(
                0,
                GameIds.Buildings.BarracksCenter,
                GameIds.Lanes.Center,
                GameIds.Races.Bug,
                squadLevel: 4,
                squadId: "test");

            combat.HandleWave(wave, _catalog);

            Assert.AreEqual(14, combat.Units.Count);
            var expected = RaceMarchSpeedRules.BaseMarchSpeed * RaceMarchSpeedRules.BugFrenzyMoveMultiplier;
            foreach (var unit in combat.Units)
            {
                Assert.AreEqual(expected, unit.MarchMoveSpeed, 0.001f,
                    $"Role {unit.Role} should march at race speed");
            }
        }

        [Test]
        public void Tick_UnitMarchesAlongLane()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 10f,
                goldBounty: 1);

            var unit = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats);
            var start = unit.WorldPosition;
            combat.Tick(1f);

            Assert.Greater(HorizontalDistance(start, unit.WorldPosition), 9f);
        }

        [Test]
        public void Tick_KillGrantsBountyToKillerOwner()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, randomSeed: 1);

            var killerStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 50f,
                damageMax: 50f,
                attackSpeed: 10f,
                attackRange: 30f,
                moveSpeed: 0f,
                goldBounty: 1);

            var victimStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 40f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 12);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meetDistance0 = lane0.Path.TotalLength * 0.45f;
            var meetDistance1 = lane1.Path.TotalLength * 0.45f;

            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, killerStats, meetDistance0);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats, meetDistance1);

            var killerGoldBefore = controller.Players[0].Gold;
            UnitKillEvent? killEvent = null;
            combat.UnitKilled += e => killEvent = e;

            combat.Tick(0.2f);

            Assert.AreEqual(1, combat.Units.Count);
            Assert.AreEqual(killerGoldBefore + 12, controller.Players[0].Gold);
            Assert.IsTrue(killEvent.HasValue);
            Assert.AreEqual(12, killEvent.Value.GoldGranted);
            Assert.AreEqual(0, killEvent.Value.KillerOwnerSlot);
            Assert.AreEqual(1, killEvent.Value.VictimOwnerSlot);
        }

        [Test]
        public void Tick_MeleeCannotDamageFlyingInRange()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var meleeStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 100f,
                damageMax: 100f,
                attackSpeed: 10f,
                attackRange: 30f,
                moveSpeed: 0f,
                goldBounty: 1);

            var flyingStats = new UnitCombatStats(
                UnitRole.Flying,
                maxHp: 50f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 6);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meetDistance0 = lane0.Path.TotalLength * 0.45f;
            var meetDistance1 = lane1.Path.TotalLength * 0.45f;

            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, meleeStats, meetDistance0);
            var flying = combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Flying, flyingStats, meetDistance1);

            combat.Tick(1f);

            Assert.AreEqual(2, combat.Units.Count);
            Assert.AreEqual(50f, flying.CurrentHp, 0.01f);
        }

        [Test]
        public void CombatLaneRules_MirrorFlankEngagesOpponentsOnly()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            var leftUnit = new MatchUnitState(1, 0, GameIds.Lanes.Left, UnitRole.Melee, default, 1f, Vector3.zero);
            var rightOpponent = new MatchUnitState(2, 3, GameIds.Lanes.Right, UnitRole.Melee, default, 1f, Vector3.zero);
            var rightNonOpponent = new MatchUnitState(3, 1, GameIds.Lanes.Right, UnitRole.Melee, default, 1f, Vector3.zero);

            Assert.IsTrue(CombatLaneRules.CanEngage(leftUnit, rightOpponent, controller.Graph));
            Assert.IsFalse(CombatLaneRules.CanEngage(leftUnit, rightNonOpponent, controller.Graph));
        }

        [Test]
        public void Tick_AfterKill_PursuesAnotherEnemyInAggroRadius()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, randomSeed: 3);

            var killerStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 50f,
                damageMax: 50f,
                attackSpeed: 10f,
                attackRange: 30f,
                moveSpeed: 6f,
                goldBounty: 1);

            var victimStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 30f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 5);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meetDistance0 = lane0.Path.TotalLength * 0.45f;
            var meetDistance1 = lane1.Path.TotalLength * 0.45f;

            var killer = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, killerStats, meetDistance0);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats, meetDistance1);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats, meetDistance1 + 10f);

            combat.Tick(0.2f);
            Assert.AreEqual(2, combat.Units.Count);

            MatchUnitState remainingEnemy = null;
            foreach (var unit in combat.Units)
            {
                if (unit.OwnerSlot == 1)
                {
                    remainingEnemy = unit;
                }
            }

            Assert.IsNotNull(remainingEnemy);
            Assert.IsTrue(combat.TryGetUnitWorldPosition(killer, out var killerStart));
            Assert.IsTrue(combat.TryGetUnitWorldPosition(remainingEnemy, out var enemyStart));
            var startDistance = Vector3.Distance(
                new Vector3(killerStart.x, 0f, killerStart.z),
                new Vector3(enemyStart.x, 0f, enemyStart.z));
            var enemyHpBefore = remainingEnemy.CurrentHp;

            combat.Tick(1f);

            Assert.IsTrue(combat.TryGetUnitWorldPosition(killer, out var killerAfter));
            Assert.IsTrue(combat.TryGetUnitWorldPosition(remainingEnemy, out var enemyAfter));
            var endDistance = Vector3.Distance(
                new Vector3(killerAfter.x, 0f, killerAfter.z),
                new Vector3(enemyAfter.x, 0f, enemyAfter.z));

            var closedDistance = endDistance < startDistance - 0.1f;
            var dealtDamage = remainingEnemy.CurrentHp < enemyHpBefore;
            Assert.IsTrue(closedDistance || dealtDamage);
        }

        [Test]
        public void Tick_ChaseTowardTarget_DoesNotExceedMoveSpeed()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var chaserStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 6f,
                goldBounty: 1);

            var targetStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 500f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);

            var chaser = combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                chaserStats,
                lane0.Path.TotalLength * 0.2f);
            combat.SpawnUnit(
                1,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                targetStats,
                lane1.Path.TotalLength * 0.5f,
                formationOffset: new Vector3(5f, 0f, 3f));

            Assert.IsTrue(combat.TryGetUnitWorldPosition(chaser, out var before));

            const float deltaTime = 0.5f;
            combat.Tick(deltaTime);

            Assert.IsTrue(combat.TryGetUnitWorldPosition(chaser, out var after));
            before.y = 0f;
            after.y = 0f;
            var moved = Vector3.Distance(before, after);

            Assert.LessOrEqual(moved, chaser.MarchMoveSpeed * deltaTime * 1.05f);
        }

        [Test]
        public void Tick_Chase_RoutesAroundBlockingAlly()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var chaserStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1.5f,
                moveSpeed: 6f,
                goldBounty: 1);

            var stationaryStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1.5f,
                moveSpeed: 0f,
                goldBounty: 1);

            var enemyStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 500f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1.5f,
                moveSpeed: 0f,
                goldBounty: 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meetDistance = lane0.Path.TotalLength * 0.45f;

            var chaser = combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                chaserStats,
                meetDistance - 6f);
            var blocker = combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                stationaryStats,
                meetDistance - 1.5f);
            var enemy = combat.SpawnUnit(
                1,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                enemyStats,
                lane1.Path.TotalLength * 0.45f);

            Assert.IsTrue(combat.TryGetUnitWorldPosition(blocker, out var blockerStart));
            Assert.IsTrue(combat.TryGetUnitWorldPosition(chaser, out var chaserStart));
            Assert.IsTrue(combat.TryGetUnitWorldPosition(enemy, out var enemyStart));
            var startDistance = HorizontalDistance(chaserStart, enemyStart);

            for (var i = 0; i < 40; i++)
            {
                combat.Tick(0.1f);
            }

            Assert.IsTrue(combat.TryGetUnitWorldPosition(blocker, out var blockerEnd));
            Assert.IsTrue(combat.TryGetUnitWorldPosition(chaser, out var chaserEnd));
            Assert.IsTrue(combat.TryGetUnitWorldPosition(enemy, out var enemyEnd));

            var blockerMoved = HorizontalDistance(blockerStart, blockerEnd);
            Assert.Less(blockerMoved, 0.1f, "Blocking ally must not be displaced");

            var endDistance = HorizontalDistance(chaserEnd, enemyEnd);
            Assert.Less(endDistance, startDistance - 0.25f, "Chaser must close distance to enemy");
        }

        [Test]
        public void Tick_Chase_NeverDisplacesAlly()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var moveStats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1.5f, 6f, 1);
            var stationaryStats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1.5f, 0f, 1);
            var enemyStats = new UnitCombatStats(
                UnitRole.Melee, 500f, 0f, 1f, 1f, 0.1f, 1.5f, 0f, 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            var meet = lane0.Path.TotalLength * 0.45f;

            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, moveStats, meet - 5f);
            var blocker = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stationaryStats, meet - 1f);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, enemyStats, meet);

            Assert.IsTrue(combat.TryGetUnitWorldPosition(blocker, out var blockerStart));

            for (var i = 0; i < 50; i++)
            {
                combat.Tick(0.1f);
            }

            Assert.IsTrue(combat.TryGetUnitWorldPosition(blocker, out var blockerEnd));
            Assert.Less(HorizontalDistance(blockerStart, blockerEnd), 0.1f);
        }

        [Test]
        public void Tick_March_NeverDisplacesAlly()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var stats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1.5f, 0f, 1);
            var rearStats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1.5f, 5f, 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            var meet = lane0.Path.TotalLength * 0.4f;

            var front = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, meet);
            var rear = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, rearStats, meet - 3f);

            Assert.IsTrue(combat.TryGetUnitWorldPosition(front, out var frontStart));
            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane);
            var route = LaneRoute.FromPath(lane.Path);
            var rearStartProgress = route.ProjectDistance(rear.WorldPosition);

            for (var i = 0; i < 30; i++)
            {
                combat.Tick(0.1f);
            }

            Assert.IsTrue(combat.TryGetUnitWorldPosition(front, out var frontEnd));
            Assert.Less(HorizontalDistance(frontStart, frontEnd), 0.1f);
            Assert.Greater(route.ProjectDistance(rear.WorldPosition), rearStartProgress + 0.5f);
        }

        [Test]
        public void Tick_ThreeMelee_SpreadLaterallyAroundTarget()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var allyStats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 10f, 10f, 1f, 1.5f, 5f, 1);
            var enemyStats = new UnitCombatStats(
                UnitRole.Melee, 500f, 0f, 1f, 1f, 0.1f, 1.5f, 0f, 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meet = lane0.Path.TotalLength * 0.45f;

            var spawnGap = CombatFormationRules.MinUnitSeparation + 1f;
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, allyStats, meet - spawnGap * 2f);
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, allyStats, meet - spawnGap);
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, allyStats, meet - 1f);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, enemyStats, lane1.Path.TotalLength * 0.45f);

            for (var i = 0; i < 50; i++)
            {
                combat.Tick(0.1f);
            }

            var spreadCount = 0;
            var allyPositions = new List<Vector3>();
            foreach (var unit in combat.Units)
            {
                if (unit.OwnerSlot != 0)
                {
                    continue;
                }

                allyPositions.Add(unit.WorldPosition);
                controller.Graph.TryGetLane(unit.OwnerSlot, unit.LaneId, out var lane);
                var route = LaneRoute.FromPath(lane.Path);
                if (GetPathLateral(route, unit.WorldPosition) > 0.35f)
                {
                    spreadCount++;
                }
            }

            Assert.GreaterOrEqual(spreadCount, 1);

            for (var i = 0; i < allyPositions.Count; i++)
            {
                for (var j = i + 1; j < allyPositions.Count; j++)
                {
                    Assert.GreaterOrEqual(
                        HorizontalDistance(allyPositions[i], allyPositions[j]),
                        CombatFormationRules.MinUnitSeparation * 0.70f);
                }
            }
        }

        [Test]
        public void Tick_RearMelee_ReachesAttackRange()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var allyStats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 10f, 10f, 1f, 1.5f, 5f, 1);
            var enemyStats = new UnitCombatStats(
                UnitRole.Melee, 500f, 0f, 1f, 1f, 0.1f, 1.5f, 0f, 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meet = lane0.Path.TotalLength * 0.45f;
            var right = CombatFormationRules.GetLaneRight(lane0.Path, meet);

            combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                allyStats,
                meet - 2f,
                formationOffset: right * 2f);
            combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                allyStats,
                meet - 1.5f,
                formationOffset: right * -2f);
            var rear = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, allyStats, meet - 3f);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, enemyStats, lane1.Path.TotalLength * 0.45f);

            var reachedCombat = false;
            MatchUnitState enemyUnit = null;
            foreach (var unit in combat.Units)
            {
                if (unit.OwnerSlot == 1)
                {
                    enemyUnit = unit;
                    break;
                }
            }

            Assert.IsNotNull(enemyUnit);
            Assert.IsTrue(combat.TryGetUnitWorldPosition(rear, out var rearStart));
            Assert.IsTrue(combat.TryGetUnitWorldPosition(enemyUnit, out var enemyStart));
            var startDistance = HorizontalDistance(rearStart, enemyStart);

            for (var i = 0; i < 80; i++)
            {
                combat.Tick(0.1f);
                if (!combat.TryGetUnitWorldPosition(rear, out var rearPos))
                {
                    continue;
                }

                foreach (var enemy in combat.Units)
                {
                    if (enemy.OwnerSlot != 1)
                    {
                        continue;
                    }

                    if (!combat.TryGetUnitWorldPosition(enemy, out var enemyPos))
                    {
                        continue;
                    }

                    if (HorizontalDistance(rearPos, enemyPos) <= rear.Stats.AttackRange * 1.05f)
                    {
                        reachedCombat = true;
                        break;
                    }
                }

                if (reachedCombat)
                {
                    break;
                }
            }

            if (!reachedCombat)
            {
                Assert.IsTrue(combat.TryGetUnitWorldPosition(rear, out var rearEnd));
                Assert.IsTrue(combat.TryGetUnitWorldPosition(enemyUnit, out var enemyEnd));
                var closedDistance = HorizontalDistance(rearEnd, enemyEnd) < startDistance - 1f;
                controller.Graph.TryGetLane(rear.OwnerSlot, rear.LaneId, out var lane);
                var route = LaneRoute.FromPath(lane.Path);
                var rearLateral = GetPathLateral(route, rear.WorldPosition);
                Assert.IsTrue(
                    closedDistance && rearLateral > 0.4f,
                    "Rear melee should close distance with lateral bypass around allies");
                return;
            }

            Assert.IsTrue(reachedCombat, "Rear melee should reach attack range around allies");
        }

        [Test]
        public void Tick_March_DoesNotStackBehindFightingAlly()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var blockerStats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1.5f, 0f, 1);
            var marchStats = new UnitCombatStats(
                UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1.5f, 5f, 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            var meet = lane0.Path.TotalLength * 0.4f;

            var blocker = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, blockerStats, meet);
            var marcher = combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, marchStats, meet - 3f);

            for (var i = 0; i < 40; i++)
            {
                combat.Tick(0.1f);
            }

            var route = LaneRoute.FromPath(lane0.Path);

            Assert.Greater(
                route.ProjectDistance(marcher.WorldPosition),
                meet - 2f,
                "Marcher should advance past a stationary ally");
            Assert.GreaterOrEqual(
                HorizontalDistance(marcher.WorldPosition, blocker.WorldPosition),
                CombatFormationRules.MinUnitSeparation * 0.85f);
        }

        // RTS flow integration tests continue below.

        [Test]
        public void Tick_Chase_DoesNotOverlapUnits()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1.5f,
                moveSpeed: 5f,
                goldBounty: 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meet = lane0.Path.TotalLength * 0.45f;

            var spawnGap = CombatFormationRules.MinUnitSeparation + 1f;
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, meet - spawnGap * 2f);
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, meet - spawnGap);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, stats, meet);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, stats, meet + spawnGap);

            for (var i = 0; i < 30; i++)
            {
                combat.Tick(0.1f);
            }

            var minPairDistance = stats.AttackRange * 0.72f;
            var units = combat.Units;
            for (var i = 0; i < units.Count; i++)
            {
                for (var j = i + 1; j < units.Count; j++)
                {
                    Assert.IsTrue(combat.TryGetUnitWorldPosition(units[i], out var a));
                    Assert.IsTrue(combat.TryGetUnitWorldPosition(units[j], out var b));
                    Assert.GreaterOrEqual(
                        HorizontalDistance(a, b),
                        minPairDistance);
                }
            }
        }

        [Test]
        public void Tick_Chase_StaysWithinRoadBoundary()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var chaserStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1.5f,
                moveSpeed: 6f,
                goldBounty: 1);

            var targetStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 500f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1.5f,
                moveSpeed: 0f,
                goldBounty: 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);

            combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                chaserStats,
                lane0.Path.TotalLength * 0.2f);
            combat.SpawnUnit(
                1,
                GameIds.Lanes.Center,
                UnitRole.Melee,
                targetStats,
                lane1.Path.TotalLength * 0.5f,
                formationOffset: new Vector3(5f, 0f, 3f));

            for (var i = 0; i < 25; i++)
            {
                combat.Tick(0.1f);
            }

            foreach (var unit in combat.Units)
            {
                if (unit.OwnerSlot != 0)
                {
                    continue;
                }

                controller.Graph.TryGetLane(unit.OwnerSlot, unit.LaneId, out var lane);
                var route = LaneRoute.FromPath(lane.Path);
                var distance = route.ProjectDistance(unit.WorldPosition);
                var spine = route.EvaluateDistance(distance);
                spine.y = 0f;
                var position = unit.WorldPosition;
                position.y = 0f;
                var drift = Vector3.Distance(position, spine);
                Assert.LessOrEqual(
                    drift,
                    UnitLocomotionRules.MaxCombatDriftFromLane + 0.5f);
            }
        }

        [Test]
        public void Tick_N4FlankLeft_MarchesOffBarracksAndProgressesAlongLane()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(4));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            controller.Graph.TryGetLane(1, GameIds.Lanes.Left, out var lane);
            var route = LaneRoute.FromPath(lane.Path);
            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 6f,
                goldBounty: 1);

            var unit = combat.SpawnUnit(1, GameIds.Lanes.Left, UnitRole.Melee, stats);
            var startDistance = unit.MarchProgressDistance;

            for (var i = 0; i < 120; i++)
            {
                combat.Tick(0.05f);
            }

            Assert.Greater(unit.MarchProgressDistance, startDistance + 8f);
            Assert.Greater(unit.MarchProgressDistance, route.ProjectDistance(unit.WorldPosition) - 1f);

            var spine = route.EvaluateDistance(unit.MarchProgressDistance);
            spine.y = 0f;
            var position = unit.WorldPosition;
            position.y = 0f;
            Assert.LessOrEqual(
                Vector3.Distance(position, spine),
                UnitLocomotionRules.MaxMarchDriftFromLane + 0.75f);
        }

        [Test]
        public void Tick_SiegeDamagesEnemyBuildingOnCenterLane()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = controller.Combat;
            var targetBuilding = FindBuilding(
                controller.Buildings.Buildings,
                ownerSlot: 1,
                GameIds.Buildings.BarracksCenter);
            Assert.IsNotNull(targetBuilding);

            var siegeStats = new UnitCombatStats(
                UnitRole.Siege,
                maxHp: 200f,
                armor: 0f,
                damageMin: 500f,
                damageMax: 500f,
                attackSpeed: 2f,
                attackRange: 18f,
                moveSpeed: 2f,
                goldBounty: 1);

            var siege = combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                UnitRole.Siege,
                siegeStats,
                distanceAlongLane: 10f);
            siege.WorldPosition = targetBuilding.WorldPosition + Vector3.back * 6f;
            siege.CurrentTargetBuildingInstanceId = targetBuilding.InstanceId;

            for (var i = 0; i < 30; i++)
            {
                combat.Tick(0.25f);
            }

            Assert.IsTrue(targetBuilding.IsRuins);
        }

        static BuildingState FindBuilding(
            IReadOnlyList<BuildingState> buildings,
            int ownerSlot,
            string buildingId)
        {
            foreach (var building in buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.BuildingId == buildingId)
                {
                    return building;
                }
            }

            return null;
        }

        static float GetPathLateral(LaneRoute route, Vector3 worldPosition)
        {
            var distance = route.ProjectDistance(worldPosition);
            var spine = route.EvaluateDistance(distance);
            var right = CombatFormationRules.GetLaneRight(route.Path, distance);
            worldPosition.y = spine.y;
            return Mathf.Abs(Vector3.Dot(worldPosition - spine, right));
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
