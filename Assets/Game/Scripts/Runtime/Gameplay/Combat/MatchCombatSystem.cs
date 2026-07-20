using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public sealed class MatchCombatSystem
    {
        readonly List<MatchUnitState> _units = new();
        readonly Dictionary<int, MatchUnitState> _unitById = new();
        readonly List<MatchPlayerState> _players = new();
        readonly List<CombatProjectileState> _projectiles = new();
        readonly List<CombatMeleeStrikeState> _meleeStrikes = new();

        LaneGraph _graph;
        LaneRouteRegistry _routes;
        BuildingRegistry _buildings;
        WalkableSurface _walkable;
        MatchArenaLayout _layout;
        int _nextUnitId = 1;
        int _nextProjectileId = 1;
        System.Random _random;

        readonly List<MatchUnitState> _tickBuffer = new();
        readonly List<MatchUnitState> _nearbyBuffer = new();
        readonly List<MatchUnitState> _alliesBuffer = new();
        readonly List<MatchUnitState> _nestedBuffer = new();
        readonly List<CombatProjectileState> _projectileImpactBuffer = new();
        readonly List<CombatMeleeStrikeState> _meleeImpactBuffer = new();
        readonly List<PendingBarracksSpawn> _pendingSpawns = new();
        readonly Dictionary<int, LaneRoute> _committedRoutes = new();
        const float SpatialCellSize = 12f;
        readonly Dictionary<(int, int), List<MatchUnitState>> _spatialGrid = new();

        sealed class PendingBarracksSpawn
        {
            public float RemainingSeconds;
            public int OwnerSlot;
            public string LaneId;
            public UnitRole Role;
            public UnitCombatStats Stats;
            public float MarchMoveSpeed;
            public float SpawnDistance;
            public Vector3 FormationOffset;
        }

        public event Action<UnitKillEvent> UnitKilled;

        public IReadOnlyList<MatchUnitState> Units => _units;
        public IReadOnlyList<CombatProjectileState> Projectiles => _projectiles;
        public IReadOnlyList<CombatMeleeStrikeState> MeleeStrikes => _meleeStrikes;

        public bool TryGetUnitWorldPosition(MatchUnitState unit, out Vector3 position)
        {
            if (unit == null)
            {
                position = default;
                return false;
            }

            position = unit.WorldPosition;
            return true;
        }

        public void Reset(IReadOnlyList<MatchPlayerState> players, LaneGraph graph, int randomSeed = 12345)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            _units.Clear();
            _unitById.Clear();
            ClearSpatialGrid();
            _pendingSpawns.Clear();
            _players.Clear();
            _projectiles.Clear();
            _meleeStrikes.Clear();
            _players.AddRange(players);
            _graph = graph;
            _routes = LaneRouteRegistry.Build(graph);
            _walkable = null;
            _layout = null;
            _committedRoutes.Clear();
            _nextUnitId = 1;
            _nextProjectileId = 1;
            _random = new System.Random(randomSeed);
        }

        public void SetBuildings(BuildingRegistry buildings) => _buildings = buildings;

        /// <summary>Arena layout for flank remount scoring after mid retarget.</summary>
        public void SetArenaLayout(MatchArenaLayout layout) => _layout = layout;

        /// <summary>SourceParts walkable area (static bake per map mode). Null = legacy corridor clamp.</summary>
        public void SetWalkableSurface(WalkableSurface surface) => _walkable = surface;

        public bool TryGetRoute(int ownerSlot, string laneId, out LaneRoute route)
        {
            route = null;
            return _routes != null && _routes.TryGetRoute(ownerSlot, laneId, out route);
        }

        /// <summary>
        /// Replaces a lane route (center retarget). Units keep their world position and run onto the new path.
        /// </summary>
        /// <param name="skipUnitIds">Units that should not remount onto this path (e.g. mid finishers going to flank).</param>
        public void ReplaceLaneRoute(
            int ownerSlot,
            string laneId,
            LanePath path,
            HashSet<int> skipUnitIds = null)
        {
            if (_routes == null || path == null)
            {
                return;
            }

            _routes.Replace(ownerSlot, laneId, path);
            if (!_routes.TryGetRoute(ownerSlot, laneId, out var route))
            {
                return;
            }

            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != ownerSlot || unit.LaneId != laneId)
                {
                    continue;
                }

                if (skipUnitIds != null && skipUnitIds.Contains(unit.UnitId))
                {
                    continue;
                }

                if (unit.CommittedMarchPath != null)
                {
                    continue;
                }

                unit.CurrentTargetId = null;
                unit.CurrentTargetBuildingInstanceId = null;
                // Nearest point on the new path at the unit's current position — never snap WorldPosition.
                unit.MarchProgressDistance = route.ProjectDistance(unit.WorldPosition);
                if (_graph != null && _graph.TryGetLane(ownerSlot, laneId, out var lane) && lane != null)
                {
                    unit.MarchFocusOpponentSlot = lane.OpponentSlot;
                }
            }
        }

        /// <summary>
        /// Collect center-lane units that have arrived at the current mid route finish.
        /// Call before replacing the center path.
        /// </summary>
        public HashSet<int> CollectUnitsAtRouteEnd(int ownerSlot, string laneId)
        {
            var result = new HashSet<int>();
            if (_routes == null || !_routes.TryGetRoute(ownerSlot, laneId, out var route))
            {
                return result;
            }

            var end = route.Path.End;
            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != ownerSlot || unit.LaneId != laneId)
                {
                    continue;
                }

                if (unit.CommittedMarchPath != null)
                {
                    continue;
                }

                if (CenterMarchRetargetRules.HasReachedRouteEnd(
                        CenterMarchRetargetRules.ResolveEffectiveMarchProgress(
                            unit.MarchProgressDistance,
                            unit.WorldPosition,
                            route.Path),
                        route.TotalLength,
                        unit.WorldPosition,
                        end))
                {
                    result.Add(unit.UnitId);
                }
            }

            return result;
        }

        /// <summary>
        /// Units past mid-halfway (but not yet at finish) that must keep the old mid path.
        /// </summary>
        public HashSet<int> CollectUnitsPastMidHalfway(int ownerSlot, string laneId, HashSet<int> excludeUnitIds = null)
        {
            var result = new HashSet<int>();
            if (_routes == null || !_routes.TryGetRoute(ownerSlot, laneId, out var route))
            {
                return result;
            }

            var meet = CenterMarchRetargetRules.GetCenterMeetDistance(route.Path);
            var end = route.Path.End;
            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != ownerSlot || unit.LaneId != laneId)
                {
                    continue;
                }

                if (unit.CommittedMarchPath != null)
                {
                    continue;
                }

                if (excludeUnitIds != null && excludeUnitIds.Contains(unit.UnitId))
                {
                    continue;
                }

                var progress = CenterMarchRetargetRules.ResolveEffectiveMarchProgress(
                    unit.MarchProgressDistance,
                    unit.WorldPosition,
                    route.Path);

                if (CenterMarchRetargetRules.HasReachedRouteEnd(
                        progress,
                        route.TotalLength,
                        unit.WorldPosition,
                        end))
                {
                    continue;
                }

                if (CenterMarchRetargetRules.HasPassedMidHalfway(
                        progress,
                        meet,
                        route.TotalLength))
                {
                    result.Add(unit.UnitId);
                }
            }

            return result;
        }

        /// <summary>Keep old open mid path until these units reach its finish, then flank.</summary>
        public void CommitUnitsToMidPath(
            int ownerSlot,
            HashSet<int> unitIds,
            LanePath oldMidPath,
            int nextOpponentSlot)
        {
            if (unitIds == null || unitIds.Count == 0 || oldMidPath == null)
            {
                return;
            }

            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != ownerSlot || !unitIds.Contains(unit.UnitId))
                {
                    continue;
                }

                unit.CommittedMarchPath = oldMidPath;
                unit.MarchFocusOpponentSlot = nextOpponentSlot;
                unit.CurrentTargetId = null;
                unit.CurrentTargetBuildingInstanceId = null;
                unit.BehaviorState = UnitBehaviorState.Move;
                unit.MarchProgressDistance = CenterMarchRetargetRules.ResolveEffectiveMarchProgress(
                    unit.MarchProgressDistance,
                    unit.WorldPosition,
                    oldMidPath);
                _committedRoutes[unit.UnitId] = LaneRoute.FromPath(oldMidPath);
            }
        }

        /// <summary>
        /// Remount finished mid units onto a flank toward <paramref name="nextOpponentSlot"/>,
        /// choosing Left/Right per unit so the arc does not go through the owner's base.
        /// </summary>
        public void RemountUnitsToFlankToward(
            int ownerSlot,
            HashSet<int> unitIds,
            int nextOpponentSlot,
            MatchArenaLayout layout)
        {
            if (_routes == null || unitIds == null || unitIds.Count == 0 || layout == null)
            {
                return;
            }

            _routes.TryGetRoute(ownerSlot, GameIds.Lanes.Left, out var leftRoute);
            _routes.TryGetRoute(ownerSlot, GameIds.Lanes.Right, out var rightRoute);

            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != ownerSlot || !unitIds.Contains(unit.UnitId))
                {
                    continue;
                }

                ClearCommittedMarch(unit);
                var flankLaneId = CenterMarchRetargetRules.ResolveFlankLaneIdFromPosition(
                    ownerSlot,
                    nextOpponentSlot,
                    unit.WorldPosition,
                    layout,
                    leftRoute,
                    rightRoute);
                if (!_routes.TryGetRoute(ownerSlot, flankLaneId, out var route))
                {
                    continue;
                }

                unit.LaneId = flankLaneId;
                unit.MarchFocusOpponentSlot = nextOpponentSlot;
                unit.CurrentTargetId = null;
                unit.CurrentTargetBuildingInstanceId = null;
                unit.MarchProgressDistance = route.ProjectDistance(unit.WorldPosition);
                unit.BehaviorState = UnitBehaviorState.Move;
            }
        }

        /// <summary>
        /// Remount finished mid units onto a flank route without teleporting their world position.
        /// </summary>
        public void RemountUnitsToLane(int ownerSlot, HashSet<int> unitIds, string newLaneId)
        {
            if (_routes == null || unitIds == null || unitIds.Count == 0 || string.IsNullOrEmpty(newLaneId))
            {
                return;
            }

            if (!_routes.TryGetRoute(ownerSlot, newLaneId, out var route))
            {
                return;
            }

            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != ownerSlot || !unitIds.Contains(unit.UnitId))
                {
                    continue;
                }

                ClearCommittedMarch(unit);
                unit.LaneId = newLaneId;
                unit.CurrentTargetId = null;
                unit.CurrentTargetBuildingInstanceId = null;
                unit.MarchProgressDistance = route.ProjectDistance(unit.WorldPosition);
                unit.BehaviorState = UnitBehaviorState.Move;
            }
        }

        void ClearCommittedMarch(MatchUnitState unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.CommittedMarchPath = null;
            _committedRoutes.Remove(unit.UnitId);
        }

        bool TryGetEffectiveRoute(MatchUnitState unit, out LaneRoute route)
        {
            route = null;
            if (unit == null)
            {
                return false;
            }

            if (unit.CommittedMarchPath != null)
            {
                if (!_committedRoutes.TryGetValue(unit.UnitId, out route)
                    || route == null
                    || route.Path != unit.CommittedMarchPath)
                {
                    route = LaneRoute.FromPath(unit.CommittedMarchPath);
                    _committedRoutes[unit.UnitId] = route;
                }

                return true;
            }

            return _routes != null && _routes.TryGetRoute(unit.OwnerSlot, unit.LaneId, out route);
        }

        void TryCompleteCommittedMidMarch(MatchUnitState unit)
        {
            if (unit?.CommittedMarchPath == null || _layout == null)
            {
                return;
            }

            if (!TryGetEffectiveRoute(unit, out var route))
            {
                return;
            }

            if (!CenterMarchRetargetRules.HasReachedRouteEnd(
                    unit.MarchProgressDistance,
                    route.TotalLength,
                    unit.WorldPosition,
                    route.Path.End))
            {
                return;
            }

            var opponent = unit.MarchFocusOpponentSlot;
            if (opponent < 0)
            {
                ClearCommittedMarch(unit);
                unit.MarchFocusOpponentSlot = -1;
                return;
            }

            var ids = new HashSet<int> { unit.UnitId };
            RemountUnitsToFlankToward(unit.OwnerSlot, ids, opponent, _layout);
        }

        /// <summary>
        /// Units whose march focus is the eliminated foe (flank marchers + mid-commit).
        /// </summary>
        public HashSet<int> CollectUnitsFocusingOpponent(int ownerSlot, int opponentSlot)
        {
            var result = new HashSet<int>();
            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != ownerSlot)
                {
                    continue;
                }

                if (unit.MarchFocusOpponentSlot == opponentSlot)
                {
                    result.Add(unit.UnitId);
                }
            }

            return result;
        }

        /// <summary>
        /// After a flank/mid focus foe dies: remount living owner's units onto a flank
        /// toward the next alive enemy (no arc through own base).
        /// </summary>
        public void RetargetUnitsFocusingEliminated(
            int eliminatedSlot,
            IReadOnlyList<MatchPlayerState> players,
            MatchArenaLayout layout)
        {
            if (players == null || layout == null)
            {
                return;
            }

            for (var owner = 0; owner < players.Count; owner++)
            {
                if (players[owner].IsEliminated || owner == eliminatedSlot)
                {
                    continue;
                }

                var focusing = CollectUnitsFocusingOpponent(owner, eliminatedSlot);
                if (focusing.Count == 0)
                {
                    continue;
                }

                var next = CenterMarchRetargetRules.ResolveNextAliveClockwise(
                    eliminatedSlot,
                    players,
                    owner);
                if (!next.HasValue)
                {
                    foreach (var id in focusing)
                    {
                        if (_unitById.TryGetValue(id, out var unit))
                        {
                            unit.MarchFocusOpponentSlot = -1;
                        }
                    }

                    continue;
                }

                // Still finishing old mid: only retarget the eventual flank foe.
                var remountIds = new HashSet<int>();
                foreach (var id in focusing)
                {
                    if (!_unitById.TryGetValue(id, out var unit))
                    {
                        continue;
                    }

                    if (unit.CommittedMarchPath != null)
                    {
                        unit.MarchFocusOpponentSlot = next.Value;
                        continue;
                    }

                    remountIds.Add(id);
                }

                if (remountIds.Count == 0)
                {
                    continue;
                }

                RemountUnitsToFlankToward(owner, remountIds, next.Value, layout);
            }
        }

        public void DespawnUnitsForOwner(int ownerSlot)
        {
            for (var i = _pendingSpawns.Count - 1; i >= 0; i--)
            {
                if (_pendingSpawns[i].OwnerSlot == ownerSlot)
                {
                    _pendingSpawns.RemoveAt(i);
                }
            }

            for (var i = _units.Count - 1; i >= 0; i--)
            {
                if (_units[i].OwnerSlot == ownerSlot)
                {
                    _unitById.Remove(_units[i].UnitId);
                    var last = _units.Count - 1;
                    if (i != last) _units[i] = _units[last];
                    _units.RemoveAt(last);
                }
            }

            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                if (_projectiles[i].AttackerOwnerSlot == ownerSlot)
                {
                    var last = _projectiles.Count - 1;
                    if (i != last) _projectiles[i] = _projectiles[last];
                    _projectiles.RemoveAt(last);
                }
            }

            for (var i = _meleeStrikes.Count - 1; i >= 0; i--)
            {
                var attacker = GetUnitById(_meleeStrikes[i].AttackerUnitId);
                if (attacker != null && attacker.OwnerSlot == ownerSlot)
                {
                    var last = _meleeStrikes.Count - 1;
                    if (i != last) _meleeStrikes[i] = _meleeStrikes[last];
                    _meleeStrikes.RemoveAt(last);
                }
            }
        }

        public void HandleWave(BarracksWaveFired wave, ICombatUnitCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (_graph == null
                || !_routes.TryGetRoute(wave.OwnerSlot, wave.LaneId, out var route))
            {
                return;
            }

            var race = catalog.GetRace(wave.OwnerRaceId);
            var squad = catalog.GetSquad(wave.SquadLevel);
            if (race == null || squad == null)
            {
                return;
            }

            var spawnPlan = SquadSpawnRules.BuildSpawnPlan(squad);
            var rearmostRow = SquadSpawnRules.GetRearmostRowIndex(squad);

            var spawnUnitIndex = 0;
            foreach (var slot in spawnPlan)
            {
                var definition = race.GetUnit(slot.Role);
                if (definition == null)
                {
                    continue;
                }

                var stats = UnitCombatStats.FromDefinition(definition);
                var unitMarchSpeed = RaceMarchSpeedRules.GetMarchSpeed(race, definition);
                var spawnDistance = CombatFormationRules.GetSpawnDistanceForRow(
                    slot.RowIndex,
                    rearmostRow,
                    _random)
                    + slot.IndexInRow * CombatFormationRules.SpawnRowDepth * 0.5f
                    + spawnUnitIndex * CombatFormationRules.SpawnRowDepth * 0.12f;
                var formationOffset = CombatFormationRules.BuildRowSpawnFormationOffset(
                    route.Path,
                    slot,
                    _random,
                    spawnDistance,
                    spawnUnitIndex);
                var delaySeconds = SquadSpawnRules.GetSpawnDelaySeconds(spawnUnitIndex);
                spawnUnitIndex++;

                var pending = new PendingBarracksSpawn
                {
                    RemainingSeconds = delaySeconds,
                    OwnerSlot = wave.OwnerSlot,
                    LaneId = wave.LaneId,
                    Role = slot.Role,
                    Stats = stats,
                    MarchMoveSpeed = unitMarchSpeed,
                    SpawnDistance = spawnDistance,
                    FormationOffset = formationOffset,
                };

                if (pending.RemainingSeconds <= 0f)
                {
                    CommitPendingSpawn(pending);
                }
                else
                {
                    _pendingSpawns.Add(pending);
                }
            }
        }

        /// <summary>Explicit spawn for tests and scripted scenarios.</summary>
        public MatchUnitState SpawnUnit(
            int ownerSlot,
            string laneId,
            UnitRole role,
            UnitCombatStats stats,
            float distanceAlongLane = 0f,
            Vector3 formationOffset = default,
            bool isHero = false,
            int heroSlot = 0)
        {
            if (!_routes.TryGetRoute(ownerSlot, laneId, out var route))
            {
                throw new InvalidOperationException($"Lane route not found for slot {ownerSlot}, lane {laneId}.");
            }

            var worldPosition = route.ResolveSpawnPosition(distanceAlongLane, formationOffset);
            var progressDistance = Mathf.Clamp(
                route.ProjectDistanceForward(worldPosition, distanceAlongLane),
                distanceAlongLane - 0.5f,
                distanceAlongLane + 1.25f);
            var unit = new MatchUnitState(
                _nextUnitId++,
                ownerSlot,
                laneId,
                role,
                stats,
                stats.MaxHp,
                worldPosition,
                route.FindMarchWaypointIndex(worldPosition),
                marchSpawnDistance: distanceAlongLane,
                isHero: isHero,
                heroSlot: heroSlot);
            unit.MarchProgressDistance = progressDistance;
            ApplySpawnFacing(unit, route, distanceAlongLane);
            ApplyMarchFocusFromLane(unit, ownerSlot, laneId);
            _units.Add(unit);
            _unitById[unit.UnitId] = unit;
            return unit;
        }

        public MatchUnitState GetUnit(int unitId) => GetUnitById(unitId);

        /// <summary>
        /// Client/host-migration: upsert living units from snapshot; drop missing ones.
        /// Does not run AI — caller must not tick combat on pure clients.
        /// </summary>
        public void ApplyAuthoritativeUnits(
            MatchUnitSnapshot[] snapshots,
            ICombatUnitCatalog catalog = null)
        {
            ClearSpatialGrid();
            var keep = new HashSet<int>();
            if (snapshots != null)
            {
                for (var i = 0; i < snapshots.Length; i++)
                {
                    var snap = snapshots[i];
                    if (!snap.IsAlive || snap.UnitId <= 0)
                    {
                        continue;
                    }

                    keep.Add(snap.UnitId);
                    UpsertAuthoritativeUnit(snap, catalog);
                }
            }

            for (var i = _units.Count - 1; i >= 0; i--)
            {
                if (keep.Contains(_units[i].UnitId))
                {
                    continue;
                }

                _unitById.Remove(_units[i].UnitId);
                var last = _units.Count - 1;
                if (i != last)
                {
                    _units[i] = _units[last];
                }

                _units.RemoveAt(last);
            }
        }

        void UpsertAuthoritativeUnit(MatchUnitSnapshot snap, ICombatUnitCatalog catalog)
        {
            MatchSnapshotCodec.TryParseUnitRole(snap.UnitDefId, out var role);
            var laneId = string.IsNullOrEmpty(snap.LaneId) ? GameIds.Lanes.Center : snap.LaneId;
            var position = new Vector3(snap.PosX, 0.15f, snap.PosZ);
            var facing = new Vector3(snap.FacingX, 0f, snap.FacingZ);
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = Vector3.forward;
            }
            else
            {
                facing.Normalize();
            }

            if (_unitById.TryGetValue(snap.UnitId, out var existing)
                && existing.LaneId == laneId
                && existing.Role == role)
            {
                existing.CurrentHp = Mathf.Max(0f, snap.Health);
                existing.WorldPosition = position;
                existing.FacingDirection = facing;
                existing.MarchProgressDistance = _routes != null
                    && _routes.TryGetRoute(snap.OwnerSlot, laneId, out var route)
                    ? route.ProjectDistance(position)
                    : existing.MarchProgressDistance;
                return;
            }

            if (existing != null)
            {
                _unitById.Remove(existing.UnitId);
                _units.Remove(existing);
            }

            var stats = ResolveSnapshotStats(role, snap.OwnerSlot, snap.Health, catalog);
            var unit = new MatchUnitState(
                snap.UnitId,
                snap.OwnerSlot,
                laneId,
                role,
                stats,
                Mathf.Max(0.01f, snap.Health),
                position,
                marchSpawnDistance: 0f,
                isHero: role == UnitRole.Hero);
            unit.CurrentHp = Mathf.Max(0f, snap.Health);
            unit.FacingDirection = facing;
            if (_routes != null && _routes.TryGetRoute(snap.OwnerSlot, laneId, out var spawnRoute))
            {
                unit.MarchProgressDistance = spawnRoute.ProjectDistance(position);
                unit.MarchWaypointIndex = spawnRoute.FindMarchWaypointIndex(position);
            }

            _units.Add(unit);
            _unitById[unit.UnitId] = unit;
            _nextUnitId = Mathf.Max(_nextUnitId, snap.UnitId + 1);
        }

        UnitCombatStats ResolveSnapshotStats(
            UnitRole role,
            int ownerSlot,
            float snapshotHp,
            ICombatUnitCatalog catalog)
        {
            if (catalog != null && ownerSlot >= 0 && ownerSlot < _players.Count)
            {
                var race = catalog.GetRace(_players[ownerSlot].RaceId);
                var definition = race?.GetUnit(role);
                if (definition != null)
                {
                    return UnitCombatStats.FromDefinition(definition);
                }
            }

            var maxHp = Mathf.Max(snapshotHp, 1f);
            return new UnitCombatStats(
                role,
                maxHp: maxHp,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 1f,
                attackRange: 1.5f,
                moveSpeed: 4f,
                goldBounty: 1);
        }

        public void ApplyExternalDamage(int targetUnitId, float rawDamage, int killerOwnerSlot)
        {
            var target = GetUnitById(targetUnitId);
            if (target == null || !target.IsAlive)
            {
                return;
            }

            ApplyDamage(null, target, rawDamage, killerOwnerSlot);
        }

        /// <summary>Defensive building shot — travels as a projectile, damages on impact.</summary>
        public bool TryFireBuildingProjectile(
            int buildingInstanceId,
            int ownerSlot,
            Vector3 buildingWorldPosition,
            int targetUnitId,
            float rawDamage)
        {
            var target = GetUnitById(targetUnitId);
            if (target == null || !target.IsAlive || rawDamage <= 0f)
            {
                return false;
            }

            var start = buildingWorldPosition + Vector3.up * TowerCombatRules.MuzzleHeight;
            var end = CombatProjectileTrajectory.GetProjectileTarget(target.WorldPosition);
            var duration = CombatProjectileTrajectory.ComputeFlightDuration(
                start,
                end,
                TowerCombatRules.ProjectileSpeed);

            _projectiles.Add(new CombatProjectileState(
                _nextProjectileId++,
                attackerUnitId: 0,
                targetUnitId,
                ownerSlot,
                UnitRole.Ranged,
                GetPlayerRaceId(ownerSlot),
                rawDamage,
                duration,
                start,
                end,
                isParabolic: false,
                targetBuildingInstanceId: null,
                sourceBuildingInstanceId: buildingInstanceId));
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            TickPendingSpawns(deltaTime);

            if (_units.Count == 0 && _projectiles.Count == 0 && _meleeStrikes.Count == 0)
            {
                return;
            }

            RebuildSpatialGrid();

            _tickBuffer.Clear();
            _tickBuffer.AddRange(_units);
            _tickBuffer.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
            foreach (var unit in _tickBuffer)
            {
                if (unit.IsAlive)
                {
                    TickUnit(unit, deltaTime);
                }
            }

            TickProjectiles(deltaTime);
            TickMeleeStrikes(deltaTime);
        }

        void TickPendingSpawns(float deltaTime)
        {
            if (_pendingSpawns.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _pendingSpawns.Count;)
            {
                var pending = _pendingSpawns[i];
                pending.RemainingSeconds -= deltaTime;
                if (pending.RemainingSeconds > 0f)
                {
                    i++;
                    continue;
                }

                CommitPendingSpawn(pending);
                _pendingSpawns.RemoveAt(i);
            }
        }

        void CommitPendingSpawn(PendingBarracksSpawn pending)
        {
            if (!_routes.TryGetRoute(pending.OwnerSlot, pending.LaneId, out var route))
            {
                return;
            }

            var worldPosition = route.ResolveSpawnPosition(pending.SpawnDistance, pending.FormationOffset);
            var progressDistance = Mathf.Clamp(
                route.ProjectDistanceForward(worldPosition, pending.SpawnDistance),
                pending.SpawnDistance - 0.5f,
                pending.SpawnDistance + 1.25f);
            var unit = new MatchUnitState(
                _nextUnitId++,
                pending.OwnerSlot,
                pending.LaneId,
                pending.Role,
                pending.Stats,
                pending.Stats.MaxHp,
                worldPosition,
                route.FindMarchWaypointIndex(worldPosition),
                pending.MarchMoveSpeed,
                pending.SpawnDistance);
            unit.MarchProgressDistance = progressDistance;
            ApplySpawnFacing(unit, route, pending.SpawnDistance);
            ApplyMarchFocusFromLane(unit, pending.OwnerSlot, pending.LaneId);
            _units.Add(unit);
            _unitById[unit.UnitId] = unit;
        }

        void ApplyMarchFocusFromLane(MatchUnitState unit, int ownerSlot, string laneId)
        {
            if (unit == null || _graph == null || string.IsNullOrEmpty(laneId))
            {
                return;
            }

            if (_graph.TryGetLane(ownerSlot, laneId, out var lane) && lane != null)
            {
                unit.MarchFocusOpponentSlot = lane.OpponentSlot;
            }
        }

        static void ApplySpawnFacing(MatchUnitState unit, LaneRoute route, float distanceAlongLane)
        {
            var facing = route.EvaluateDirectionAtDistance(distanceAlongLane);
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                unit.FacingDirection = facing.normalized;
            }
        }

        void TickUnit(MatchUnitState unit, float deltaTime)
        {
            if (unit.IsParkedAtBase)
            {
                return;
            }

            if (!TryGetEffectiveRoute(unit, out var route))
            {
                return;
            }

            TryCompleteCommittedMidMarch(unit);
            if (unit.CommittedMarchPath == null
                && !TryGetEffectiveRoute(unit, out route))
            {
                return;
            }

            unit.TargetScanCooldown -= deltaTime;
            ResolveCurrentTarget(unit);
            ResolveCurrentBuildingTarget(unit);

            if (unit.CurrentTargetId == null
                && unit.CurrentTargetBuildingInstanceId == null
                && unit.TargetScanCooldown <= 0f)
            {
                unit.TargetScanCooldown = UnitLocomotionRules.TargetScanInterval;
                ScanForNearestTarget(unit);
                TryAcquireEndOfLaneBuilding(unit, route);
            }

            var buildingTarget = GetBuildingByInstanceId(unit.CurrentTargetBuildingInstanceId);
            if (buildingTarget != null)
            {
                var buildingDistance = GetBuildingSurfaceDistance(unit.WorldPosition, buildingTarget);
                var buildingReach = CombatRules.GetBuildingAttackReach(unit.Stats.AttackRange);
                if (buildingDistance <= buildingReach)
                {
                    unit.BehaviorState = UnitBehaviorState.Attack;
                    TickBuildingAttack(unit, buildingTarget, deltaTime);
                    return;
                }

                unit.BehaviorState = UnitBehaviorState.Chase;
                TickChaseBuilding(unit, route, buildingTarget, deltaTime);
                return;
            }

            var target = GetUnitById(unit.CurrentTargetId);
            if (target != null && CombatRules.CanAttackTarget(unit.Role, target.Role))
            {
                var distance = HorizontalDistance(unit.WorldPosition, target.WorldPosition);
                if (distance <= unit.Stats.AttackRange)
                {
                    unit.BehaviorState = UnitBehaviorState.Attack;
                    TickAttack(unit, target, deltaTime);
                    return;
                }

                unit.BehaviorState = UnitBehaviorState.Chase;
                TickChase(unit, route, target, deltaTime);
                return;
            }

            unit.BehaviorState = UnitBehaviorState.Move;
            TickMove(unit, route, deltaTime);
            TryCompleteCommittedMidMarch(unit);
        }

        void TickMove(MatchUnitState unit, LaneRoute route, float deltaTime)
        {
            var maxStep = unit.MarchMoveSpeed * deltaTime;
            if (maxStep <= 0.0001f)
            {
                return;
            }

            unit.MarchProgressDistance = route.ProjectDistanceForward(
                unit.WorldPosition,
                unit.MarchProgressDistance);

            var destination = UnitLocomotionRules.GetRouteLookaheadDestination(
                route,
                unit.WorldPosition,
                maxStep,
                unit.MarchProgressDistance);
            destination = ApplyRouteBypassIfBlocked(unit, route, destination);
            var allies = CollectMarchAlliesForAvoidance(unit, route);
            var previousPosition = unit.WorldPosition;
            var proposed = UnitLocomotionRules.MoveTowards(
                unit.WorldPosition,
                destination,
                maxStep,
                allies,
                out var moveFacing,
                unit.UnitId);
            unit.WorldPosition = ApplyWalkableMovement(
                route,
                previousPosition,
                proposed,
                maxStep,
                unit.MarchProgressDistance);
            unit.MarchProgressDistance = route.AdvanceProgress(
                unit.MarchProgressDistance,
                route.ProjectDistanceForward(unit.WorldPosition, unit.MarchProgressDistance));
            ApplyMoveFacing(unit, moveFacing, previousPosition, deltaTime);
        }

        void TickChase(MatchUnitState unit, LaneRoute route, MatchUnitState target, float deltaTime)
        {
            if (target == null || !target.IsAlive)
            {
                ClearTarget(unit);
                return;
            }

            if (!CombatRules.CanAttackTarget(unit.Role, target.Role))
            {
                ClearTarget(unit);
                return;
            }

            var maxStep = unit.MarchMoveSpeed * deltaTime;
            if (maxStep <= 0.0001f)
            {
                return;
            }

            var allies = CollectAlliesForAvoidance(unit);
            var previousPosition = unit.WorldPosition;
            var proposed = UnitLocomotionRules.MoveTowards(
                unit.WorldPosition,
                target.WorldPosition,
                maxStep,
                allies,
                out var moveFacing,
                unit.UnitId);
            unit.WorldPosition = ApplyWalkableMovement(
                route,
                previousPosition,
                proposed,
                maxStep,
                route.ProjectDistance(proposed));
            unit.MarchProgressDistance = route.AdvanceProgress(
                unit.MarchProgressDistance,
                route.ProjectDistanceForward(unit.WorldPosition, unit.MarchProgressDistance));
            ApplyMoveFacing(unit, moveFacing, previousPosition, deltaTime);
        }

        void TickAttack(MatchUnitState unit, MatchUnitState target, float deltaTime)
        {
            if (target == null || !target.IsAlive)
            {
                ClearTarget(unit);
                return;
            }

            if (!CombatRules.CanAttackTarget(unit.Role, target.Role))
            {
                ClearTarget(unit);
                return;
            }

            var distance = HorizontalDistance(unit.WorldPosition, target.WorldPosition);
            if (distance > unit.Stats.AttackRange)
            {
                unit.BehaviorState = UnitBehaviorState.Chase;
                return;
            }

            UpdateFacingTowards(unit, target.WorldPosition, deltaTime);

            unit.AttackCooldownRemaining -= deltaTime;
            if (unit.AttackCooldownRemaining <= 0f)
            {
                BeginAttack(unit, target);
                unit.AttackCooldownRemaining = CombatRules.GetAttackIntervalSeconds(unit.Stats.AttackSpeed);
            }

            if (target == null || !target.IsAlive)
            {
                ClearTarget(unit);
            }
        }

        void ResolveCurrentTarget(MatchUnitState unit)
        {
            if (unit.CurrentTargetId == null)
            {
                if (!unit.CurrentTargetBuildingInstanceId.HasValue
                    && unit.BehaviorState is UnitBehaviorState.Chase or UnitBehaviorState.Attack)
                {
                    unit.BehaviorState = UnitBehaviorState.Move;
                }

                return;
            }

            var target = GetUnitById(unit.CurrentTargetId);
            if (target == null || !target.IsAlive)
            {
                ClearTarget(unit);
                return;
            }

            if (!IsTargetInAggroRange(unit, target))
            {
                ClearTarget(unit);
                return;
            }

            if (!CombatLaneRules.CanEngage(unit, target, _graph)
                || !CombatRules.CanAttackTarget(unit.Role, target.Role))
            {
                ClearTarget(unit);
            }
        }

        void ScanForNearestTarget(MatchUnitState unit)
        {
            var myPosition = unit.WorldPosition;
            var aggroRadius = CombatRules.GetAggroRadius(unit.Stats);
            MatchUnitState bestUnit = null;
            var bestUnitScore = float.MaxValue;

            _nearbyBuffer.Clear();
            QueryNearbyUnits(myPosition, aggroRadius, _nearbyBuffer);

            foreach (var other in _nearbyBuffer)
            {
                if (!CombatLaneRules.CanEngage(unit, other, _graph))
                {
                    continue;
                }

                if (!CombatRules.CanAttackTarget(unit.Role, other.Role))
                {
                    continue;
                }

                var distance = HorizontalDistance(myPosition, other.WorldPosition);
                if (distance > aggroRadius)
                {
                    continue;
                }

                var score = distance;
                if (IsEngagedByAlly(other, unit.OwnerSlot))
                {
                    score *= 0.5f;
                }

                if (score < bestUnitScore)
                {
                    bestUnitScore = score;
                    bestUnit = other;
                }
            }

            BuildingState bestBuilding = null;
            var bestBuildingScore = float.MaxValue;
            if (_buildings != null)
            {
                bestBuilding = _buildings.FindBuildingTarget(
                    unit.OwnerSlot,
                    unit.LaneId,
                    myPosition,
                    aggroRadius,
                    _graph);
                if (bestBuilding != null)
                {
                    bestBuildingScore = GetBuildingSurfaceDistance(myPosition, bestBuilding);
                }
            }

            unit.CurrentTargetId = null;
            unit.CurrentTargetBuildingInstanceId = null;

            if (bestUnit == null && bestBuilding == null)
            {
                return;
            }

            if (bestBuilding == null || (bestUnit != null && bestUnitScore <= bestBuildingScore))
            {
                unit.CurrentTargetId = bestUnit.UnitId;
                return;
            }

            unit.CurrentTargetBuildingInstanceId = bestBuilding.InstanceId;
        }

        static float GetBuildingSurfaceDistance(Vector3 from, BuildingState building)
        {
            var centerDistance = HorizontalDistance(from, building.WorldPosition);
            return BuildingRules.GetSurfaceDistance(centerDistance, building.BuildingId);
        }

        static Vector3 GetBuildingEngagePoint(Vector3 from, BuildingState building)
        {
            var center = building.WorldPosition;
            center.y = 0f;
            from.y = 0f;
            var away = from - center;
            var dist = away.magnitude;
            var radius = BuildingRules.GetEngageRadius(building.BuildingId);
            if (dist < 0.0001f)
            {
                return center + Vector3.forward * radius;
            }

            return center + away / dist * radius;
        }

        bool IsTargetInAggroRange(MatchUnitState unit, MatchUnitState target)
        {
            var aggroRadius = CombatRules.GetAggroRadius(unit.Stats);
            return HorizontalDistance(unit.WorldPosition, target.WorldPosition) <= aggroRadius;
        }

        void ClearTarget(MatchUnitState unit)
        {
            unit.CurrentTargetId = null;
            unit.CurrentTargetBuildingInstanceId = null;
            unit.BehaviorState = UnitBehaviorState.Move;
            unit.TargetScanCooldown = 0f;
        }

        BuildingState GetBuildingByInstanceId(int? instanceId)
        {
            if (!instanceId.HasValue || _buildings == null)
            {
                return null;
            }

            return _buildings.GetByInstanceId(instanceId.Value);
        }

        void ResolveCurrentBuildingTarget(MatchUnitState unit)
        {
            if (!unit.CurrentTargetBuildingInstanceId.HasValue)
            {
                return;
            }

            var building = GetBuildingByInstanceId(unit.CurrentTargetBuildingInstanceId);
            if (building == null
                || building.IsRuins
                || !BuildingRules.CanLaneAttackBuilding(
                    unit.OwnerSlot,
                    unit.LaneId,
                    building,
                    _graph))
            {
                unit.CurrentTargetBuildingInstanceId = null;
            }
        }

        /// <summary>
        /// At open-path finish with nothing in aggro: push toward nearest remaining enemy building
        /// (e.g. flank barracks past Main — outside normal aggro).
        /// </summary>
        void TryAcquireEndOfLaneBuilding(MatchUnitState unit, LaneRoute route)
        {
            if (unit.CurrentTargetId != null || unit.CurrentTargetBuildingInstanceId != null)
            {
                return;
            }

            if (_buildings == null || route == null || route.IsClosedLoop)
            {
                return;
            }

            if (!CenterMarchRetargetRules.HasReachedRouteEnd(
                    unit.MarchProgressDistance,
                    route.TotalLength,
                    unit.WorldPosition,
                    route.Path.End))
            {
                return;
            }

            var building = _buildings.FindNearestEnemyBuilding(
                unit.OwnerSlot,
                unit.LaneId,
                unit.WorldPosition,
                _graph);
            if (building != null)
            {
                unit.CurrentTargetBuildingInstanceId = building.InstanceId;
            }
        }

        void TickChaseBuilding(
            MatchUnitState unit,
            LaneRoute route,
            BuildingState building,
            float deltaTime)
        {
            if (building == null || building.IsRuins)
            {
                unit.CurrentTargetBuildingInstanceId = null;
                return;
            }

            var maxStep = unit.MarchMoveSpeed * deltaTime;
            if (maxStep <= 0.0001f)
            {
                return;
            }

            var allies = CollectAlliesForAvoidance(unit);
            var previousPosition = unit.WorldPosition;
            var engagePoint = GetBuildingEngagePoint(unit.WorldPosition, building);
            var proposed = UnitLocomotionRules.MoveTowards(
                unit.WorldPosition,
                engagePoint,
                maxStep,
                allies,
                out var moveFacing,
                unit.UnitId);
            unit.WorldPosition = ApplyWalkableMovement(
                route,
                previousPosition,
                proposed,
                maxStep,
                route.ProjectDistance(proposed));
            unit.MarchProgressDistance = route.AdvanceProgress(
                unit.MarchProgressDistance,
                route.ProjectDistanceForward(unit.WorldPosition, unit.MarchProgressDistance));
            ApplyMoveFacing(unit, moveFacing, previousPosition, deltaTime);
        }

        Vector3 ApplyWalkableMovement(
            LaneRoute route,
            Vector3 previousPosition,
            Vector3 proposedPosition,
            float maxStep,
            float progressDistance)
        {
            var centerRadius = _graph != null
                ? _graph.CenterArenaRadius
                : LaneGraphBuilder.DefaultCenterArenaRadius;
            return UnitLocomotionRules.ApplyWalkableLimit(
                route,
                previousPosition,
                proposedPosition,
                maxStep,
                progressDistance,
                centerRadius,
                _walkable);
        }

        void TickBuildingAttack(MatchUnitState unit, BuildingState building, float deltaTime)
        {
            if (building == null || building.IsRuins)
            {
                unit.CurrentTargetBuildingInstanceId = null;
                return;
            }

            var distance = GetBuildingSurfaceDistance(unit.WorldPosition, building);
            var buildingReach = CombatRules.GetBuildingAttackReach(unit.Stats.AttackRange);
            if (distance > buildingReach)
            {
                unit.BehaviorState = UnitBehaviorState.Chase;
                return;
            }

            UpdateFacingTowards(unit, building.WorldPosition, deltaTime);

            unit.AttackCooldownRemaining -= deltaTime;
            if (unit.AttackCooldownRemaining <= 0f)
            {
                BeginBuildingAttack(unit, building);
                unit.AttackCooldownRemaining = CombatRules.GetAttackIntervalSeconds(unit.Stats.AttackSpeed);
            }
        }

        void BeginBuildingAttack(MatchUnitState attacker, BuildingState building)
        {
            if (attacker == null || building == null || building.IsRuins || _buildings == null)
            {
                return;
            }

            attacker.AttackSwingSerial++;
            var rawDamage = CombatRules.RollDamage(
                attacker.Stats.DamageMin,
                attacker.Stats.DamageMax,
                _random);

            if (CombatAttackRules.UsesMeleeStrike(attacker.Role)
                || !CombatAttackRules.UsesProjectile(attacker.Role))
            {
                _meleeStrikes.Add(new CombatMeleeStrikeState(
                    attacker.UnitId,
                    targetUnitId: -1,
                    rawDamage,
                    CombatAttackRules.MeleeStrikeDuration,
                    targetBuildingInstanceId: building.InstanceId));
                return;
            }

            var start = CombatProjectileTrajectory.GetProjectileOrigin(attacker.WorldPosition);
            var end = CombatProjectileTrajectory.GetProjectileTarget(building.WorldPosition);
            var duration = CombatProjectileTrajectory.ComputeFlightDuration(
                start,
                end,
                CombatAttackRules.ProjectileSpeed);
            _projectiles.Add(new CombatProjectileState(
                _nextProjectileId++,
                attacker.UnitId,
                targetUnitId: -1,
                attacker.OwnerSlot,
                attacker.Role,
                GetPlayerRaceId(attacker.OwnerSlot),
                rawDamage,
                duration,
                start,
                end,
                isParabolic: false,
                targetBuildingInstanceId: building.InstanceId));
        }

        MatchUnitState GetUnitById(int? unitId)
        {
            if (unitId == null || !_unitById.TryGetValue(unitId.Value, out var unit))
            {
                return null;
            }

            return unit;
        }

        List<MatchUnitState> CollectMarchAlliesForAvoidance(MatchUnitState unit, LaneRoute route)
        {
            _nearbyBuffer.Clear();
            var queryRadius = UnitLocomotionRules.AvoidanceRadius * 3f;
            QueryNearbyUnits(unit.WorldPosition, queryRadius, _nearbyBuffer);
            var allies = _alliesBuffer;
            allies.Clear();
            var myPosition = unit.WorldPosition;
            var myDistance = unit.MarchProgressDistance;
            var forward = route.EvaluateDirectionAtDistance(myDistance);
            var aheadGapMax = CombatFormationRules.MinLaneFollowGap * 1.5f;

            foreach (var other in _nearbyBuffer)
            {
                if (other.UnitId == unit.UnitId
                    || !other.IsAlive
                    || other.OwnerSlot != unit.OwnerSlot
                    || other.LaneId != unit.LaneId)
                {
                    continue;
                }

                var otherDistance = other.MarchProgressDistance;
                if (otherDistance <= myDistance + 0.35f
                    || otherDistance > myDistance + aheadGapMax)
                {
                    continue;
                }

                if (otherDistance + CombatFormationRules.MinLaneFollowGap < myDistance)
                {
                    continue;
                }

                if (HorizontalDistanceSq(myPosition, other.WorldPosition) > queryRadius * queryRadius)
                {
                    continue;
                }

                var toOther = other.WorldPosition - myPosition;
                toOther.y = 0f;
                if (toOther.sqrMagnitude < 0.001f
                    || Vector3.Dot(toOther.normalized, forward) < 0.35f)
                {
                    continue;
                }

                allies.Add(other);
            }

            return allies;
        }

        List<MatchUnitState> CollectAlliesForAvoidance(MatchUnitState unit)
        {
            _nearbyBuffer.Clear();
            var queryRadius = UnitLocomotionRules.AvoidanceRadius * 3f;
            QueryNearbyUnits(unit.WorldPosition, queryRadius, _nearbyBuffer);
            var allies = _alliesBuffer;
            allies.Clear();
            var radiusSq = queryRadius * queryRadius;
            var myPosition = unit.WorldPosition;

            foreach (var other in _nearbyBuffer)
            {
                if (other.UnitId == unit.UnitId || !other.IsAlive || other.OwnerSlot != unit.OwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(myPosition, other.WorldPosition) > radiusSq)
                {
                    continue;
                }

                allies.Add(other);
            }

            return allies;
        }

        Vector3 ApplyRouteBypassIfBlocked(MatchUnitState unit, LaneRoute route, Vector3 destination)
        {
            var myDistance = route.ProjectDistanceForward(unit.WorldPosition, unit.MarchProgressDistance);
            var forward = route.EvaluateDirectionAtDistance(myDistance);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var aheadGap = CombatFormationRules.MinLaneFollowGap * 0.5f;

            _nearbyBuffer.Clear();
            QueryNearbyUnits(unit.WorldPosition, CombatFormationRules.MinUnitSeparation * 3f, _nearbyBuffer);

            foreach (var other in _nearbyBuffer)
            {
                if (other.UnitId == unit.UnitId
                    || !other.IsAlive
                    || other.OwnerSlot != unit.OwnerSlot
                    || other.LaneId != unit.LaneId)
                {
                    continue;
                }

                var otherDistance = route.ProjectDistanceForward(
                    other.WorldPosition,
                    other.MarchProgressDistance);
                if (otherDistance <= myDistance + aheadGap)
                {
                    continue;
                }

                if (otherDistance - myDistance > 5f)
                {
                    continue;
                }

                var toOther = other.WorldPosition - unit.WorldPosition;
                toOther.y = 0f;
                if (toOther.sqrMagnitude < 0.001f
                    || Vector3.Dot(toOther.normalized, forward) < 0.45f)
                {
                    continue;
                }

                var spreadSign = unit.UnitId % 2 == 0 ? 1f : -1f;
                return destination + right * (CombatFormationRules.MinUnitSeparation * spreadSign);
            }

            return destination;
        }

        bool IsEngagedByAlly(MatchUnitState enemy, int ownerSlot)
        {
            _nestedBuffer.Clear();
            QueryNearbyUnits(enemy.WorldPosition, 16f, _nestedBuffer);

            foreach (var ally in _nestedBuffer)
            {
                if (!ally.IsAlive || ally.OwnerSlot != ownerSlot || ally.UnitId == enemy.UnitId)
                {
                    continue;
                }

                if (HorizontalDistance(ally.WorldPosition, enemy.WorldPosition) <= ally.Stats.AttackRange * 1.25f)
                {
                    return true;
                }
            }

            return false;
        }

        void UpdateFacingTowards(MatchUnitState unit, Vector3 targetPosition, float deltaTime)
        {
            var direction = targetPosition - unit.WorldPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                unit.FacingDirection = UnitLocomotionRules.StepFacingTowards(
                    unit.FacingDirection,
                    direction,
                    deltaTime);
            }
        }

        static void ApplyMoveFacing(
            MatchUnitState unit,
            Vector3 moveFacing,
            Vector3 previousPosition,
            float deltaTime)
        {
            moveFacing.y = 0f;
            if (moveFacing.sqrMagnitude > 0.0001f)
            {
                unit.FacingDirection = UnitLocomotionRules.StepFacingTowards(
                    unit.FacingDirection,
                    moveFacing,
                    deltaTime);
                return;
            }

            ApplyFacingFromMovement(unit, previousPosition, deltaTime);
        }

        static void ApplyFacingFromMovement(MatchUnitState unit, Vector3 previousPosition, float deltaTime)
        {
            if (UnitLocomotionRules.TryGetFacingFromDisplacement(
                    previousPosition,
                    unit.WorldPosition,
                    out var facing))
            {
                unit.FacingDirection = UnitLocomotionRules.StepFacingTowards(
                    unit.FacingDirection,
                    facing,
                    deltaTime);
            }
        }

        void BeginAttack(MatchUnitState attacker, MatchUnitState target)
        {
            if (attacker == null || target == null || !target.IsAlive)
            {
                return;
            }

            attacker.AttackSwingSerial++;
            var rawDamage = CombatRules.RollDamage(
                attacker.Stats.DamageMin,
                attacker.Stats.DamageMax,
                _random);

            if (CombatAttackRules.UsesMeleeStrike(attacker.Role))
            {
                _meleeStrikes.Add(new CombatMeleeStrikeState(
                    attacker.UnitId,
                    target.UnitId,
                    rawDamage,
                    CombatAttackRules.MeleeStrikeDuration));
                return;
            }

            if (!CombatAttackRules.UsesProjectile(attacker.Role))
            {
                ApplyDamage(attacker, target, rawDamage, attacker.OwnerSlot);
                return;
            }

            var start = CombatProjectileTrajectory.GetProjectileOrigin(attacker.WorldPosition);
            var end = CombatProjectileTrajectory.GetProjectileTarget(target.WorldPosition);
            var isParabolic = CombatAttackRules.UsesParabolicArc(attacker.Role);
            var duration = CombatProjectileTrajectory.ComputeFlightDuration(
                start,
                end,
                CombatAttackRules.ProjectileSpeed);
            _projectiles.Add(new CombatProjectileState(
                _nextProjectileId++,
                attacker.UnitId,
                target.UnitId,
                attacker.OwnerSlot,
                attacker.Role,
                GetPlayerRaceId(attacker.OwnerSlot),
                rawDamage,
                duration,
                start,
                end,
                isParabolic));
        }

        void TickProjectiles(float deltaTime)
        {
            if (_projectiles.Count == 0)
            {
                return;
            }

            // Collect impacts first — ApplyDamage / building ruin may DespawnUnitsForOwner
            // and mutate _projectiles (reentrancy). Resolving mid-loop throws IndexOutOfRange.
            _projectileImpactBuffer.Clear();
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                projectile.Elapsed += deltaTime;
                if (projectile.Elapsed < projectile.FlightDuration)
                {
                    continue;
                }

                var last = _projectiles.Count - 1;
                if (i != last)
                {
                    _projectiles[i] = _projectiles[last];
                }

                _projectiles.RemoveAt(last);
                _projectileImpactBuffer.Add(projectile);
            }

            for (var i = 0; i < _projectileImpactBuffer.Count; i++)
            {
                ResolveProjectileImpact(_projectileImpactBuffer[i]);
            }

            _projectileImpactBuffer.Clear();
        }

        void ResolveProjectileImpact(CombatProjectileState projectile)
        {
            if (projectile.TargetBuildingInstanceId.HasValue)
            {
                _buildings?.TryApplyDamage(
                    projectile.TargetBuildingInstanceId.Value,
                    projectile.RawDamage,
                    projectile.AttackerOwnerSlot);
                return;
            }

            var attacker = GetUnitById(projectile.AttackerUnitId);
            var target = GetUnitById(projectile.TargetUnitId);
            if (target != null && target.IsAlive)
            {
                ApplyDamage(attacker, target, projectile.RawDamage, projectile.AttackerOwnerSlot);
            }
        }

        void TickMeleeStrikes(float deltaTime)
        {
            if (_meleeStrikes.Count == 0)
            {
                return;
            }

            _meleeImpactBuffer.Clear();
            for (var i = _meleeStrikes.Count - 1; i >= 0; i--)
            {
                var strike = _meleeStrikes[i];
                strike.TimeRemaining -= deltaTime;
                if (strike.TimeRemaining > 0f)
                {
                    continue;
                }

                var last = _meleeStrikes.Count - 1;
                if (i != last)
                {
                    _meleeStrikes[i] = _meleeStrikes[last];
                }

                _meleeStrikes.RemoveAt(last);
                _meleeImpactBuffer.Add(strike);
            }

            for (var i = 0; i < _meleeImpactBuffer.Count; i++)
            {
                ResolveMeleeImpact(_meleeImpactBuffer[i]);
            }

            _meleeImpactBuffer.Clear();
        }

        void ResolveMeleeImpact(CombatMeleeStrikeState strike)
        {
            var attacker = GetUnitById(strike.AttackerUnitId);
            if (strike.TargetBuildingInstanceId.HasValue)
            {
                var killerSlot = attacker?.OwnerSlot ?? GetUnitOwnerSlot(strike.AttackerUnitId);
                _buildings?.TryApplyDamage(
                    strike.TargetBuildingInstanceId.Value,
                    strike.RawDamage,
                    killerSlot);
                return;
            }

            var target = GetUnitById(strike.TargetUnitId);
            if (target != null && target.IsAlive)
            {
                var killerSlot = attacker?.OwnerSlot ?? GetUnitOwnerSlot(strike.AttackerUnitId);
                ApplyDamage(attacker, target, strike.RawDamage, killerSlot);
            }
        }

        void ApplyDamage(MatchUnitState attacker, MatchUnitState target, float rawDamage, int killerOwnerSlot)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            var damage = CombatRules.ApplyArmor(rawDamage, target.Stats.Armor);
            target.CurrentHp -= damage;

            if (target.IsAlive)
            {
                return;
            }

            if (attacker != null && attacker.CurrentTargetId == target.UnitId)
            {
                ClearTarget(attacker);
            }

            var bounty = CombatRules.ComputeKillBounty(target.Stats.GoldBounty, target.IsHero);
            GrantGold(killerOwnerSlot, bounty);
            RemoveUnit(target);
            UnitKilled?.Invoke(new UnitKillEvent(
                killerOwnerSlot,
                target.OwnerSlot,
                target.UnitId,
                bounty,
                target.Role));
        }

        int GetUnitOwnerSlot(int unitId)
        {
            var unit = GetUnitById(unitId);
            return unit?.OwnerSlot ?? 0;
        }

        string GetPlayerRaceId(int ownerSlot)
        {
            foreach (var player in _players)
            {
                if (player.SlotIndex == ownerSlot)
                {
                    return player.RaceId;
                }
            }

            return GameIds.Races.Human;
        }

        void GrantGold(int ownerSlot, int amount)
        {
            foreach (var player in _players)
            {
                if (player.SlotIndex == ownerSlot)
                {
                    player.Gold += amount;
                    return;
                }
            }
        }

        public bool DespawnUnit(int unitId)
        {
            var unit = GetUnitById(unitId);
            if (unit == null)
            {
                return false;
            }

            RemoveUnit(unit);
            return true;
        }

        void RemoveUnit(MatchUnitState unit)
        {
            var index = _units.IndexOf(unit);
            if (index < 0) return;
            ClearCommittedMarch(unit);
            var last = _units.Count - 1;
            if (index != last) _units[index] = _units[last];
            _units.RemoveAt(last);
            _unitById.Remove(unit.UnitId);
        }

        void RebuildSpatialGrid()
        {
            foreach (var kvp in _spatialGrid)
                kvp.Value.Clear();
            _spatialGrid.Clear();

            foreach (var unit in _units)
            {
                if (!unit.IsAlive) continue;
                var key = GetSpatialKey(unit.WorldPosition);
                if (!_spatialGrid.TryGetValue(key, out var cell))
                {
                    cell = new List<MatchUnitState>();
                    _spatialGrid[key] = cell;
                }
                cell.Add(unit);
            }
        }

        void ClearSpatialGrid()
        {
            foreach (var kvp in _spatialGrid)
                kvp.Value.Clear();
            _spatialGrid.Clear();
        }

        static (int, int) GetSpatialKey(Vector3 position)
        {
            return ((int)Math.Floor(position.x / SpatialCellSize),
                    (int)Math.Floor(position.z / SpatialCellSize));
        }

        void QueryNearbyUnits(Vector3 position, float radius, List<MatchUnitState> results)
        {
            var minCx = (int)Math.Floor((position.x - radius) / SpatialCellSize);
            var maxCx = (int)Math.Floor((position.x + radius) / SpatialCellSize);
            var minCz = (int)Math.Floor((position.z - radius) / SpatialCellSize);
            var maxCz = (int)Math.Floor((position.z + radius) / SpatialCellSize);

            for (var cx = minCx; cx <= maxCx; cx++)
            {
                for (var cz = minCz; cz <= maxCz; cz++)
                {
                    if (!_spatialGrid.TryGetValue((cx, cz), out var cell))
                        continue;
                    foreach (var unit in cell)
                        results.Add(unit);
                }
            }
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }
    }
}
