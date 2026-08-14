using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public sealed class MatchCombatSystem : IProjectileImpactHandler, IMeleeImpactHandler
    {
        readonly List<MatchUnitState> _units = new();
        readonly Dictionary<int, MatchUnitState> _unitById = new();
        readonly List<MatchPlayerState> _players = new();
        readonly CombatProjectileSystem _projectiles = new();
        readonly CombatMeleeStrikeSystem _meleeStrikes = new();
        readonly CombatSpatialGrid _spatialGrid = new();

        LaneGraph _graph;
        LaneRouteRegistry _routes;
        BuildingRegistry _buildings;
        WalkableSurface _walkable;
        MatchArenaLayout _layout;
        int _nextUnitId = 1;
        System.Random _random;

        readonly List<MatchUnitState> _tickBuffer = new();
        readonly List<MatchUnitState> _nearbyBuffer = new();
        readonly List<MatchUnitState> _alliesBuffer = new();
        readonly List<MatchUnitState> _nestedBuffer = new();
        readonly List<PendingBarracksSpawn> _pendingSpawns = new();
        readonly Dictionary<int, LaneRoute> _committedRoutes = new();
        readonly List<CombatCorpseState> _corpses = new();
        readonly List<CasterSpellCastEvent> _networkSpellCasts = new();
        readonly List<CasterSpellCastEvent> _presenterSpellCasts = new();
        readonly List<HeroAbilityCastEvent> _networkHeroCasts = new();
        readonly List<HeroAbilityCastEvent> _presenterHeroCasts = new();
        readonly List<MatchProjectileSnapshot> _networkProjectileSpawns = new();
        int _spellCastSerial;
        int _lastAppliedSpellSerial;
        int _lastAppliedProjectileId;

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
        public event Action<CasterSpellCastEvent> SpellCast;
        public event Action<HeroAbilityCastEvent> HeroSpellCast;

        public UnitVisualCatalog UnitVisualCatalog { get; set; }

        public IReadOnlyList<MatchUnitState> Units => _units;
        public IReadOnlyList<CombatProjectileState> Projectiles => _projectiles.Active;
        public IReadOnlyList<CombatMeleeStrikeState> MeleeStrikes => _meleeStrikes.Active;
        /// <summary>Transient host-side corpses kept for resurrect (cull age = CasterSpellRules.ResurrectCorpseMaxAgeSeconds).</summary>
        public IReadOnlyList<CombatCorpseState> Corpses => _corpses;

        /// <summary>
        /// Casts not yet captured into a snapshot. Host: drained by <see cref="MatchSnapshotCodec.Capture"/>.
        /// Used to sync spell VFX to clients.
        /// </summary>
        public IReadOnlyList<CasterSpellCastEvent> NetworkSpellCasts => _networkSpellCasts;

        /// <summary>Host-only: consumed by the snapshot capture into the next <see cref="NetworkSpellCasts"/>.</summary>
        public void ClearNetworkSpellCasts() => _networkSpellCasts.Clear();

        /// <summary>
        /// Casts pending presenter playback (host + clients). Consumers call this once per sync tick;
        /// the same serial never reappears thanks to snapshot v9 dedup on clients.
        /// </summary>
        public IReadOnlyList<CasterSpellCastEvent> ConsumePendingSpellCasts()
        {
            var result = new List<CasterSpellCastEvent>(_presenterSpellCasts);
            _presenterSpellCasts.Clear();
            return result;
        }

        /// <summary>
        /// Hero ability casts not yet captured into a snapshot (mirrors <see cref="NetworkSpellCasts"/>).
        /// </summary>
        public IReadOnlyList<HeroAbilityCastEvent> NetworkHeroCasts => _networkHeroCasts;

        /// <summary>Host-only: consumed by the snapshot capture into the next <see cref="NetworkHeroCasts"/>.</summary>
        public void ClearNetworkHeroCasts() => _networkHeroCasts.Clear();

        /// <summary>Hero ability casts pending presenter playback (host + clients).</summary>
        public IReadOnlyList<HeroAbilityCastEvent> ConsumePendingHeroAbilityCasts()
        {
            var result = new List<HeroAbilityCastEvent>(_presenterHeroCasts);
            _presenterHeroCasts.Clear();
            return result;
        }

        /// <summary>
        /// Projectile spawn events not yet captured into a snapshot (mirrors <see cref="NetworkSpellCasts"/>).
        /// </summary>
        public IReadOnlyList<MatchProjectileSnapshot> NetworkProjectileSpawns => _networkProjectileSpawns;

        /// <summary>Host-only: consumed by snapshot capture into the next <see cref="NetworkProjectileSpawns"/>.</summary>
        public void ClearNetworkProjectileSpawns() => _networkProjectileSpawns.Clear();

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

        public bool TryGetUnitWorldPosition(int unitId, out Vector3 position)
        {
            return TryGetUnitWorldPosition(GetUnitById(unitId), out position);
        }

        public void Reset(IReadOnlyList<MatchPlayerState> players, LaneGraph graph, int randomSeed = 12345)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            _units.Clear();
            _unitById.Clear();
            _spatialGrid.Clear();
            _pendingSpawns.Clear();
            _players.Clear();
            _projectiles.Clear();
            _meleeStrikes.Clear();
            _corpses.Clear();
            _networkSpellCasts.Clear();
            _presenterSpellCasts.Clear();
            _networkHeroCasts.Clear();
            _presenterHeroCasts.Clear();
            _networkProjectileSpawns.Clear();
            _spellCastSerial = 0;
            _lastAppliedSpellSerial = 0;
            _lastAppliedProjectileId = 0;
            _players.AddRange(players);
            _graph = graph;
            _routes = LaneRouteRegistry.Build(graph);
            _walkable = null;
            _layout = null;
            _committedRoutes.Clear();
            _nextUnitId = 1;
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

            _projectiles.RemoveByOwner(ownerSlot);
            _meleeStrikes.RemoveByOwner(ownerSlot, GetUnitById);
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

                var player = wave.OwnerSlot >= 0 && wave.OwnerSlot < _players.Count
                    ? _players[wave.OwnerSlot]
                    : null;
                var stats = UnitStatsResolver.Resolve(
                    catalog,
                    UnitVisualCatalog,
                    wave.OwnerRaceId,
                    slot.Role,
                    player);
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
            int heroSlot = 0,
            int level = 1)
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
                heroSlot: heroSlot,
                level: level);
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
            MatchSpellSnapshot[] spellCasts = null,
            ICombatUnitCatalog catalog = null,
            MatchProjectileSnapshot[] projectiles = null)
        {
            _spatialGrid.Clear();
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

            ApplyAuthoritativeSpellCasts(spellCasts);
            ApplyAuthoritativeProjectiles(projectiles);
        }

        /// <summary>
        /// Client-side: apply one-shot projectile spawn events (deduped by ProjectileId), then fly locally.
        /// Empty arrays are no-ops — unlike units, projectiles are not a continuous replicated list.
        /// </summary>
        public void ApplyAuthoritativeProjectiles(MatchProjectileSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
            {
                return;
            }

            for (var i = 0; i < snapshots.Length; i++)
            {
                var snap = snapshots[i];
                if (snap.ProjectileId <= _lastAppliedProjectileId || snap.FlightDuration <= 0f)
                {
                    continue;
                }

                _lastAppliedProjectileId = snap.ProjectileId;
                var role = Enum.IsDefined(typeof(UnitRole), (int)snap.AttackerRole)
                    ? (UnitRole)snap.AttackerRole
                    : UnitRole.Ranged;
                int? targetBuilding = snap.TargetBuildingInstanceId >= 0
                    ? snap.TargetBuildingInstanceId
                    : null;
                int? sourceBuilding = snap.SourceBuildingInstanceId >= 0
                    ? snap.SourceBuildingInstanceId
                    : null;
                var sourceBuildingId = string.IsNullOrEmpty(snap.SourceBuildingId)
                    ? null
                    : snap.SourceBuildingId;

                _projectiles.AddPresentation(new CombatProjectileState(
                    snap.ProjectileId,
                    attackerUnitId: 0,
                    targetUnitId: -1,
                    snap.AttackerOwnerSlot,
                    role,
                    attackerRaceId: string.Empty,
                    rawDamage: 0f,
                    snap.FlightDuration,
                    new Vector3(snap.StartX, snap.StartY, snap.StartZ),
                    new Vector3(snap.TargetX, snap.TargetY, snap.TargetZ),
                    snap.IsParabolic,
                    targetBuilding,
                    sourceBuilding,
                    sourceBuildingId));
            }
        }

        /// <summary>Client visuals: advance projectile flight without host damage resolution.</summary>
        public void AdvanceProjectilePresentation(float deltaTime) =>
            _projectiles.AdvancePresentation(deltaTime);

        void EmitNetworkProjectileSpawn(CombatProjectileState projectile)
        {
            if (projectile == null)
            {
                return;
            }

            _networkProjectileSpawns.Add(new MatchProjectileSnapshot
            {
                ProjectileId = projectile.ProjectileId,
                AttackerOwnerSlot = projectile.AttackerOwnerSlot,
                AttackerRole = (byte)projectile.AttackerRole,
                StartX = projectile.StartPosition.x,
                StartY = projectile.StartPosition.y,
                StartZ = projectile.StartPosition.z,
                TargetX = projectile.TargetPosition.x,
                TargetY = projectile.TargetPosition.y,
                TargetZ = projectile.TargetPosition.z,
                FlightDuration = projectile.FlightDuration,
                Elapsed = 0f,
                IsParabolic = projectile.IsParabolic,
                TargetBuildingInstanceId = projectile.TargetBuildingInstanceId ?? -1,
                SourceBuildingInstanceId = projectile.SourceBuildingInstanceId ?? -1,
                SourceBuildingId = projectile.SourceBuildingId ?? string.Empty,
            });
        }

        /// <summary>Client-side: forward snapshot cast events to the presenter once per serial.</summary>
        void ApplyAuthoritativeSpellCasts(MatchSpellSnapshot[] spellCasts)
        {
            if (spellCasts == null)
            {
                return;
            }

            for (var i = 0; i < spellCasts.Length; i++)
            {
                var snap = spellCasts[i];
                if (snap.Serial <= _lastAppliedSpellSerial)
                {
                    continue;
                }

                _lastAppliedSpellSerial = snap.Serial;
                if (snap.SpellKind == 1)
                {
                    _presenterHeroCasts.Add(new HeroAbilityCastEvent(
                        snap.CasterUnitId,
                        snap.OwnerSlot,
                        (HeroAbilityType)snap.HeroAbility,
                        new Vector3(snap.CenterX, 0.15f, snap.CenterZ),
                        snap.Radius,
                        snap.TargetUnitId,
                        snap.Serial));
                    continue;
                }

                _presenterSpellCasts.Add(new CasterSpellCastEvent(
                    snap.CasterUnitId,
                    snap.OwnerSlot,
                    (CasterSpellType)snap.SpellType,
                    snap.TargetUnitId,
                    new Vector3(snap.CenterX, 0.15f, snap.CenterZ),
                    snap.Radius,
                    snap.Serial));
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
                existing.CurrentMana = Mathf.Max(0f, snap.Mana);
                existing.WorldPosition = position;
                existing.FacingDirection = facing;
                existing.BehaviorState = (UnitBehaviorState)snap.BehaviorState;
                existing.AttackSwingSerial = snap.AttackSwingSerial;
                existing.IsParkedAtBase = snap.IsParkedAtBase;
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

            var stats = ResolveSnapshotStats(role, snap.OwnerSlot, snap.Health, snap.Level, snap.HeroSlot, catalog);
            var unit = new MatchUnitState(
                snap.UnitId,
                snap.OwnerSlot,
                laneId,
                role,
                stats,
                Mathf.Max(0.01f, snap.Health),
                position,
                marchSpawnDistance: 0f,
                isHero: role == UnitRole.Hero,
                heroSlot: snap.HeroSlot,
                level: Math.Max(1, snap.Level));
            unit.CurrentHp = Mathf.Max(0f, snap.Health);
            unit.CurrentMana = Mathf.Max(0f, snap.Mana);
            unit.FacingDirection = facing;
            unit.BehaviorState = (UnitBehaviorState)snap.BehaviorState;
            unit.AttackSwingSerial = snap.AttackSwingSerial;
            unit.IsParkedAtBase = snap.IsParkedAtBase;
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
            int level,
            int heroSlot,
            ICombatUnitCatalog catalog)
        {
            if (role == UnitRole.Hero || role == UnitRole.Titan)
            {
                return ResolveChampionSnapshotStats(role, ownerSlot, level, heroSlot, catalog);
            }

            if (catalog != null && ownerSlot >= 0 && ownerSlot < _players.Count)
            {
                return UnitStatsResolver.Resolve(
                    catalog,
                    UnitVisualCatalog,
                    _players[ownerSlot].RaceId,
                    role,
                    _players[ownerSlot]);
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

        UnitCombatStats ResolveChampionSnapshotStats(
            UnitRole role,
            int ownerSlot,
            int level,
            int heroSlot,
            ICombatUnitCatalog catalog)
        {
            var player = ownerSlot >= 0 && ownerSlot < _players.Count ? _players[ownerSlot] : null;
            var raceId = player != null ? player.RaceId : GameIds.Races.Human;
            var slot = role == UnitRole.Hero && heroSlot >= 1 ? heroSlot : 0;
            var stats = UnitStatsResolver.ResolveBase(
                catalog,
                UnitVisualCatalog,
                raceId,
                role,
                slot);
            stats = HeroLevelRules.ApplyLevelGrowth(stats, Math.Max(1, level));
            return RaceUpgradeStatsRules.Apply(stats, player);
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
            string buildingId,
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

            var projectile = _projectiles.SpawnFromBuilding(
                targetUnitId,
                ownerSlot,
                GetPlayerRaceId(ownerSlot),
                rawDamage,
                duration,
                start,
                end,
                buildingInstanceId,
                buildingId);
            EmitNetworkProjectileSpawn(projectile);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            TickPendingSpawns(deltaTime);
            TickCorpses(deltaTime);

            if (_units.Count == 0 && _projectiles.Active.Count == 0 && _meleeStrikes.Active.Count == 0)
            {
                return;
            }

            _spatialGrid.Rebuild(_units);

            _tickBuffer.Clear();
            _tickBuffer.AddRange(_units);
            _tickBuffer.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
            foreach (var unit in _tickBuffer)
            {
                if (!unit.IsAlive)
                {
                    continue;
                }

                if (unit.Stats.HasMana)
                {
                    unit.CurrentMana = Mathf.Min(
                        unit.Stats.MaxMana,
                        unit.CurrentMana + CasterSpellRules.ManaRegenPerSecond * deltaTime);
                }

                TickUnit(unit, deltaTime);
            }

            TickProjectiles(deltaTime);
            TickMeleeStrikes(deltaTime);
        }

        void TickProjectiles(float deltaTime)
        {
            _projectiles.Tick(deltaTime, this);
        }

        void TickCorpses(float deltaTime)
        {
            if (_corpses.Count == 0)
            {
                return;
            }

            for (var i = _corpses.Count - 1; i >= 0; i--)
            {
                var corpse = _corpses[i];
                corpse.AgeSeconds += deltaTime;
                if (corpse.AgeSeconds > CasterSpellRules.ResurrectCorpseMaxAgeSeconds)
                {
                    _corpses.RemoveAt(i);
                }
            }
        }

        void TickMeleeStrikes(float deltaTime)
        {
            _meleeStrikes.Tick(deltaTime, this);
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
            if (unit.ArmorBuffRemaining > 0f)
            {
                unit.ArmorBuffRemaining = Mathf.Max(0f, unit.ArmorBuffRemaining - deltaTime);
            }

            if (unit.IsParkedAtBase)
            {
                return;
            }

            if (unit.FrozenRemainingSeconds > 0f)
            {
                unit.FrozenRemainingSeconds = Mathf.Max(0f, unit.FrozenRemainingSeconds - deltaTime);
                unit.BehaviorState = UnitBehaviorState.Frozen;
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

            if (unit.Role == UnitRole.Caster && TryCastSpell(unit, deltaTime))
            {
                return;
            }

            if (unit.IsHero && TryCastHeroAbility(unit, deltaTime))
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
                unit.AttackCooldownRemaining = GetUnitAttackInterval(unit);
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
            _spatialGrid.Query(myPosition, aggroRadius, _nearbyBuffer);

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
                unit.AttackCooldownRemaining = GetUnitAttackInterval(unit);
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
                _meleeStrikes.Spawn(new CombatMeleeStrikeState(
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
            var projectile = _projectiles.SpawnBuildingAttack(
                attacker.UnitId,
                building.InstanceId,
                attacker.OwnerSlot,
                attacker.Role,
                GetPlayerRaceId(attacker.OwnerSlot),
                rawDamage,
                duration,
                start,
                end);
            EmitNetworkProjectileSpawn(projectile);
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
            _spatialGrid.Query(unit.WorldPosition, queryRadius, _nearbyBuffer);
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
                    || other.LaneId != unit.LaneId
                    || !IsSameFlightCategory(unit, other))
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
            _spatialGrid.Query(unit.WorldPosition, queryRadius, _nearbyBuffer);
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

                if (!IsSameFlightCategory(unit, other))
                {
                    continue;
                }

                allies.Add(other);
            }

            return allies;
        }

        /// <summary>
        /// Flying units only jostle other flying units; ground units only jostle ground units.
        /// </summary>
        static bool IsSameFlightCategory(MatchUnitState a, MatchUnitState b)
        {
            return (a.Role == UnitRole.Flying) == (b.Role == UnitRole.Flying);
        }

        Vector3 ApplyRouteBypassIfBlocked(MatchUnitState unit, LaneRoute route, Vector3 destination)
        {
            var myDistance = route.ProjectDistanceForward(unit.WorldPosition, unit.MarchProgressDistance);
            var forward = route.EvaluateDirectionAtDistance(myDistance);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var aheadGap = CombatFormationRules.MinLaneFollowGap * 0.5f;

            _nearbyBuffer.Clear();
            _spatialGrid.Query(unit.WorldPosition, CombatFormationRules.MinUnitSeparation * 3f, _nearbyBuffer);

            foreach (var other in _nearbyBuffer)
            {
                if (other.UnitId == unit.UnitId
                    || !other.IsAlive
                    || other.OwnerSlot != unit.OwnerSlot
                    || other.LaneId != unit.LaneId
                    || !IsSameFlightCategory(unit, other))
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
            _spatialGrid.Query(enemy.WorldPosition, 16f, _nestedBuffer);

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
                _meleeStrikes.Spawn(new CombatMeleeStrikeState(
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
            var projectile = _projectiles.Spawn(
                attacker.UnitId,
                target.UnitId,
                attacker.OwnerSlot,
                attacker.Role,
                GetPlayerRaceId(attacker.OwnerSlot),
                rawDamage,
                duration,
                start,
                end,
                isParabolic);
            EmitNetworkProjectileSpawn(projectile);
        }

        public void ResolveProjectileImpact(CombatProjectileState projectile)
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

        public void ResolveMeleeImpact(CombatMeleeStrikeState strike)
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

            var damage = CombatRules.ApplyArmor(rawDamage, GetEffectiveArmor(target));
            damage *= GetArmyDamageMultiplier(attacker);
            if (attacker != null && attacker.UltimateBuffRemaining > 0f)
            {
                damage *= 1f + HeroAbilityRules.UltimateSelfDamageBonusPercent;
            }

            target.CurrentHp -= damage;

            if (target.IsAlive)
            {
                return;
            }

            if (attacker != null && attacker.CurrentTargetId == target.UnitId)
            {
                ClearTarget(attacker);
            }

            var bounty = CombatRules.ComputeKillBounty(target.Stats.GoldBounty, target.IsChampion);
            GrantGold(killerOwnerSlot, bounty);
            _corpses.Add(new CombatCorpseState(target));
            RemoveUnit(target);
            UnitKilled?.Invoke(new UnitKillEvent(
                killerOwnerSlot,
                target.OwnerSlot,
                target.UnitId,
                bounty,
                target.Role,
                attacker?.UnitId ?? 0));
        }

        int GetUnitOwnerSlot(int unitId)
        {
            var unit = GetUnitById(unitId);
            return unit?.OwnerSlot ?? 0;
        }

        /// <summary>
        /// King aura (passive, hero slot 1, level ≥ <see cref="HeroLevelRules.AuraUnlockLevel"/>): army of the
        /// hero's owner deals +<see cref="HeroAbilityRules.AuraDamageBonusPercent"/> damage while
        /// that hero is alive.
        /// </summary>
        float GetArmyDamageMultiplier(MatchUnitState attacker)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return 1f;
            }

            return HasLivingHero(attacker.OwnerSlot, HeroAbilityRules.KingSlot, HeroLevelRules.AuraUnlockLevel)
                ? 1f + HeroAbilityRules.AuraDamageBonusPercent
                : 1f;
        }

        float GetArmyAttackSpeedMultiplier(MatchUnitState attacker)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return 1f;
            }

            return HasLivingHero(attacker.OwnerSlot, HeroAbilityRules.PaladinSlot, HeroLevelRules.AuraUnlockLevel)
                ? 1f + HeroAbilityRules.AuraAttackSpeedBonusPercent
                : 1f;
        }

        float GetEffectiveArmor(MatchUnitState target)
        {
            if (target == null)
            {
                return 0f;
            }

            var armor = target.Stats.Armor;
            if (HasLivingHero(target.OwnerSlot, HeroAbilityRules.PriestSlot, HeroLevelRules.AuraUnlockLevel))
            {
                armor *= 1f + HeroAbilityRules.AuraArmorBonusPercent;
            }

            if (target.ArmorBuffRemaining > 0f)
            {
                armor += HeroAbilityRules.ShieldArmorBonus;
            }

            return armor;
        }

        float GetUnitAttackInterval(MatchUnitState unit) =>
            CombatRules.GetAttackIntervalSeconds(unit.Stats.AttackSpeed * GetArmyAttackSpeedMultiplier(unit));

        bool HasLivingHero(int ownerSlot, int heroSlot, int minLevel)
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var candidate = _units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || !candidate.IsHero
                    || candidate.OwnerSlot != ownerSlot
                    || candidate.HeroSlot != heroSlot)
                {
                    continue;
                }

                if (candidate.Level >= minLevel)
                {
                    return true;
                }
            }

            return false;
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

        int GetMagicLevel(int ownerSlot)
        {
            foreach (var player in _players)
            {
                if (player.SlotIndex == ownerSlot)
                {
                    return player.MagicLevel;
                }
            }

            return 0;
        }

        /// <summary>
        /// Caster spell AI: ticks cooldowns, then casts by priority Heal → Frost → Resurrect
        /// when unlocked and a valid target is in cast range. Falls through to baseline attack otherwise.
        /// </summary>
        bool TryCastSpell(MatchUnitState unit, float deltaTime)
        {
            unit.HealCooldownRemaining = Mathf.Max(0f, unit.HealCooldownRemaining - deltaTime);
            unit.FrostCooldownRemaining = Mathf.Max(0f, unit.FrostCooldownRemaining - deltaTime);
            unit.ResurrectCooldownRemaining = Mathf.Max(0f, unit.ResurrectCooldownRemaining - deltaTime);

            var magicLevel = GetMagicLevel(unit.OwnerSlot);
            if (magicLevel >= CasterSpellRules.HealRequiredMagicLevel
                && unit.HealCooldownRemaining <= 0f
                && TryCastHeal(unit))
            {
                return true;
            }

            if (magicLevel >= CasterSpellRules.FrostRequiredMagicLevel
                && unit.FrostCooldownRemaining <= 0f
                && TryCastFrost(unit))
            {
                return true;
            }

            if (magicLevel >= CasterSpellRules.ResurrectRequiredMagicLevel
                && unit.ResurrectCooldownRemaining <= 0f
                && TryCastResurrect(unit))
            {
                return true;
            }

            return false;
        }

        void EmitSpellCast(CasterSpellCastEvent cast)
        {
            _networkSpellCasts.Add(cast);
            _presenterSpellCasts.Add(cast);
            SpellCast?.Invoke(cast);
        }

        bool TryCastHeal(MatchUnitState caster)
        {
            if (caster.CurrentMana < CasterSpellRules.HealManaCost)
            {
                return false;
            }

            var target = CasterSpellRules.PickHealTarget(caster, _units, CasterSpellRules.CastRange);
            if (target == null)
            {
                return false;
            }

            target.CurrentHp = CasterSpellRules.ApplyHeal(target.CurrentHp, target.Stats.MaxHp);
            caster.CurrentMana -= CasterSpellRules.HealManaCost;
            caster.HealCooldownRemaining = CasterSpellRules.HealCooldownSeconds;
            EmitSpellCast(new CasterSpellCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                CasterSpellType.Heal,
                target.UnitId,
                target.WorldPosition,
                0f,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastFrost(MatchUnitState caster)
        {
            if (caster.CurrentMana < CasterSpellRules.FrostManaCost)
            {
                return false;
            }

            var center = CasterSpellRules.PickFrostCenter(
                caster,
                _units,
                CasterSpellRules.CastRange,
                CasterSpellRules.FrostRadius);
            if (center == null)
            {
                return false;
            }

            var victims = CasterSpellRules.GatherFrostVictims(
                caster,
                center,
                _units,
                CasterSpellRules.FrostRadius);
            for (var i = 0; i < victims.Count; i++)
            {
                ApplyDamage(caster, victims[i], CasterSpellRules.FrostDamage, caster.OwnerSlot);
                victims[i].FrozenRemainingSeconds = Mathf.Max(
                    victims[i].FrozenRemainingSeconds,
                    CasterSpellRules.FrostFreezeSeconds);
            }

            caster.CurrentMana -= CasterSpellRules.FrostManaCost;
            caster.FrostCooldownRemaining = CasterSpellRules.FrostCooldownSeconds;
            EmitSpellCast(new CasterSpellCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                CasterSpellType.Frost,
                center.UnitId,
                center.WorldPosition,
                CasterSpellRules.FrostRadius,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastResurrect(MatchUnitState caster)
        {
            if (caster.CurrentMana < CasterSpellRules.ResurrectManaCost)
            {
                return false;
            }

            var corpse = CasterSpellRules.PickResurrectCorpse(
                caster,
                _corpses,
                CasterSpellRules.CastRange,
                CasterSpellRules.ResurrectCorpseMaxAgeSeconds);
            if (corpse == null)
            {
                return false;
            }

            var revived = ResurrectUnit(corpse);
            if (revived == null)
            {
                return false;
            }

            caster.CurrentMana -= CasterSpellRules.ResurrectManaCost;
            caster.ResurrectCooldownRemaining = CasterSpellRules.ResurrectCooldownSeconds;
            EmitSpellCast(new CasterSpellCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                CasterSpellType.Resurrect,
                revived.UnitId,
                revived.WorldPosition,
                0f,
                ++_spellCastSerial));
            return true;
        }

        MatchUnitState ResurrectUnit(CombatCorpseState corpse)
        {
            if (!_routes.TryGetRoute(corpse.OwnerSlot, corpse.LaneId, out var route))
            {
                return null;
            }

            var progressDistance = route.ProjectDistance(corpse.WorldPosition);
            var revived = new MatchUnitState(
                _nextUnitId++,
                corpse.OwnerSlot,
                corpse.LaneId,
                corpse.Role,
                corpse.Stats,
                corpse.Stats.MaxHp,
                corpse.WorldPosition,
                route.FindMarchWaypointIndex(corpse.WorldPosition),
                marchSpawnDistance: progressDistance);
            revived.MarchProgressDistance = progressDistance;
            revived.MarchFocusOpponentSlot = corpse.MarchFocusOpponentSlot;
            ApplySpawnFacing(revived, route, progressDistance);
            ApplyMarchFocusFromLane(revived, corpse.OwnerSlot, corpse.LaneId);
            _units.Add(revived);
            _unitById[revived.UnitId] = revived;
            _corpses.Remove(corpse);
            return revived;
        }

        /// <summary>
        /// Hero ability AI: ticks cooldowns, then casts the slot kit by priority
        /// (support → ultimate → opener) when unlocked and a valid target is in range.
        /// Falls through to baseline attack otherwise.
        /// </summary>
        bool TryCastHeroAbility(MatchUnitState unit, float deltaTime)
        {
            unit.HealCooldownRemaining = Mathf.Max(0f, unit.HealCooldownRemaining - deltaTime);
            unit.StrikeCooldownRemaining = Mathf.Max(0f, unit.StrikeCooldownRemaining - deltaTime);
            unit.UltimateCooldownRemaining = Mathf.Max(0f, unit.UltimateCooldownRemaining - deltaTime);
            unit.UltimateBuffRemaining = Mathf.Max(0f, unit.UltimateBuffRemaining - deltaTime);

            return unit.HeroSlot switch
            {
                HeroAbilityRules.PaladinSlot => TryCastPaladinAbilities(unit, unit.Level),
                HeroAbilityRules.PriestSlot => TryCastPriestAbilities(unit, unit.Level),
                _ => TryCastKingAbilities(unit, unit.Level),
            };
        }

        bool TryCastKingAbilities(MatchUnitState unit, int level)
        {
            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Heal, level)
                && unit.HealCooldownRemaining <= 0f
                && TryCastHeroHeal(unit))
            {
                return true;
            }

            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Ultimate, level)
                && unit.UltimateCooldownRemaining <= 0f
                && TryCastHeroUltimate(unit))
            {
                return true;
            }

            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Strike, level)
                && unit.StrikeCooldownRemaining <= 0f
                && TryCastHeroStrike(unit))
            {
                return true;
            }

            return false;
        }

        bool TryCastPaladinAbilities(MatchUnitState unit, int level)
        {
            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Shield, level)
                && unit.HealCooldownRemaining <= 0f
                && TryCastPaladinShield(unit))
            {
                return true;
            }

            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Consecration, level)
                && unit.UltimateCooldownRemaining <= 0f
                && TryCastPaladinConsecration(unit))
            {
                return true;
            }

            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Smite, level)
                && unit.StrikeCooldownRemaining <= 0f
                && TryCastPaladinSmite(unit))
            {
                return true;
            }

            return false;
        }

        bool TryCastPriestAbilities(MatchUnitState unit, int level)
        {
            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.GreaterHeal, level)
                && unit.HealCooldownRemaining <= 0f
                && TryCastPriestGreaterHeal(unit))
            {
                return true;
            }

            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Revive, level)
                && unit.UltimateCooldownRemaining <= 0f
                && TryCastPriestRevive(unit))
            {
                return true;
            }

            if (HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.HolyNova, level)
                && unit.StrikeCooldownRemaining <= 0f
                && TryCastPriestNova(unit))
            {
                return true;
            }

            return false;
        }

        bool TryCastHeroHeal(MatchUnitState caster)
        {
            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, _units, HeroAbilityRules.HealRadius);
            var target = FindMostInjuredAlly(allies);
            if (target == null)
            {
                return false;
            }

            for (var i = 0; i < allies.Count; i++)
            {
                allies[i].CurrentHp = HeroAbilityRules.ApplyHeal(allies[i].CurrentHp, allies[i].Stats.MaxHp);
            }

            caster.HealCooldownRemaining = HeroAbilityRules.HealCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.Heal,
                caster.WorldPosition,
                HeroAbilityRules.HealRadius,
                target.UnitId,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastHeroStrike(MatchUnitState caster)
        {
            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, _units, HeroAbilityRules.StrikeRadius);
            if (enemies.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < enemies.Count; i++)
            {
                ApplyDamage(caster, enemies[i], HeroAbilityRules.StrikeDamage, caster.OwnerSlot);
            }

            caster.StrikeCooldownRemaining = HeroAbilityRules.StrikeCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.Strike,
                caster.WorldPosition,
                HeroAbilityRules.StrikeRadius,
                targetUnitId: 0,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastHeroUltimate(MatchUnitState caster)
        {
            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, _units, HeroAbilityRules.UltimateRadius);
            if (enemies.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < enemies.Count; i++)
            {
                ApplyDamage(caster, enemies[i], HeroAbilityRules.UltimateDamage, caster.OwnerSlot);
            }

            caster.UltimateBuffRemaining = HeroAbilityRules.UltimateSelfBuffSeconds;
            caster.UltimateCooldownRemaining = HeroAbilityRules.UltimateCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.Ultimate,
                caster.WorldPosition,
                HeroAbilityRules.UltimateRadius,
                targetUnitId: 0,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastPaladinSmite(MatchUnitState caster)
        {
            var target = HeroAbilityRules.FindNearestEnemy(caster, _units, HeroAbilityRules.SmiteRadius);
            if (target == null)
            {
                return false;
            }

            ApplyDamage(caster, target, HeroAbilityRules.SmiteDamage, caster.OwnerSlot);
            caster.StrikeCooldownRemaining = HeroAbilityRules.SmiteCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.Smite,
                target.WorldPosition,
                0.8f,
                target.UnitId,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastPaladinShield(MatchUnitState caster)
        {
            if (caster.ArmorBuffRemaining > 0f)
            {
                return false;
            }

            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, _units, HeroAbilityRules.ShieldRadius);
            if (enemies.Count == 0)
            {
                return false;
            }

            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, _units, HeroAbilityRules.ShieldRadius);
            for (var i = 0; i < allies.Count; i++)
            {
                allies[i].ArmorBuffRemaining = Mathf.Max(
                    allies[i].ArmorBuffRemaining,
                    HeroAbilityRules.ShieldDurationSeconds);
            }

            caster.HealCooldownRemaining = HeroAbilityRules.ShieldCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.Shield,
                caster.WorldPosition,
                HeroAbilityRules.ShieldRadius,
                caster.UnitId,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastPaladinConsecration(MatchUnitState caster)
        {
            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, _units, HeroAbilityRules.ConsecrationRadius);
            if (enemies.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < enemies.Count; i++)
            {
                ApplyDamage(caster, enemies[i], HeroAbilityRules.ConsecrationDamage, caster.OwnerSlot);
                if (enemies[i].IsAlive)
                {
                    enemies[i].FrozenRemainingSeconds = Mathf.Max(
                        enemies[i].FrozenRemainingSeconds,
                        HeroAbilityRules.ConsecrationStunSeconds);
                }
            }

            caster.UltimateCooldownRemaining = HeroAbilityRules.ConsecrationCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.Consecration,
                caster.WorldPosition,
                HeroAbilityRules.ConsecrationRadius,
                targetUnitId: 0,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastPriestNova(MatchUnitState caster)
        {
            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, _units, HeroAbilityRules.NovaRadius);
            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, _units, HeroAbilityRules.NovaRadius);
            var injured = FindMostInjuredAlly(allies);
            if (enemies.Count == 0 && injured == null)
            {
                return false;
            }

            for (var i = 0; i < enemies.Count; i++)
            {
                ApplyDamage(caster, enemies[i], HeroAbilityRules.NovaDamage, caster.OwnerSlot);
            }

            for (var i = 0; i < allies.Count; i++)
            {
                allies[i].CurrentHp = HeroAbilityRules.ApplyHeal(
                    allies[i].CurrentHp,
                    allies[i].Stats.MaxHp,
                    HeroAbilityRules.NovaHealAmount);
            }

            caster.StrikeCooldownRemaining = HeroAbilityRules.NovaCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.HolyNova,
                caster.WorldPosition,
                HeroAbilityRules.NovaRadius,
                injured?.UnitId ?? 0,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastPriestGreaterHeal(MatchUnitState caster)
        {
            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, _units, HeroAbilityRules.GreaterHealRadius);
            var target = FindMostInjuredAlly(allies);
            if (target == null)
            {
                return false;
            }

            for (var i = 0; i < allies.Count; i++)
            {
                allies[i].CurrentHp = HeroAbilityRules.ApplyHeal(
                    allies[i].CurrentHp,
                    allies[i].Stats.MaxHp,
                    HeroAbilityRules.GreaterHealAmount);
            }

            caster.HealCooldownRemaining = HeroAbilityRules.GreaterHealCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.GreaterHeal,
                caster.WorldPosition,
                HeroAbilityRules.GreaterHealRadius,
                target.UnitId,
                ++_spellCastSerial));
            return true;
        }

        bool TryCastPriestRevive(MatchUnitState caster)
        {
            var corpse = CasterSpellRules.PickResurrectCorpse(
                caster,
                _corpses,
                HeroAbilityRules.ReviveRadius,
                CasterSpellRules.ResurrectCorpseMaxAgeSeconds);
            if (corpse == null)
            {
                return false;
            }

            var revived = ResurrectUnit(corpse);
            if (revived == null)
            {
                return false;
            }

            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, _units, HeroAbilityRules.ReviveHealRadius);
            for (var i = 0; i < allies.Count; i++)
            {
                allies[i].CurrentHp = HeroAbilityRules.ApplyHeal(
                    allies[i].CurrentHp,
                    allies[i].Stats.MaxHp,
                    HeroAbilityRules.ReviveHealAmount);
            }

            caster.UltimateCooldownRemaining = HeroAbilityRules.ReviveCooldownSeconds;
            EmitHeroAbilityCast(new HeroAbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                HeroAbilityType.Revive,
                revived.WorldPosition,
                HeroAbilityRules.ReviveHealRadius,
                revived.UnitId,
                ++_spellCastSerial));
            return true;
        }

        static MatchUnitState FindMostInjuredAlly(List<MatchUnitState> allies)
        {
            if (allies == null || allies.Count == 0)
            {
                return null;
            }

            MatchUnitState best = null;
            var bestFraction = float.MaxValue;
            for (var i = 0; i < allies.Count; i++)
            {
                var candidate = allies[i];
                if (candidate.CurrentHp >= candidate.Stats.MaxHp - 0.001f)
                {
                    continue;
                }

                var fraction = candidate.CurrentHp / candidate.Stats.MaxHp;
                if (fraction < bestFraction)
                {
                    bestFraction = fraction;
                    best = candidate;
                }
            }

            return best;
        }

        void EmitHeroAbilityCast(HeroAbilityCastEvent cast)
        {
            _networkHeroCasts.Add(cast);
            _presenterHeroCasts.Add(cast);
            HeroSpellCast?.Invoke(cast);
        }

        void GrantGold(int ownerSlot, int amount)
        {            foreach (var player in _players)
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
