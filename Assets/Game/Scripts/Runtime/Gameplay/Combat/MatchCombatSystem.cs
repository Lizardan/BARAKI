using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public sealed class MatchCombatSystem : IProjectileImpactHandler, IMeleeImpactHandler, IPendingProjectileHandler, IUnitAbilityHost
    {
        readonly List<MatchUnitState> _units = new();
        readonly Dictionary<int, MatchUnitState> _unitById = new();
        readonly List<MatchPlayerState> _players = new();
        readonly CombatProjectileSystem _projectiles = new();
        readonly CombatMeleeStrikeSystem _meleeStrikes = new();
        readonly CombatPendingProjectileSystem _pendingProjectiles = new();
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
        readonly List<AbilityCastEvent> _networkAbilityCasts = new();
        readonly List<AbilityCastEvent> _presenterAbilityCasts = new();
        readonly List<HeroHealZoneState> _healZones = new();
        readonly List<MatchProjectileSnapshot> _networkProjectileSpawns = new();
        readonly Dictionary<int, UnitRenderTrack> _renderTracks = new();
        int _spellCastSerial;
        int _lastAppliedSpellSerial;
        int _lastAppliedProjectileId;
        readonly HashSet<int> _loggedMissingVfxAbilityIds = new();

        sealed class PendingBarracksSpawn
        {
            public float RemainingSeconds;
            public int OwnerSlot;
            public string LaneId;
            public UnitRole Role;
            public UnitCombatStats Stats;
            public int BonusSlot;
            public float MarchMoveSpeed;
            public float SpawnDistance;
            public Vector3 FormationOffset;
        }

        public event Action<UnitKillEvent> UnitKilled;
        public event Action<AbilityCastEvent> AbilityCast;

        public UnitVisualCatalog UnitVisualCatalog { get; set; }
        /// <summary>Resolves snapshot ability ids to <see cref="UnitAbilityDef"/> (clients + host).</summary>
        public UnitAbilityCatalog AbilityCatalog { get; set; }

        public IReadOnlyList<MatchUnitState> Units => _units;
        public IReadOnlyList<CombatProjectileState> Projectiles => _projectiles.Active;
        public IReadOnlyList<CombatMeleeStrikeState> MeleeStrikes => _meleeStrikes.Active;
        /// <summary>Transient host-side corpses kept for resurrect (cull age = CasterSpellRules.ResurrectCorpseMaxAgeSeconds).</summary>
        public IReadOnlyList<CombatCorpseState> Corpses => _corpses;

        /// <summary>
        /// Casts not yet captured into a snapshot. Host: drained by <see cref="MatchSnapshotCodec.Capture"/>.
        /// Used to sync ability VFX to clients.
        /// </summary>
        public IReadOnlyList<AbilityCastEvent> NetworkAbilityCasts => _networkAbilityCasts;

        /// <summary>Host-only: consumed by the snapshot capture into the next <see cref="NetworkAbilityCasts"/>.</summary>
        public void ClearNetworkAbilityCasts() => _networkAbilityCasts.Clear();

        /// <summary>
        /// Casts waiting for presenter playback this tick. Peek before <see cref="ConsumePendingAbilityCasts"/>
        /// so animator overrides can apply on the same frame as VFX.
        /// </summary>
        public IReadOnlyList<AbilityCastEvent> PendingPresenterCasts => _presenterAbilityCasts;

        /// <summary>
        /// Casts pending presenter playback (host + clients). Consumers call this once per sync tick;
        /// the same serial never reappears thanks to snapshot v9 dedup on clients.
        /// </summary>
        public IReadOnlyList<AbilityCastEvent> ConsumePendingAbilityCasts()
        {
            var result = new List<AbilityCastEvent>(_presenterAbilityCasts);
            _presenterAbilityCasts.Clear();
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
            _pendingProjectiles.Clear();
            _corpses.Clear();
            _networkAbilityCasts.Clear();
            _presenterAbilityCasts.Clear();
            _networkProjectileSpawns.Clear();
            _spellCastSerial = 0;
            _lastAppliedSpellSerial = 0;
            _lastAppliedProjectileId = 0;
            _loggedMissingVfxAbilityIds.Clear();
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
                var bonusSlot = BonusKitRules.EffectiveBonusSlotForRole(
                    player?.BonusPickSlot ?? 0,
                    slot.Role);
                var stats = UnitStatsResolver.Resolve(
                    catalog,
                    UnitVisualCatalog,
                    wave.OwnerRaceId,
                    slot.Role,
                    player,
                    bonusSlot: bonusSlot);
                var unitMarchSpeed = HumanBonusUnitRules.ApplyMarchDiscipline(
                    player,
                    RaceMarchSpeedRules.GetMarchSpeed(race, definition));
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
                    BonusSlot = bonusSlot,
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
            int level = 1,
            int bonusSlot = 0)
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
                level: level,
                bonusSlot: bonusSlot);
            unit.MarchProgressDistance = progressDistance;
            ApplySpawnFacing(unit, route, distanceAlongLane);
            ApplyMarchFocusFromLane(unit, ownerSlot, laneId);
            AttachAbilities(unit);
            ApplySpawnUniqueModifiers(unit);
            _units.Add(unit);
            _unitById[unit.UnitId] = unit;
            return unit;
        }

        /// <summary>
        /// FACELESS-013: captures the owner's Shadow of the Void pick at spawn time.
        /// Replacement policy — only units spawned after the pick carry the evade flag.
        /// </summary>
        void ApplySpawnUniqueModifiers(MatchUnitState unit)
        {
            if (unit == null || unit.OwnerSlot < 0 || unit.OwnerSlot >= _players.Count)
            {
                return;
            }

            unit.ShadowEvadeActive = FacelessBonusUnitRules.HasShadowOfTheVoid(_players[unit.OwnerSlot]);
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
            MatchProjectileSnapshot[] projectiles = null,
            float matchTimeSeconds = 0f)
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
                    UpsertAuthoritativeUnit(snap, catalog, matchTimeSeconds);
                }
            }

            for (var i = _units.Count - 1; i >= 0; i--)
            {
                if (keep.Contains(_units[i].UnitId))
                {
                    continue;
                }

                _unitById.Remove(_units[i].UnitId);
                _renderTracks.Remove(_units[i].UnitId);
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
        /// Client rendering: returns the authoritative sample pair bracketing
        /// <paramref name="renderTimeSeconds"/> for snapshot interpolation.
        /// </summary>
        public bool TryGetUnitRenderPair(
            int unitId,
            float renderTimeSeconds,
            out UnitRenderSample prev,
            out UnitRenderSample next,
            out float alpha)
        {
            if (_renderTracks.TryGetValue(unitId, out var track))
            {
                return track.TryGetPair(renderTimeSeconds, out prev, out next, out alpha);
            }

            prev = default;
            next = default;
            alpha = 0f;
            return false;
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
                    sourceBuildingId,
                    snap.AppliesSplashAoe));
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
                IsParabolic = projectile.IsParabolic,
                TargetBuildingInstanceId = projectile.TargetBuildingInstanceId ?? -1,
                SourceBuildingInstanceId = projectile.SourceBuildingInstanceId ?? -1,
                SourceBuildingId = projectile.SourceBuildingId ?? string.Empty,
                AppliesSplashAoe = projectile.AppliesSplashAoe,
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
                var def = AbilityCatalog != null ? AbilityCatalog.Find(snap.AbilityId) : null;
                if (def == null)
                {
                    MainExtraAbilityFxDefs.TryGet(snap.AbilityId, out def);
                }

                if (def == null)
                {
                    continue;
                }

                // FX-only defs (Studio VfxPrefab, no behaviour) still play on clients.
                if (def.Behaviour == null
                    && def.Fx.VfxPrefab == null
                    && !MainExtraAbilityFxDefs.TryGet(snap.AbilityId, out _))
                {
                    continue;
                }

                if (def.Fx.VfxPrefab == null
                    && !_loggedMissingVfxAbilityIds.Contains(def.AbilityId))
                {
                    _loggedMissingVfxAbilityIds.Add(def.AbilityId);
                    PlaytestLog.Warn(
                        "AbilityFx",
                        "EmptyVfxPrefab",
                        ("abilityId", def.AbilityId),
                        ("name", def.DisplayName));
                }

                _presenterAbilityCasts.Add(new AbilityCastEvent(
                    snap.CasterUnitId,
                    snap.OwnerSlot,
                    def,
                    snap.TargetUnitId,
                    new Vector3(snap.CenterX, 0.15f, snap.CenterZ),
                    snap.Radius,
                    snap.Serial));
            }
        }

        void UpsertAuthoritativeUnit(MatchUnitSnapshot snap, ICombatUnitCatalog catalog, float matchTimeSeconds)
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

            var behaviorState = (UnitBehaviorState)snap.BehaviorState;

            if (_unitById.TryGetValue(snap.UnitId, out var existing)
                && existing.LaneId == laneId
                && existing.Role == role)
            {
                existing.CurrentHp = Mathf.Max(0f, snap.Health);
                existing.CurrentMana = Mathf.Max(0f, snap.Mana);
                existing.WorldPosition = position;
                existing.FacingDirection = facing;
                existing.BehaviorState = behaviorState;
                existing.AttackSwingSerial = snap.AttackSwingSerial;
                existing.IsParkedAtBase = snap.IsParkedAtBase;
                existing.BonusSlot = snap.BonusSlot;
                existing.AuraRadius = snap.AuraRadius;
                existing.AuraColorPacked = snap.AuraColorPacked;
                existing.AuraAbilityId = snap.AuraAbilityId;
                existing.CurrentTargetId = snap.TargetUnitId > 0 ? snap.TargetUnitId : null;
                existing.CurrentTargetBuildingInstanceId =
                    snap.TargetBuildingInstanceId >= 0 ? snap.TargetBuildingInstanceId : null;
                existing.AttackCommitRemainingSeconds = snap.IsAttackCommitted ? 0.01f : 0f;
                existing.MarchProgressDistance = _routes != null
                    && _routes.TryGetRoute(snap.OwnerSlot, laneId, out var route)
                    ? route.ProjectDistance(position)
                    : existing.MarchProgressDistance;
                AddRenderSample(existing.UnitId, matchTimeSeconds, position, facing, behaviorState, snap.AttackSwingSerial);
                return;
            }

            if (existing != null)
            {
                _unitById.Remove(existing.UnitId);
                _units.Remove(existing);
                _renderTracks.Remove(existing.UnitId);
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
            unit.BonusSlot = snap.BonusSlot;
            unit.AuraRadius = snap.AuraRadius;
            unit.AuraColorPacked = snap.AuraColorPacked;
            unit.AuraAbilityId = snap.AuraAbilityId;
            unit.CurrentTargetId = snap.TargetUnitId > 0 ? snap.TargetUnitId : null;
            unit.CurrentTargetBuildingInstanceId =
                snap.TargetBuildingInstanceId >= 0 ? snap.TargetBuildingInstanceId : null;
            unit.AttackCommitRemainingSeconds = snap.IsAttackCommitted ? 0.01f : 0f;
            if (_routes != null && _routes.TryGetRoute(snap.OwnerSlot, laneId, out var spawnRoute))
            {
                unit.MarchProgressDistance = spawnRoute.ProjectDistance(position);
                unit.MarchWaypointIndex = spawnRoute.FindMarchWaypointIndex(position);
            }

            ApplySpawnUniqueModifiers(unit);
            _units.Add(unit);
            _unitById[unit.UnitId] = unit;
            AttachAbilities(unit);
            _nextUnitId = Mathf.Max(_nextUnitId, snap.UnitId + 1);
            AddRenderSample(unit.UnitId, matchTimeSeconds, position, facing, behaviorState, snap.AttackSwingSerial);
        }

        void AddRenderSample(
            int unitId,
            float matchTimeSeconds,
            Vector3 position,
            Vector3 facing,
            UnitBehaviorState behaviorState,
            int attackSwingSerial)
        {
            if (!_renderTracks.TryGetValue(unitId, out var track))
            {
                track = new UnitRenderTrack();
                _renderTracks[unitId] = track;
            }

            track.Add(matchTimeSeconds, position, facing, behaviorState, attackSwingSerial);
        }

        /// <summary>
        /// Record render samples from local sim (host) for smooth interpolated presentation.
        /// Call after Tick on host/offline to populate UnitRenderTrack with sim positions.
        /// </summary>
        public void RecordLocalRenderSamples(float matchTimeSeconds)
        {
            foreach (var unit in _units)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                AddRenderSample(
                    unit.UnitId,
                    matchTimeSeconds,
                    unit.WorldPosition,
                    unit.FacingDirection,
                    unit.BehaviorState,
                    unit.AttackSwingSerial);
            }
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

            if (_units.Count == 0
                && _projectiles.Active.Count == 0
                && _meleeStrikes.Active.Count == 0
                && _pendingProjectiles.Active.Count == 0)
            {
                _healZones.Clear();
                return;
            }

            _spatialGrid.Rebuild(_units);

            // Resolve deliveries from previous swings before AI can spawn new ones,
            // so a mid-swing delay is never fully consumed in the same Tick that created it.
            TickMeleeStrikes(deltaTime);
            TickPendingProjectiles(deltaTime);
            TickProjectiles(deltaTime);

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
                TickTowerTrackStatus(unit, deltaTime);
            }

            TickHealZones(deltaTime);
            TickAuraRegen(deltaTime);
            TickFieldMedicsRegen(deltaTime);
            ClampUnitsToEffectiveMaxHp();
        }

        /// <summary>Per-unit tower-track timers: burn damage and Bloodrage decay (PRE-007).</summary>
        void TickTowerTrackStatus(MatchUnitState unit, float deltaTime)
        {
            if (unit.BloodrageRemainingSeconds > 0f)
            {
                unit.BloodrageRemainingSeconds = Mathf.Max(0f, unit.BloodrageRemainingSeconds - deltaTime);
            }

            if (unit.SlowRemainingSeconds > 0f)
            {
                unit.SlowRemainingSeconds = Mathf.Max(0f, unit.SlowRemainingSeconds - deltaTime);
                if (unit.SlowRemainingSeconds <= 0f)
                {
                    unit.SlowPercent = 0f;
                }
            }

            if (unit.ArmorDebuffRemainingSeconds > 0f)
            {
                unit.ArmorDebuffRemainingSeconds = Mathf.Max(0f, unit.ArmorDebuffRemainingSeconds - deltaTime);
                if (unit.ArmorDebuffRemainingSeconds <= 0f)
                {
                    unit.ArmorDebuffAmount = 0f;
                }
            }

            if (unit.FeastRemainingSeconds > 0f)
            {
                unit.FeastRemainingSeconds = Mathf.Max(0f, unit.FeastRemainingSeconds - deltaTime);
                if (unit.FeastRemainingSeconds <= 0f)
                {
                    unit.FeastStacks = 0;
                    unit.FeastAttackSpeedPerStack = 0f;
                }
            }

            if (unit.BurnSecondsRemaining <= 0f || unit.BurnDamagePerSecond <= 0f || !unit.IsAlive)
            {
                return;
            }

            unit.BurnSecondsRemaining -= deltaTime;
            var burnDamage = unit.BurnDamagePerSecond * deltaTime;
            if (unit.BurnSecondsRemaining <= 0f)
            {
                unit.BurnSecondsRemaining = 0f;
                unit.BurnDamagePerSecond = 0f;
            }

            ApplyDamage(null, unit, burnDamage, unit.BurnSourceOwnerSlot);
        }

        /// <summary>Field Medics (tower track 8): flat HP regeneration for regular units.</summary>
        void TickFieldMedicsRegen(float deltaTime)
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit.IsChampion)
                {
                    continue;
                }

                var level = GetTowerTrackLevel(unit.OwnerSlot, 7);
                if (level <= 0)
                {
                    continue;
                }

                var maxHp = GetEffectiveMaxHp(unit);
                if (unit.CurrentHp >= maxHp)
                {
                    continue;
                }

                unit.CurrentHp = Mathf.Min(
                    maxHp,
                    unit.CurrentHp + TowerTrackRules.RegenHpPerSecondPerLevel * level * deltaTime);
            }
        }

        void TickProjectiles(float deltaTime)
        {
            _projectiles.Tick(deltaTime, this);
        }

        void TickPendingProjectiles(float deltaTime)
        {
            _pendingProjectiles.Tick(deltaTime, this);
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
                pending.SpawnDistance,
                bonusSlot: pending.BonusSlot);
            unit.MarchProgressDistance = progressDistance;
            ApplySpawnFacing(unit, route, pending.SpawnDistance);
            ApplyMarchFocusFromLane(unit, pending.OwnerSlot, pending.LaneId);
            AttachAbilities(unit);
            ApplySpawnUniqueModifiers(unit);
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
                if (unit.ArmorBuffRemaining <= 0f)
                {
                    unit.ArmorBuffBonus = 0f;
                }
            }

            if (unit.AbsorbSecondsRemaining > 0f)
            {
                unit.AbsorbSecondsRemaining = Mathf.Max(0f, unit.AbsorbSecondsRemaining - deltaTime);
                if (unit.AbsorbSecondsRemaining <= 0f)
                {
                    unit.AbsorbRemaining = 0f;
                }
            }

            if (unit.EvadeRemainingSeconds > 0f)
            {
                unit.EvadeRemainingSeconds = Mathf.Max(0f, unit.EvadeRemainingSeconds - deltaTime);
            }

            if (unit.IsParkedAtBase)
            {
                return;
            }

            TickAbilityCooldowns(unit, deltaTime);

            if (unit.FrozenRemainingSeconds > 0f)
            {
                unit.FrozenRemainingSeconds = Mathf.Max(0f, unit.FrozenRemainingSeconds - deltaTime);
                unit.CastLockRemainingSeconds = 0f;
                unit.BehaviorState = UnitBehaviorState.Frozen;
                return;
            }

            if (unit.CastLockRemainingSeconds > 0f)
            {
                unit.CastLockRemainingSeconds = Mathf.Max(0f, unit.CastLockRemainingSeconds - deltaTime);
                unit.BehaviorState = unit.CastLockUsesAttackAnim
                    ? UnitBehaviorState.Attack
                    : UnitBehaviorState.Cast;
                if (unit.CastLockRemainingSeconds > 0f)
                {
                    return;
                }
            }

            if (TickSuperAttackCommit(unit, deltaTime))
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

            if (TryCastKitAbilities(unit, deltaTime))
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
                var buildingIdentity = IdentityOf(unit);
                if (CombatRules.IsWithinBuildingAttackBand(
                        buildingDistance,
                        unit.Stats.AttackRange,
                        buildingIdentity))
                {
                    unit.BehaviorState = UnitBehaviorState.Attack;
                    TickBuildingAttack(unit, buildingTarget, deltaTime);
                    return;
                }

                if (buildingDistance < buildingIdentity.GetMinAttackRange())
                {
                    unit.BehaviorState = UnitBehaviorState.Chase;
                    TickRetreatFromPoint(unit, route, buildingTarget.WorldPosition, deltaTime);
                    return;
                }

                unit.BehaviorState = UnitBehaviorState.Chase;
                TickChaseBuilding(unit, route, buildingTarget, deltaTime);
                return;
            }

            var target = GetUnitById(unit.CurrentTargetId);
            var unitIdentity = IdentityOf(unit);
            if (target != null && unitIdentity.CanAttackTarget(target.Role))
            {
                var distance = HorizontalDistance(unit.WorldPosition, target.WorldPosition);
                if (CombatRules.IsWithinAttackBand(
                        distance,
                        unit.Stats.AttackRange,
                        unitIdentity,
                        target.Role))
                {
                    unit.BehaviorState = UnitBehaviorState.Attack;
                    TickAttack(unit, target, deltaTime);
                    return;
                }

                if (distance < unitIdentity.GetMinAttackRange())
                {
                    unit.BehaviorState = UnitBehaviorState.Chase;
                    TickRetreatFromPoint(unit, route, target.WorldPosition, deltaTime);
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
            var maxStep = GetEffectiveMarchSpeed(unit) * deltaTime;
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

            if (!IdentityOf(unit).CanAttackTarget(target.Role))
            {
                ClearTarget(unit);
                return;
            }

            var maxStep = GetEffectiveMarchSpeed(unit) * deltaTime;
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

            var unitIdentity = IdentityOf(unit);
            if (!unitIdentity.CanAttackTarget(target.Role))
            {
                ClearTarget(unit);
                return;
            }

            var distance = HorizontalDistance(unit.WorldPosition, target.WorldPosition);
            if (!CombatRules.IsWithinAttackBand(
                    distance,
                    unit.Stats.AttackRange,
                    unitIdentity,
                    target.Role))
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
                || !IdentityOf(unit).CanAttackTarget(target.Role))
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

                var identity = IdentityOf(unit);
                if (!identity.CanAttackTarget(other.Role))
                {
                    continue;
                }

                var distance = HorizontalDistance(myPosition, other.WorldPosition);
                if (distance > aggroRadius)
                {
                    continue;
                }

                // Super artillery ignores targets inside min range (cannot fire there);
                // Faceless Super is melee, so its minimum band is zero (fights point-blank).
                if (distance < identity.GetMinAttackRange())
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
            if (unit == null)
            {
                return;
            }

            if (unit.Role == UnitRole.Super
                && (unit.AttackCommitRemainingSeconds > 0f || HasPendingProjectileFor(unit.UnitId)))
            {
                unit.CurrentTargetId = null;
                unit.CurrentTargetBuildingInstanceId = null;
                unit.BehaviorState = UnitBehaviorState.Attack;
                return;
            }

            unit.CurrentTargetId = null;
            unit.CurrentTargetBuildingInstanceId = null;
            unit.BehaviorState = UnitBehaviorState.Move;
            unit.TargetScanCooldown = 0f;
        }

        void CommitSuperAttackSwing(MatchUnitState attacker, Vector3 aimWorldPosition)
        {
            if (attacker == null || attacker.Role != UnitRole.Super)
            {
                return;
            }

            attacker.AttackCommitAimPosition = aimWorldPosition;
            attacker.AttackCommitRemainingSeconds = GetUnitAttackInterval(attacker);
            attacker.BehaviorState = UnitBehaviorState.Attack;
        }

        /// <summary>
        /// Holds Super in Attack until the committed swing anim finishes (even if target died).
        /// </summary>
        bool TickSuperAttackCommit(MatchUnitState unit, float deltaTime)
        {
            if (unit.Role != UnitRole.Super || unit.AttackCommitRemainingSeconds <= 0f)
            {
                return false;
            }

            unit.AttackCommitRemainingSeconds = Mathf.Max(0f, unit.AttackCommitRemainingSeconds - deltaTime);
            unit.BehaviorState = UnitBehaviorState.Attack;
            UpdateFacingTowards(unit, unit.AttackCommitAimPosition, deltaTime);
            unit.AttackCooldownRemaining = Mathf.Max(0f, unit.AttackCooldownRemaining - deltaTime);

            if (unit.AttackCommitRemainingSeconds > 0f || HasPendingProjectileFor(unit.UnitId))
            {
                return true;
            }

            unit.CurrentTargetId = null;
            unit.CurrentTargetBuildingInstanceId = null;
            unit.BehaviorState = UnitBehaviorState.Move;
            unit.TargetScanCooldown = 0f;
            return false;
        }

        bool HasPendingProjectileFor(int attackerUnitId)
        {
            var active = _pendingProjectiles.Active;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].AttackerUnitId == attackerUnitId)
                {
                    return true;
                }
            }

            return false;
        }

        void TickRetreatFromPoint(
            MatchUnitState unit,
            LaneRoute route,
            Vector3 threatPosition,
            float deltaTime)
        {
            var maxStep = GetEffectiveMarchSpeed(unit) * deltaTime;
            if (maxStep <= 0.0001f)
            {
                return;
            }

            var away = unit.WorldPosition - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = -unit.FacingDirection;
                away.y = 0f;
            }

            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector3.forward;
            }

            away.Normalize();
            var destination = unit.WorldPosition + away * Mathf.Max(maxStep, 1f);
            var allies = CollectAlliesForAvoidance(unit);
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
            ApplyMoveFacing(unit, moveFacing, previousPosition, deltaTime);
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

            var maxStep = GetEffectiveMarchSpeed(unit) * deltaTime;
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
            if (!CombatRules.IsWithinBuildingAttackBand(distance, unit.Stats.AttackRange, IdentityOf(unit)))
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
            var identity = IdentityOf(attacker);
            var impactDelay = CombatAttackRules.ResolveSwingImpactDelay(
                GetUnitAttackInterval(attacker),
                identity);

            if (identity.UsesMeleeStrike || !identity.UsesProjectile)
            {
                _meleeStrikes.Spawn(new CombatMeleeStrikeState(
                    attacker.UnitId,
                    targetUnitId: -1,
                    rawDamage,
                    impactDelay,
                    targetBuildingInstanceId: building.InstanceId));
                return;
            }

            CommitSuperAttackSwing(attacker, building.WorldPosition);
            _pendingProjectiles.Spawn(new CombatPendingProjectileState(
                attacker.UnitId,
                targetUnitId: -1,
                rawDamage,
                impactDelay,
                targetBuildingInstanceId: building.InstanceId,
                aimWorldPosition: building.WorldPosition));
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
                var dProgress = otherDistance - myDistance;
                if (dProgress < -0.35f || dProgress > aheadGapMax)
                {
                    continue;
                }

                if (otherDistance + CombatFormationRules.MinLaneFollowGap < myDistance)
                {
                    continue;
                }

                var toOther = other.WorldPosition - myPosition;
                toOther.y = 0f;
                var toOtherSq = toOther.sqrMagnitude;
                if (toOtherSq > queryRadius * queryRadius)
                {
                    continue;
                }

                // Same-row units (|dProgress| small) and exact overlaps separate regardless of
                // facing; only clearly-ahead allies require the ahead check (avoids pushing the
                // unit backward from trailing column-mates).
                if (dProgress > 0.35f
                    && Vector3.Dot(toOther.normalized, forward) < 0.35f)
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

                var allyReach = CombatRules.GetUnitAttackReach(ally.Stats.AttackRange, enemy.Role);
                if (HorizontalDistance(ally.WorldPosition, enemy.WorldPosition) <= allyReach * 1.25f)
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

            var useHybridMelee = HumanBonusUnitRules.IsHybridMeleeNow(
                attacker,
                attacker.WorldPosition,
                target.WorldPosition);
            var identity = IdentityOf(attacker);

            float rawDamage;
            if (useHybridMelee)
            {
                var owner = attacker.OwnerSlot >= 0 && attacker.OwnerSlot < _players.Count
                    ? _players[attacker.OwnerSlot]
                    : null;
                rawDamage = HumanBonusUnitRules.RollHybridMeleeDamage(owner, _random);
            }
            else
            {
                rawDamage = CombatRules.RollDamage(
                    attacker.Stats.DamageMin,
                    attacker.Stats.DamageMax,
                    _random);
            }

            var impactDelay = CombatAttackRules.ResolveSwingImpactDelay(
                GetUnitAttackInterval(attacker),
                identity);

            if (useHybridMelee
                || identity.UsesMeleeStrike)
            {
                _meleeStrikes.Spawn(new CombatMeleeStrikeState(
                    attacker.UnitId,
                    target.UnitId,
                    rawDamage,
                    impactDelay));
                return;
            }

            if (!identity.UsesProjectile)
            {
                _meleeStrikes.Spawn(new CombatMeleeStrikeState(
                    attacker.UnitId,
                    target.UnitId,
                    rawDamage,
                    impactDelay));
                return;
            }

            CommitSuperAttackSwing(attacker, target.WorldPosition);
            _pendingProjectiles.Spawn(new CombatPendingProjectileState(
                attacker.UnitId,
                target.UnitId,
                rawDamage,
                impactDelay,
                aimWorldPosition: target.WorldPosition));
        }

        public void ReleasePendingProjectile(CombatPendingProjectileState pending)
        {
            if (pending == null)
            {
                return;
            }

            var attacker = GetUnitById(pending.AttackerUnitId);
            if (attacker == null || !attacker.IsAlive)
            {
                return;
            }

            var usesCatapult = HumanBonusUnitRules.UsesCatapultSplash(attacker.BonusSlot);
            var isParabolic = usesCatapult || CombatAttackRules.UsesParabolicArc(attacker.Role);
            var start = CombatProjectileTrajectory.GetProjectileOrigin(attacker);

            if (pending.TargetBuildingInstanceId.HasValue)
            {
                var building = GetBuildingByInstanceId(pending.TargetBuildingInstanceId);
                var aim = building != null && !building.IsRuins
                    ? building.WorldPosition
                    : pending.AimWorldPosition;
                if (aim == default)
                {
                    return;
                }

                var end = CombatProjectileTrajectory.GetProjectileTarget(aim);
                var duration = CombatProjectileTrajectory.ComputeFlightDuration(
                    start,
                    end,
                    CombatAttackRules.ProjectileSpeed);
                var projectile = building != null && !building.IsRuins
                    ? _projectiles.SpawnBuildingAttack(
                        attacker.UnitId,
                        building.InstanceId,
                        attacker.OwnerSlot,
                        attacker.Role,
                        GetPlayerRaceId(attacker.OwnerSlot),
                        pending.RawDamage,
                        duration,
                        start,
                        end,
                        isParabolic,
                        appliesSplashAoe: usesCatapult)
                    : _projectiles.Spawn(
                        attacker.UnitId,
                        targetUnitId: -1,
                        attacker.OwnerSlot,
                        attacker.Role,
                        GetPlayerRaceId(attacker.OwnerSlot),
                        pending.RawDamage,
                        duration,
                        start,
                        end,
                        isParabolic,
                        appliesSplashAoe: usesCatapult);
                EmitNetworkProjectileSpawn(projectile);
                return;
            }

            var target = GetUnitById(pending.TargetUnitId);
            var unitAim = target != null && target.IsAlive
                ? target.WorldPosition
                : pending.AimWorldPosition;
            if (unitAim == default)
            {
                return;
            }

            var shotEnd = CombatProjectileTrajectory.GetProjectileTarget(unitAim);
            var flight = CombatProjectileTrajectory.ComputeFlightDuration(
                start,
                shotEnd,
                CombatAttackRules.ProjectileSpeed);
            var unitProjectile = _projectiles.Spawn(
                attacker.UnitId,
                target != null && target.IsAlive ? target.UnitId : -1,
                attacker.OwnerSlot,
                attacker.Role,
                GetPlayerRaceId(attacker.OwnerSlot),
                pending.RawDamage,
                flight,
                start,
                shotEnd,
                isParabolic,
                appliesSplashAoe: usesCatapult);
            EmitNetworkProjectileSpawn(unitProjectile);
        }

        public void ResolveProjectileImpact(CombatProjectileState projectile)
        {
            if (projectile.TargetBuildingInstanceId.HasValue)
            {
                // FACELESS-013: Void Bastion — 20% of direct attacks against an owner building miss.
                if (RollVoidBastionMiss(projectile.TargetBuildingInstanceId.Value))
                {
                    return;
                }

                _buildings?.TryApplyDamage(
                    projectile.TargetBuildingInstanceId.Value,
                    GetVsBuildingDamage(projectile.AttackerOwnerSlot, projectile.AttackerRole, projectile.RawDamage),
                    projectile.AttackerOwnerSlot);

                if (projectile.AppliesSplashAoe)
                {
                    var attackerVsBuilding = GetUnitById(projectile.AttackerUnitId);
                    var splashRadius = GetSplashRadius(projectile.AttackerOwnerSlot, projectile.AttackerRole);
                    ApplySplashDamage(
                        attackerVsBuilding,
                        projectile.TargetPosition,
                        projectile.RawDamage * HumanBonusUnitRules.CatapultAoeDamagePercent,
                        splashRadius,
                        projectile.AttackerOwnerSlot,
                        excludeUnitId: -1);
                    EmitTraitFx(
                        attackerVsBuilding,
                        AbilityIds.SuperCatapult,
                        projectile.TargetPosition,
                        targetUnitId: 0,
                        splashRadius);
                }

                return;
            }

            var attacker = GetUnitById(projectile.AttackerUnitId);
            var rawDamage = projectile.RawDamage;
            var rangedCrit = false;
            if (attacker != null
                && attacker.BonusSlot == BonusKitRules.BonusSlotForRole(UnitRole.Ranged)
                && BonusKitRules.RollProc(_random, HumanBonusUnitRules.OnHitProcChance))
            {
                rawDamage *= HumanBonusUnitRules.RangedCritMultiplier;
                rangedCrit = true;
            }

            var target = GetUnitById(projectile.TargetUnitId);
            if (target != null && target.IsAlive)
            {
                var effectiveDamage = rawDamage;
                if (projectile.IsBuildingAttack)
                {
                    effectiveDamage *= GetFacelessVoidHardeningMultiplier(target, isBuildingAttack: true);
                }
                ApplyDamage(attacker, target, effectiveDamage, projectile.AttackerOwnerSlot);
                TryApplyFlamingArrowsBurn(projectile, target);
                TryApplyTaintingBolt(attacker, target);
                TryApplyUnnervingAimDebuff(attacker, target);
                TryApplyFacelessSplash(attacker, target, rawDamage, projectile.AttackerOwnerSlot);
            }

            if (rangedCrit)
            {
                EmitTraitFx(
                    attacker,
                    AbilityIds.RangedCrit,
                    projectile.TargetPosition,
                    projectile.TargetUnitId);
            }

            if (projectile.AppliesSplashAoe)
            {
                ApplySplashDamage(
                    attacker,
                    projectile.TargetPosition,
                    rawDamage * HumanBonusUnitRules.CatapultAoeDamagePercent,
                    GetSplashRadius(projectile.AttackerOwnerSlot, projectile.AttackerRole),
                    projectile.AttackerOwnerSlot,
                    excludeUnitId: projectile.TargetUnitId);
                EmitTraitFx(
                    attacker,
                    AbilityIds.SuperCatapult,
                    projectile.TargetPosition,
                    projectile.TargetUnitId,
                    HumanBonusUnitRules.CatapultAoeRadius);
            }
        }

        /// <summary>
        /// Flaming Arrows (tower track 1): ranged/flying unit shots and living tower shots
        /// ignite the target on impact.
        /// </summary>
        void TryApplyFlamingArrowsBurn(CombatProjectileState projectile, MatchUnitState target)
        {
            var isTowerShot = projectile.IsBuildingAttack
                && BuildingRules.IsTower(projectile.SourceBuildingId);
            if (!isTowerShot && !TowerTrackRules.RoleMatches(0, projectile.AttackerRole))
            {
                return;
            }

            if (projectile.AttackerOwnerSlot == target.OwnerSlot)
            {
                return;
            }

            var level = GetTowerTrackLevel(projectile.AttackerOwnerSlot, 0);
            if (level <= 0)
            {
                return;
            }

            target.BurnSourceOwnerSlot = projectile.AttackerOwnerSlot;
            target.BurnDamagePerSecond = level * TowerTrackRules.BurnDamagePerSecondPerLevel;
            target.BurnSecondsRemaining = TowerTrackRules.BurnDurationSeconds;
        }

        public void ResolveMeleeImpact(CombatMeleeStrikeState strike)
        {
            var attacker = GetUnitById(strike.AttackerUnitId);
            if (strike.TargetBuildingInstanceId.HasValue)
            {
                // FACELESS-013: Void Bastion — 20% of direct attacks against an owner building miss.
                if (RollVoidBastionMiss(strike.TargetBuildingInstanceId.Value))
                {
                    return;
                }

                var killerSlot = attacker?.OwnerSlot ?? GetUnitOwnerSlot(strike.AttackerUnitId);
                var buildingDamage = attacker != null
                    ? GetVsBuildingDamage(killerSlot, attacker.Role, strike.RawDamage)
                    : strike.RawDamage;
                _buildings?.TryApplyDamage(
                    strike.TargetBuildingInstanceId.Value,
                    buildingDamage,
                    killerSlot);
                return;
            }

            var target = GetUnitById(strike.TargetUnitId);
            if (target != null && target.IsAlive)
            {
                var killerSlot = attacker?.OwnerSlot ?? GetUnitOwnerSlot(strike.AttackerUnitId);
                var meleeDealt = ApplyDamage(
                    attacker,
                    target,
                    ApplyBulwarkBlock(target, strike.RawDamage),
                    killerSlot);
                TryApplyHungerOfTheOldOne(attacker, meleeDealt);
                TryApplyUnnervingAimDebuff(attacker, target);
                TryApplyFacelessSplash(attacker, target, strike.RawDamage, killerSlot);

                if (attacker != null && HumanBonusUnitRules.IsCasterBonus(attacker))
                {
                    EmitTraitFx(
                        attacker,
                        AbilityIds.CasterHybrid,
                        target.WorldPosition,
                        target.UnitId);
                }

                if (attacker != null
                    && attacker.BonusSlot == BonusKitRules.BonusSlotForRole(UnitRole.Melee)
                    && BonusKitRules.RollProc(_random, HumanBonusUnitRules.OnHitProcChance))
                {
                    ApplySplashDamage(
                        attacker,
                        target.WorldPosition,
                        strike.RawDamage,
                        HumanBonusUnitRules.MeleeAoeRadius,
                        killerSlot,
                        excludeUnitId: target.UnitId);
                    EmitTraitFx(
                        attacker,
                        AbilityIds.MeleeCleave,
                        target.WorldPosition,
                        target.UnitId,
                        HumanBonusUnitRules.MeleeAoeRadius);
                }
            }
        }

        /// <summary>
        /// AoE damage to living enemies around a point (excludes one unit id — the primary hit).
        /// </summary>
        public void ApplySplashDamage(
            MatchUnitState attacker,
            Vector3 center,
            float rawDamage,
            float radius,
            int killerOwnerSlot,
            int excludeUnitId)
        {
            if (rawDamage <= 0f || radius <= 0f)
            {
                return;
            }

            _nearbyBuffer.Clear();
            _spatialGrid.Rebuild(_units);
            _spatialGrid.Query(center, radius, _nearbyBuffer);
            var radiusSq = radius * radius;
            for (var i = 0; i < _nearbyBuffer.Count; i++)
            {
                var other = _nearbyBuffer[i];
                if (other == null
                    || !other.IsAlive
                    || other.UnitId == excludeUnitId
                    || other.OwnerSlot == killerOwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(center, other.WorldPosition) > radiusSq)
                {
                    continue;
                }

                ApplyDamage(attacker, other, rawDamage, killerOwnerSlot);
            }
        }

        /// <summary>
        /// Applies damage to <paramref name="target"/> and returns the damage actually dealt
        /// (after armor, attacker multipliers and absorb). Zero when the target is dead/missing.
        /// </summary>
        public float ApplyDamage(MatchUnitState attacker, MatchUnitState target, float rawDamage, int killerOwnerSlot)
        {
            if (target == null || !target.IsAlive)
            {
                return 0f;
            }

            // FACELESS-012: Area of Miss — an evading target avoids all incoming attacks.
            if (target.EvadeRemainingSeconds > 0f)
            {
                return 0f;
            }

            // FACELESS-013: Shadow of the Void — a flagged target avoids 8% of all incoming attacks.
            if (target.ShadowEvadeActive
                && BonusKitRules.RollProc(_random, FacelessBonusUnitRules.ShadowEvadeChance))
            {
                return 0f;
            }

            var armor = Mathf.Max(0f, GetEffectiveArmor(target) - GetFacelessArmorPen(attacker));
            var damage = CombatRules.ApplyArmor(rawDamage, armor);
            damage *= GetArmyDamageMultiplier(attacker);
            damage *= GetLastStandDamageMultiplier(attacker);
            if (attacker != null && attacker.UltimateBuffRemaining > 0f)
            {
                var percent = attacker.UltimateBuffPercent > 0f
                    ? attacker.UltimateBuffPercent
                    : HeroAbilityRules.UltimateSelfDamageBonusPercent;
                damage *= 1f + percent;
            }

            damage *= GetFacelessRitualMultiplier(target, attacker);

            if (target.AbsorbRemaining > 0f)
            {
                var absorbed = Mathf.Min(target.AbsorbRemaining, damage);
                target.AbsorbRemaining -= absorbed;
                damage -= absorbed;
            }

            target.CurrentHp -= damage;

            // FACELESS-012: army lifesteal from the veteran signatures (Aura of Hunger, Feast Zone).
            var dealt = damage;
            if (attacker != null)
            {
                TryApplyAuraOfHunger(attacker, dealt);
                TryApplyFeastZone(attacker, dealt);
            }

            if (target.IsAlive)
            {
                return damage;
            }

            if (attacker != null && attacker.CurrentTargetId == target.UnitId)
            {
                ClearTarget(attacker);
            }

            var bounty = CombatRules.ComputeKillBounty(target.Stats.GoldBounty, target.IsChampion);
            GrantGold(killerOwnerSlot, bounty);

            var deathOwnerSlot = target.OwnerSlot;
            var deathLaneId = target.LaneId;
            var deathPosition = target.WorldPosition;
            var deathBonusSlot = target.BonusSlot;
            var deathMarchFocus = target.MarchFocusOpponentSlot;
            var deathMaxHp = target.Stats.MaxHp;

            _corpses.Add(new CombatCorpseState(target));
            RemoveUnit(target);
            TrySpawnFlyingBonusOnDeath(deathOwnerSlot, deathLaneId, deathPosition, deathBonusSlot, deathMarchFocus);
            TryApplyBloodrageOnKill(attacker);
            TryApplyVacuumCollapseOnDeath(deathOwnerSlot, deathPosition);
            TryApplyCallOfTheAbyss(attacker);
            TryApplyHungeringFlight(attacker);
            TryApplyFeastOnTheFallen(attacker);
            TryApplyDeathExplosion(deathOwnerSlot, deathPosition, deathBonusSlot, deathMaxHp);
            UnitKilled?.Invoke(new UnitKillEvent(
                killerOwnerSlot,
                deathOwnerSlot,
                target.UnitId,
                bounty,
                target.Role,
                attacker?.UnitId ?? 0));
            return damage;
        }

        void TrySpawnFlyingBonusOnDeath(
            int ownerSlot,
            string laneId,
            Vector3 worldPosition,
            int bonusSlot,
            int marchFocusOpponentSlot)
        {
            if (bonusSlot != BonusKitRules.BonusSlotForRole(UnitRole.Flying)
                || !BonusKitRules.RollProc(_random, HumanBonusUnitRules.OnDeathSpawnChance))
            {
                return;
            }

            if (!_routes.TryGetRoute(ownerSlot, laneId, out var route))
            {
                return;
            }

            var player = ownerSlot >= 0 && ownerSlot < _players.Count ? _players[ownerSlot] : null;
            var raceId = player?.RaceId ?? GameIds.Races.Human;
            var stats = UnitStatsResolver.Resolve(
                catalog: null,
                UnitVisualCatalog,
                raceId,
                UnitRole.Ranged,
                player,
                bonusSlot: 0);

            var progressDistance = route.ProjectDistance(worldPosition);
            var spawned = new MatchUnitState(
                _nextUnitId++,
                ownerSlot,
                laneId,
                UnitRole.Ranged,
                stats,
                stats.MaxHp,
                worldPosition,
                route.FindMarchWaypointIndex(worldPosition),
                marchSpawnDistance: progressDistance,
                bonusSlot: 0);
            spawned.MarchProgressDistance = progressDistance;
            spawned.MarchFocusOpponentSlot = marchFocusOpponentSlot;
            ApplySpawnFacing(spawned, route, progressDistance);
            ApplyMarchFocusFromLane(spawned, ownerSlot, laneId);
            AttachAbilities(spawned);
            ApplySpawnUniqueModifiers(spawned);
            _units.Add(spawned);
            _unitById[spawned.UnitId] = spawned;
            EmitTraitFxUnbound(ownerSlot, AbilityIds.FlyingSpawn, worldPosition);
        }

        int GetUnitOwnerSlot(int unitId)
        {
            var unit = GetUnitById(unitId);
            return unit?.OwnerSlot ?? 0;
        }

        /// <summary>
        /// King aura (passive AuraDamagePercent def): army of the owner's living champion
        /// deals extra damage while that slot is unlocked.
        /// </summary>
        float GetArmyDamageMultiplier(MatchUnitState attacker)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return 1f;
            }

            return 1f + GetAuraPercent(attacker.OwnerSlot, AuraStat.Damage, attacker.WorldPosition);
        }

        float GetArmyAttackSpeedMultiplier(MatchUnitState attacker)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return 1f;
            }

            return 1f + GetAuraPercent(attacker.OwnerSlot, AuraStat.AttackSpeed, attacker.WorldPosition);
        }

        /// <summary>Last Stand (tower track 9): bonus damage while the attacker is below the HP threshold.</summary>
        float GetLastStandDamageMultiplier(MatchUnitState attacker)
        {
            if (attacker == null
                || !attacker.IsAlive
                || attacker.IsChampion
                || !TowerTrackRules.RoleMatches(8, attacker.Role))
            {
                return 1f;
            }

            var level = GetTowerTrackLevel(attacker.OwnerSlot, 8);
            if (level <= 0
                || attacker.CurrentHp >= GetEffectiveMaxHp(attacker) * TowerTrackRules.LowHealthThreshold)
            {
                return 1f;
            }

            return 1f + TowerTrackRules.LastStandDamagePercentByLevel[level - 1];
        }

        /// <summary>Bloodrage (tower track 3): attack-speed buff after killing an enemy.</summary>
        void TryApplyBloodrageOnKill(MatchUnitState attacker)
        {
            if (attacker == null
                || !attacker.IsAlive
                || attacker.IsChampion
                || !TowerTrackRules.RoleMatches(2, attacker.Role))
            {
                return;
            }

            if (GetTowerTrackLevel(attacker.OwnerSlot, 2) <= 0)
            {
                return;
            }

            attacker.BloodrageRemainingSeconds = TowerTrackRules.BloodrageDurationSeconds;
        }

        /// <summary>Attack-speed multiplier while Bloodrage is active (divides the attack interval).</summary>
        float GetBloodrageMultiplier(MatchUnitState unit)
        {
            if (unit == null
                || unit.IsChampion
                || unit.BloodrageRemainingSeconds <= 0f
                || !TowerTrackRules.RoleMatches(2, unit.Role))
            {
                return 1f;
            }

            var level = GetTowerTrackLevel(unit.OwnerSlot, 2);
            if (level <= 0)
            {
                return 1f;
            }

            return 1f + TowerTrackRules.BloodrageAttackSpeedPercentByLevel[level - 1];
        }

        /// <summary>
        /// FACELESS-013 (Void Bastion): 20% of direct attacks against an owner building miss.
        /// Checked at attack time against the owner's current pick — existing and newly built
        /// buildings are both protected (retro).
        /// </summary>
        bool RollVoidBastionMiss(int buildingInstanceId)
        {
            if (_buildings == null)
            {
                return false;
            }

            var building = _buildings.GetByInstanceId(buildingInstanceId);
            if (building == null
                || building.OwnerSlot < 0
                || building.OwnerSlot >= _players.Count)
            {
                return false;
            }

            return FacelessBonusUnitRules.HasVoidBastion(_players[building.OwnerSlot])
                && BonusKitRules.RollProc(_random, FacelessBonusUnitRules.VoidBastionMissChance);
        }

        /// <summary>Battering Rams (tower track 4): bonus damage against buildings.</summary>
        float GetVsBuildingDamage(int ownerSlot, UnitRole role, float rawDamage)
        {
            if (!TowerTrackRules.RoleMatches(3, role))
            {
                return rawDamage;
            }

            var level = GetTowerTrackLevel(ownerSlot, 3);
            return level > 0
                ? rawDamage * (1f + TowerTrackRules.VsBuildingDamagePercentPerLevel * level)
                : rawDamage;
        }

        /// <summary>Catapult splash radius with the Battering Rams L3 bonus.</summary>
        float GetSplashRadius(int ownerSlot, UnitRole role)
        {
            var radius = HumanBonusUnitRules.CatapultAoeRadius;
            if (!TowerTrackRules.RoleMatches(3, role))
            {
                return radius;
            }

            var level = GetTowerTrackLevel(ownerSlot, 3);
            return level >= MatchEconomyRules.MaxTowerTrackLevel
                ? radius + TowerTrackRules.SplashRadiusBonusAtMaxLevel
                : radius;
        }

        /// <summary>
        /// Bulwark L3 block (tower track 2): flat reduction of incoming melee damage
        /// for melee/siege units of owners with the track maxed.
        /// </summary>
        float ApplyBulwarkBlock(MatchUnitState target, float rawDamage)
        {
            if (target == null
                || !target.IsAlive
                || target.IsChampion
                || !TowerTrackRules.RoleMatches(1, target.Role))
            {
                return rawDamage;
            }

            var level = GetTowerTrackLevel(target.OwnerSlot, 1);
            return level >= MatchEconomyRules.MaxTowerTrackLevel
                ? rawDamage * (1f - TowerTrackRules.BulwarkBlockDamageReduction)
                : rawDamage;
        }

        float GetEffectiveArmor(MatchUnitState target)
        {
            if (target == null)
            {
                return 0f;
            }

            var armor = target.Stats.Armor;
            armor *= 1f + GetAuraPercent(target.OwnerSlot, AuraStat.Armor, target.WorldPosition);

            if (target.ArmorBuffRemaining > 0f)
            {
                armor += target.ArmorBuffBonus > 0f
                    ? target.ArmorBuffBonus
                    : HeroAbilityRules.ShieldArmorBonus;
            }

            if (target.ArmorDebuffRemainingSeconds > 0f)
            {
                armor -= target.ArmorDebuffAmount;
            }

            return Mathf.Max(0f, armor);
        }

        public float GetEffectiveMaxHp(MatchUnitState unit)
        {
            if (unit == null)
            {
                return 0f;
            }

            return unit.Stats.MaxHp * (1f + GetAuraPercent(unit.OwnerSlot, AuraStat.MaxHp, unit.WorldPosition));
        }

        float GetEffectiveMarchSpeed(MatchUnitState unit)
        {
            if (unit == null)
            {
                return 0f;
            }

            if (unit.SlowRemainingSeconds <= 0f)
            {
                return unit.MarchMoveSpeed;
            }

            return unit.MarchMoveSpeed * (1f - unit.SlowPercent);
        }

        // === Faceless Tower Track Helpers (FACELESS-017) ===

        float GetFacelessArmorPen(MatchUnitState attacker)
        {
            if (attacker == null || !attacker.IsAlive || attacker.IsChampion)
            {
                return 0f;
            }

            if (!FacelessTowerTrackRules.RoleMatches(FacelessTowerTrackRules.HollowBarbsTrackIndex, attacker.Role))
            {
                return 0f;
            }

            var level = GetTowerTrackLevel(attacker.OwnerSlot, FacelessTowerTrackRules.HollowBarbsTrackIndex);
            if (level <= 0)
            {
                return 0f;
            }

            return FacelessTowerTrackRules.HollowBarbsArmorPenByLevel[level - 1];
        }

        float GetFacelessRitualMultiplier(MatchUnitState target, MatchUnitState attacker)
        {
            if (target == null || !target.IsAlive)
            {
                return 1f;
            }

            if (attacker != null && attacker.OwnerSlot == target.OwnerSlot)
            {
                return 1f;
            }

            var level = GetTowerTrackLevel(target.OwnerSlot, FacelessTowerTrackRules.RitualOfTheDeepTrackIndex);
            if (level <= 0)
            {
                return 1f;
            }

            if (!HasLivingCasterWithinRadius(target.WorldPosition, target.OwnerSlot, FacelessTowerTrackRules.RitualRadius))
            {
                return 1f;
            }

            return 1f - FacelessTowerTrackRules.RitualReductionPercentByLevel[level - 1];
        }

        bool HasLivingCasterWithinRadius(Vector3 center, int ownerSlot, float radius)
        {
            var radiusSq = radius * radius;
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit.OwnerSlot != ownerSlot)
                {
                    continue;
                }

                if (unit.Role != UnitRole.Caster)
                {
                    continue;
                }

                if (HorizontalDistanceSq(center, unit.WorldPosition) <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        float GetFacelessVoidHardeningMultiplier(MatchUnitState target, bool isBuildingAttack)
        {
            if (target == null || !target.IsAlive || target.IsChampion || !isBuildingAttack)
            {
                return 1f;
            }

            if (!FacelessTowerTrackRules.RoleMatches(FacelessTowerTrackRules.VoidHardeningTrackIndex, target.Role))
            {
                return 1f;
            }

            var level = GetTowerTrackLevel(target.OwnerSlot, FacelessTowerTrackRules.VoidHardeningTrackIndex);
            if (level <= 0)
            {
                return 1f;
            }

            return 1f - FacelessTowerTrackRules.VoidHardeningReductionPercentByLevel[level - 1];
        }

        void TryApplyVacuumCollapseOnDeath(int deathOwnerSlot, Vector3 deathPosition)
        {
            var player = deathOwnerSlot >= 0 && deathOwnerSlot < _players.Count ? _players[deathOwnerSlot] : null;
            if (player == null || player.RaceId != GameIds.Races.Faceless)
            {
                return;
            }

            var level = GetTowerTrackLevel(deathOwnerSlot, FacelessTowerTrackRules.VacuumCollapseTrackIndex);
            if (level <= 0)
            {
                return;
            }

            var slowPercent = FacelessTowerTrackRules.VacuumCollapseSlowPercentByLevel[level - 1];
            var radius = FacelessTowerTrackRules.VacuumCollapseRadius;
            var duration = FacelessTowerTrackRules.VacuumCollapseSlowDurationSeconds;

            _nearbyBuffer.Clear();
            _spatialGrid.Rebuild(_units);
            _spatialGrid.Query(deathPosition, radius, _nearbyBuffer);
            var radiusSq = radius * radius;

            for (var i = 0; i < _nearbyBuffer.Count; i++)
            {
                var enemy = _nearbyBuffer[i];
                if (enemy == null || !enemy.IsAlive || enemy.OwnerSlot == deathOwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(deathPosition, enemy.WorldPosition) > radiusSq)
                {
                    continue;
                }

                enemy.SlowRemainingSeconds = duration;
                enemy.SlowPercent = slowPercent;
            }
        }

        void TryApplyUnnervingAimDebuff(MatchUnitState attacker, MatchUnitState target)
        {
            if (attacker == null || !attacker.IsAlive || attacker.IsChampion)
            {
                return;
            }

            if (target == null || !target.IsAlive || target.OwnerSlot == attacker.OwnerSlot)
            {
                return;
            }

            if (!FacelessTowerTrackRules.RoleMatches(FacelessTowerTrackRules.UnnervingAimTrackIndex, attacker.Role))
            {
                return;
            }

            var level = GetTowerTrackLevel(attacker.OwnerSlot, FacelessTowerTrackRules.UnnervingAimTrackIndex);
            if (level <= 0)
            {
                return;
            }

            target.ArmorDebuffRemainingSeconds = FacelessTowerTrackRules.UnnervingAimDebuffDurationSeconds;
            target.ArmorDebuffAmount = FacelessTowerTrackRules.UnnervingAimArmorDebuffByLevel[level - 1];
        }

        void TryApplyFacelessSplash(MatchUnitState attacker, MatchUnitState target, float rawDamage, int killerOwnerSlot)
        {
            if (attacker == null || !attacker.IsAlive || attacker.IsChampion)
            {
                return;
            }

            if (!FacelessTowerTrackRules.RoleMatches(FacelessTowerTrackRules.SplashOfTheDeepTrackIndex, attacker.Role))
            {
                return;
            }

            var level = GetTowerTrackLevel(attacker.OwnerSlot, FacelessTowerTrackRules.SplashOfTheDeepTrackIndex);
            if (level <= 0)
            {
                return;
            }

            var radius = FacelessTowerTrackRules.SplashRadiusByLevel[level - 1];
            var damagePercent = FacelessTowerTrackRules.SplashDamagePercentByLevel[level - 1];
            ApplySplashDamage(attacker, target.WorldPosition, rawDamage * damagePercent, radius, killerOwnerSlot, excludeUnitId: target.UnitId);
        }

        // === Faceless Unit Bonus Helpers (FACELESS-011) ===

        /// <summary>Slot 1 Melee — Hunger of the Old One: 15% on-hit heal for 50% of the damage dealt.</summary>
        void TryApplyHungerOfTheOldOne(MatchUnitState attacker, float dealtDamage)
        {
            if (attacker == null || !attacker.IsAlive || dealtDamage <= 0f)
            {
                return;
            }

            if (attacker.BonusSlot != BonusKitRules.BonusSlotForRole(UnitRole.Melee))
            {
                return;
            }

            if (!FacelessBonusUnitRules.IsFacelessBonus(
                    attacker.BonusSlot,
                    attacker.Role,
                    GetPlayerRaceId(attacker.OwnerSlot)))
            {
                return;
            }

            if (!BonusKitRules.RollProc(_random, FacelessBonusUnitRules.VampiricProcChance))
            {
                return;
            }

            HealUnit(attacker, dealtDamage * FacelessBonusUnitRules.VampiricHealPercent);
        }

        /// <summary>Slot 2 Ranged — Tainting Bolt: 15% on-hit dot (3 dmg/s for 3 s). Reuses the burn timers.</summary>
        void TryApplyTaintingBolt(MatchUnitState attacker, MatchUnitState target)
        {
            if (attacker == null || !attacker.IsAlive || target == null || !target.IsAlive)
            {
                return;
            }

            if (attacker.OwnerSlot == target.OwnerSlot)
            {
                return;
            }

            if (attacker.BonusSlot != BonusKitRules.BonusSlotForRole(UnitRole.Ranged))
            {
                return;
            }

            if (!FacelessBonusUnitRules.IsFacelessBonus(
                    attacker.BonusSlot,
                    attacker.Role,
                    GetPlayerRaceId(attacker.OwnerSlot)))
            {
                return;
            }

            if (!BonusKitRules.RollProc(_random, FacelessBonusUnitRules.TaintingBoltProcChance))
            {
                return;
            }

            target.BurnSourceOwnerSlot = attacker.OwnerSlot;
            target.BurnDamagePerSecond = FacelessBonusUnitRules.TaintingBoltDamagePerSecond;
            target.BurnSecondsRemaining = FacelessBonusUnitRules.TaintingBoltDurationSeconds;
        }

        /// <summary>Slot 3 Caster — Call of the Abyss: on kill spawn one ×0.5 mini-melee.</summary>
        void TryApplyCallOfTheAbyss(MatchUnitState attacker)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return;
            }

            if (attacker.BonusSlot != BonusKitRules.BonusSlotForRole(UnitRole.Caster))
            {
                return;
            }

            if (!FacelessBonusUnitRules.IsFacelessBonus(
                    attacker.BonusSlot,
                    attacker.Role,
                    GetPlayerRaceId(attacker.OwnerSlot)))
            {
                return;
            }

            // Stat baseline resolution lives in SummonMinion (living friendly Melee, else the anchor).
            SummonMinion(
                attacker.OwnerSlot,
                attacker,
                UnitRole.Melee,
                FacelessBonusUnitRules.MiniMeleeStatScale);
        }

        /// <summary>
        /// Slots 5–6 — on-kill attack-speed stacks (and the slot 6 flat heal).
        /// Fires only for the mechanic-specific slot so a Flying (5) unit never
        /// double-stacks via the Super (6) helper and vice versa.
        /// </summary>
        void TryApplyFeastStacks(MatchUnitState attacker, float healAmount, int expectedBonusSlot)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return;
            }

            if (!FacelessBonusUnitRules.IsFacelessBonus(
                    attacker.BonusSlot,
                    attacker.Role,
                    GetPlayerRaceId(attacker.OwnerSlot)))
            {
                return;
            }

            if (attacker.BonusSlot != expectedBonusSlot)
            {
                return;
            }

            var perStack = FacelessBonusUnitRules.AttackSpeedPerStackForSlot(attacker.BonusSlot);
            if (perStack <= 0f)
            {
                return;
            }

            if (healAmount > 0f)
            {
                HealUnit(attacker, healAmount);
            }

            attacker.FeastAttackSpeedPerStack = perStack;
            attacker.FeastStacks = Mathf.Min(FacelessBonusUnitRules.MaxFeastStacks, attacker.FeastStacks + 1);
            attacker.FeastRemainingSeconds = FacelessBonusUnitRules.FeastBuffDurationSeconds;
        }

        /// <summary>Slot 5 Flying — Hungering Flight: on kill +15% attack speed, stacks up to 3.</summary>
        void TryApplyHungeringFlight(MatchUnitState attacker) =>
            TryApplyFeastStacks(attacker, 0f, BonusKitRules.BonusSlotForRole(UnitRole.Flying));

        /// <summary>Slot 6 Super — Feast on the Fallen: on kill +80 HP and +10% attack speed, stacks up to 3.</summary>
        void TryApplyFeastOnTheFallen(MatchUnitState attacker) =>
            TryApplyFeastStacks(attacker, FacelessBonusUnitRules.FeastHealFlat, BonusKitRules.BonusSlotForRole(UnitRole.Super));

        /// <summary>
        /// Slot 4 Siege — Death Explosion: on death deal 10% of the owner's max HP to enemy
        /// units within radius 3. Buildings are never hit (splash only iterates units).
        /// </summary>
        void TryApplyDeathExplosion(
            int deathOwnerSlot,
            Vector3 deathPosition,
            int deathBonusSlot,
            float deathMaxHp)
        {
            if (!FacelessBonusUnitRules.IsFacelessBonus(
                    deathBonusSlot,
                    UnitRole.Siege,
                    GetPlayerRaceId(deathOwnerSlot)))
            {
                return;
            }

            ApplySplashDamage(
                attacker: null,
                deathPosition,
                deathMaxHp * FacelessBonusUnitRules.DeathExplosionMaxHpPercent,
                FacelessBonusUnitRules.DeathExplosionRadius,
                deathOwnerSlot,
                excludeUnitId: -1);
        }

        // === Faceless Champion Veteran Helpers (FACELESS-012) ===

        /// <summary>Slot 10 Titan — Aura of Hunger: each owner unit lifesteals a fraction of the damage it deals.</summary>
        void TryApplyAuraOfHunger(MatchUnitState attacker, float dealtDamage)
        {
            if (attacker == null || !attacker.IsAlive || dealtDamage <= 0f)
            {
                return;
            }

            if (GetPlayerRaceId(attacker.OwnerSlot) != GameIds.Races.Faceless)
            {
                return;
            }

            if (!HasLivingChampionWithBonus(attacker.OwnerSlot, BonusKitRules.TitanBonusSlot))
            {
                return;
            }

            HealUnit(attacker, dealtDamage * FacelessBonusUnitRules.AuraOfHungerHealFraction);
        }

        /// <summary>Slot 9 Berserker — Feast Zone: allies inside the following zone heal a fraction of damage dealt.</summary>
        void TryApplyFeastZone(MatchUnitState attacker, float dealtDamage)
        {
            if (attacker == null || !attacker.IsAlive || dealtDamage <= 0f)
            {
                return;
            }

            if (GetPlayerRaceId(attacker.OwnerSlot) != GameIds.Races.Faceless)
            {
                return;
            }

            var zone = GetActiveFeastZone(attacker.OwnerSlot, attacker);
            if (zone == null)
            {
                return;
            }

            HealUnit(attacker, dealtDamage * zone.HealFractionOfDamageDealt);
        }

        bool HasLivingChampionWithBonus(int ownerSlot, int bonusSlot)
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null
                    || !unit.IsAlive
                    || unit.OwnerSlot != ownerSlot
                    || unit.BonusSlot != bonusSlot)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        HeroHealZoneState GetActiveFeastZone(int ownerSlot, MatchUnitState unit)
        {
            if (unit == null || _healZones == null)
            {
                return null;
            }

            for (var i = _healZones.Count - 1; i >= 0; i--)
            {
                var zone = _healZones[i];
                if (zone.OwnerSlot != ownerSlot || zone.HealFractionOfDamageDealt <= 0f)
                {
                    continue;
                }

                var radiusSq = zone.Radius * zone.Radius;
                var dx = zone.Center.x - unit.WorldPosition.x;
                var dz = zone.Center.z - unit.WorldPosition.z;
                if (dx * dx + dz * dz <= radiusSq)
                {
                    return zone;
                }
            }

            return null;
        }

        /// <summary>Heals a living unit, clamped to its effective max HP.</summary>
        public void HealUnit(MatchUnitState unit, float amount)
        {
            if (unit == null || !unit.IsAlive || amount <= 0f)
            {
                return;
            }

            var maxHp = GetEffectiveMaxHp(unit);
            if (unit.CurrentHp >= maxHp)
            {
                return;
            }

            unit.CurrentHp = Mathf.Min(maxHp, unit.CurrentHp + amount);
        }

        /// <summary>Attack-speed multiplier from Faceless on-kill stacks (slots 5–6).</summary>
        float GetFacelessFeastAsMultiplier(MatchUnitState unit)
        {
            if (unit == null
                || unit.FeastRemainingSeconds <= 0f
                || unit.FeastStacks <= 0
                || unit.FeastAttackSpeedPerStack <= 0f)
            {
                return 1f;
            }

            return 1f + unit.FeastAttackSpeedPerStack * unit.FeastStacks;
        }

        /// <summary>
        /// Strongest unlocked passive-aura contribution for <paramref name="stat"/> among the owner's
        /// living aura bearers within <paramref name="position"/>'s aura radius (radius 0 = whole army).
        /// </summary>
        float GetAuraPercent(int ownerSlot, AuraStat stat, Vector3 position)
        {
            var best = 0f;
            for (var i = 0; i < _units.Count; i++)
            {
                var candidate = _units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot != ownerSlot
                    || candidate.Abilities == null)
                {
                    continue;
                }

                for (var s = 0; s < candidate.Abilities.Length; s++)
                {
                    var def = candidate.Abilities[s];
                    if (def == null
                        || def.IsActive
                        || !IsAbilitySlotUnlocked(candidate, def))
                    {
                        continue;
                    }

                    var radius = def.Radius > 0f ? def.Radius : HeroAbilityRules.AuraRadius;
                    if (radius > 0f)
                    {
                        var dx = candidate.WorldPosition.x - position.x;
                        var dz = candidate.WorldPosition.z - position.z;
                        if (dx * dx + dz * dz > radius * radius)
                        {
                            continue;
                        }
                    }

                    var ctx = new UnitAbilityContext(
                        this,
                        candidate,
                        def,
                        s,
                        GetMagicLevel(ownerSlot));
                    var value = def.Behaviour != null ? def.Behaviour.QueryAura(in ctx, stat) : 0f;
                    if (value > best)
                    {
                        best = value;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Resolves the persistent aura disc a unit should display: radius + packed color of its
        /// first unlocked passive <see cref="AuraBehaviour"/> (used by snapshot capture on the host).
        /// Passive traits that only reuse <c>Radius</c> for on-hit AoE / splash (cleave, catapult)
        /// must not draw a disc.
        /// </summary>
        public bool TryGetAuraVisual(MatchUnitState unit, out float radius, out int packedColor) =>
            TryGetAuraVisual(unit, out radius, out packedColor, out _);

        public bool TryGetAuraVisual(
            MatchUnitState unit,
            out float radius,
            out int packedColor,
            out int abilityId)
        {
            radius = 0f;
            packedColor = 0;
            abilityId = 0;
            if (unit == null || !unit.IsAlive || unit.Abilities == null)
            {
                return false;
            }

            for (var s = 0; s < unit.Abilities.Length; s++)
            {
                var def = unit.Abilities[s];
                if (def == null
                    || !def.IsPassiveAura
                    || def.Radius <= 0f
                    || def.Behaviour is not AuraBehaviour
                    || !IsAbilitySlotUnlocked(unit, def))
                {
                    continue;
                }

                radius = def.Radius;
                abilityId = def.AbilityId;
                var color = def.Fx.Color != default(Color)
                    ? def.Fx.Color
                    : PassiveAuraFxRules.ResolveTint(abilityId);
                packedColor = AbilityFx.ToRgbaInt(color);
                return true;
            }

            return false;
        }

        float GetUnitAttackInterval(MatchUnitState unit) =>
            CombatRules.GetAttackIntervalSeconds(
                unit.Stats.AttackSpeed * GetArmyAttackSpeedMultiplier(unit))
            / GetBloodrageMultiplier(unit)
            / GetFacelessFeastAsMultiplier(unit);

        /// <summary>Effective attack interval including Haste Aura (presenter / anim speed).</summary>
        public float GetAttackIntervalSeconds(MatchUnitState unit) =>
            unit == null ? 1f : GetUnitAttackInterval(unit);

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

        /// <summary>Race-aware combat identity for a unit (race of the owning player).</summary>
        UnitCombatIdentity IdentityOf(MatchUnitState unit) =>
            UnitCombatIdentityFactory.From(unit, GetPlayerRaceId(unit.OwnerSlot));

        public int GetMagicLevel(int ownerSlot)
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

        /// <summary>Tower track level (PRE-007) of the given player slot, 0 when absent.</summary>
        public int GetTowerTrackLevel(int ownerSlot, int trackIndex)
        {
            foreach (var player in _players)
            {
                if (player.SlotIndex == ownerSlot)
                {
                    return player.GetTowerTrackLevel(trackIndex);
                }
            }

            return 0;
        }

        void AttachAbilities(MatchUnitState unit)
        {
            if (unit == null)
            {
                return;
            }

            if (!TryCopyAbilitiesFromPrefab(unit))
            {
                unit.Abilities = AbilityKitDefaults.CreateForSpawn(
                    GetPlayerRaceId(unit.OwnerSlot),
                    unit.Role,
                    unit.HeroSlot,
                    unit.BonusSlot);
            }

            var count = unit.Abilities?.Length ?? 0;
            unit.AbilityCooldownRemaining = count > 0 ? new float[count] : System.Array.Empty<float>();
        }

        bool TryCopyAbilitiesFromPrefab(MatchUnitState unit)
        {
            if (UnitVisualCatalog == null)
            {
                return false;
            }

            var raceId = GetPlayerRaceId(unit.OwnerSlot);
            if (!UnitVisualCatalog.TryGetPrefab(
                    raceId,
                    unit.Role,
                    unit.HeroSlot,
                    unit.BonusSlot,
                    out var prefab)
                || prefab == null)
            {
                return false;
            }

            var settings = prefab.GetComponentInChildren<UnitCombatSettings>(true);
            if (settings == null || settings.Abilities.Length == 0)
            {
                return false;
            }

            unit.Abilities = settings.Abilities;
            return true;
        }

        bool IsAbilitySlotUnlocked(MatchUnitState unit, UnitAbilityDef def)
        {
            if (def == null)
            {
                return false;
            }

            return def.Unlock switch
            {
                AbilityUnlock.Always => true,
                AbilityUnlock.HeroLevel => unit.Level >= def.UnlockValue,
                AbilityUnlock.MagicLevel => GetMagicLevel(unit.OwnerSlot) >= def.UnlockValue,
                _ => false,
            };
        }

        /// <summary>
        /// Ticks per-slot cooldowns every tick (even during cast-lock), then casts the first
        /// unlocked ready active ability in kit order.
        /// </summary>
        void TickAbilityCooldowns(MatchUnitState unit, float deltaTime)
        {
            var defs = unit.Abilities;
            if (defs == null || defs.Length == 0)
            {
                return;
            }

            if (unit.AbilityCooldownRemaining == null || unit.AbilityCooldownRemaining.Length != defs.Length)
            {
                unit.AbilityCooldownRemaining = new float[defs.Length];
            }

            for (var i = 0; i < defs.Length; i++)
            {
                unit.AbilityCooldownRemaining[i] = Mathf.Max(0f, unit.AbilityCooldownRemaining[i] - deltaTime);
            }
        }

        bool TryCastKitAbilities(MatchUnitState unit, float deltaTime)
        {
            var defs = unit.Abilities;
            if (defs == null || defs.Length == 0)
            {
                return false;
            }

            unit.UltimateBuffRemaining = Mathf.Max(0f, unit.UltimateBuffRemaining - deltaTime);
            if (unit.UltimateBuffRemaining <= 0f)
            {
                unit.UltimateBuffPercent = 0f;
            }

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null || !def.IsActive)
                {
                    continue;
                }

                if (!IsAbilitySlotUnlocked(unit, def) || unit.AbilityCooldownRemaining[i] > 0f)
                {
                    continue;
                }

                if (TryCastAbility(unit, def, i))
                {
                    return true;
                }
            }

            return false;
        }

        public void ArmSlotCooldown(MatchUnitState unit, int slotIndex, float seconds)
        {
            if (unit.AbilityCooldownRemaining != null
                && slotIndex >= 0
                && slotIndex < unit.AbilityCooldownRemaining.Length)
            {
                unit.AbilityCooldownRemaining[slotIndex] = seconds * GetArcaneFocusFactor(unit);
            }
        }

        /// <summary>Arcane Focus (tower track 5): caster ability cooldown multiplier.</summary>
        float GetArcaneFocusFactor(MatchUnitState unit)
        {
            if (unit == null || unit.IsChampion || !TowerTrackRules.RoleMatches(4, unit.Role))
            {
                return 1f;
            }

            var level = GetTowerTrackLevel(unit.OwnerSlot, 4);
            return level > 0 ? TowerTrackRules.CasterCooldownFactorByLevel[level - 1] : 1f;
        }

        bool TryCastAbility(MatchUnitState unit, UnitAbilityDef def, int slotIndex)
        {
            if (def.Behaviour == null)
            {
                return false;
            }

            var ctx = new UnitAbilityContext(
                this,
                unit,
                def,
                slotIndex,
                GetMagicLevel(unit.OwnerSlot));
            if (!def.Behaviour.TryCast(in ctx))
            {
                return false;
            }

            ApplyAbilityAnimLock(unit, def);
            return true;
        }

        void ApplyAbilityAnimLock(MatchUnitState unit, UnitAbilityDef def)
        {
            if (def == null)
            {
                return;
            }

            var state = def.Fx.AnimState;
            var kind = AbilityAnimRules.ResolveAnim(def.AbilityId, def.Fx.AnimKind, state);

            var lockSeconds = AbilityAnimRules.ResolveLockSeconds(
                kind,
                GetUnitAttackInterval(unit),
                def.AbilityId);
            // Active abilities with no authored cast/attack clip (AbilityAnimKind.None)
            // must still occupy the caster briefly. Without this, the next kit ability
            // re-casts on the very next tick and spells fire simultaneously instead of
            // strictly in sequence. Matches the canonical staff cast clip length.
            if (lockSeconds <= 0f)
            {
                lockSeconds = AbilityAnimRules.StaffCastClipSeconds;
            }

            unit.CastLockRemainingSeconds = lockSeconds;
            unit.CastLockUsesAttackAnim = kind == AbilityAnimKind.Attack;
            unit.CastLockAnimState = state;
            unit.CastLockAnimVariant = string.IsNullOrEmpty(state) ? 0 : def.Fx.AnimVariant;
            unit.BehaviorState = unit.CastLockUsesAttackAnim
                ? UnitBehaviorState.Attack
                : UnitBehaviorState.Cast;
            if (kind == AbilityAnimKind.Attack)
            {
                unit.AttackSwingSerial++;
            }
        }

        public void EmitCast(AbilityCastEvent cast)
        {
            var serial = ++_spellCastSerial;
            _networkAbilityCasts.Add(cast.WithSerial(serial));
            _presenterAbilityCasts.Add(cast.WithSerial(serial));
            AbilityCast?.Invoke(cast.WithSerial(serial));
        }

        void EmitTraitFx(
            MatchUnitState unit,
            int abilityId,
            Vector3 position,
            int targetUnitId = 0,
            float radius = 0f)
        {
            if (unit == null)
            {
                return;
            }

            var def = FindUnitAbility(unit, abilityId);
            if (def == null)
            {
                return;
            }

            EmitCast(new AbilityCastEvent(
                unit.UnitId,
                unit.OwnerSlot,
                def,
                targetUnitId,
                position,
                radius > 0f ? radius : def.Radius));
        }

        void EmitTraitFxUnbound(int ownerSlot, int abilityId, Vector3 position)
        {
            var def = AbilityCatalog != null ? AbilityCatalog.Find(abilityId) : null;
            if (def == null)
            {
                var kit = AbilityKitDefaults.CreateFlyingBonus();
                def = kit is { Length: > 0 } ? kit[0] : null;
            }

            if (def == null)
            {
                return;
            }

            EmitCast(new AbilityCastEvent(0, ownerSlot, def, 0, position, def.Radius));
        }

        UnitAbilityDef FindUnitAbility(MatchUnitState unit, int abilityId)
        {
            if (unit?.Abilities != null)
            {
                for (var i = 0; i < unit.Abilities.Length; i++)
                {
                    var def = unit.Abilities[i];
                    if (def != null && def.AbilityId == abilityId)
                    {
                        return def;
                    }
                }
            }

            if (AbilityCatalog != null)
            {
                var fromCatalog = AbilityCatalog.Find(abilityId);
                if (fromCatalog != null)
                {
                    return fromCatalog;
                }
            }

            if (unit == null)
            {
                return null;
            }

            var kit = unit == null
                ? null
                : AbilityKitDefaults.CreateForSpawn(
                    GetPlayerRaceId(unit.OwnerSlot),
                    unit.Role,
                    unit.HeroSlot,
                    unit.BonusSlot);
            if (kit == null)
            {
                return null;
            }

            for (var i = 0; i < kit.Length; i++)
            {
                if (kit[i] != null && kit[i].AbilityId == abilityId)
                {
                    return kit[i];
                }
            }

            return null;
        }

        public MatchUnitState ResurrectUnit(CombatCorpseState corpse)
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
            AttachAbilities(revived);
            ApplySpawnUniqueModifiers(revived);
            _units.Add(revived);
            _unitById[revived.UnitId] = revived;
            _corpses.Remove(corpse);
            return revived;
        }

        public MatchUnitState SummonMinion(int ownerSlot, MatchUnitState anchor, UnitRole role, float statScale)
        {
            if (anchor == null || statScale <= 0f)
            {
                return null;
            }

            var source = FindMinionSourceStats(ownerSlot, role, anchor);
            var stats = new UnitCombatStats(
                role,
                source.MaxHp * statScale,
                source.Armor * statScale,
                source.DamageMin * statScale,
                source.DamageMax * statScale,
                source.AttackSpeed,
                source.AttackRange,
                source.MoveSpeed,
                goldBounty: 0,
                maxMana: 0f);

            try
            {
                return SpawnUnit(
                    ownerSlot,
                    anchor.LaneId,
                    role,
                    stats,
                    anchor.MarchProgressDistance,
                    new Vector3(1.2f, 0f, 0f));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public void ConsumeCorpse(CombatCorpseState corpse)
        {
            if (corpse != null)
            {
                _corpses.Remove(corpse);
            }
        }

        /// <summary>
        /// Minion baseline: a living friendly unit of the same role (canon ×0.5 of the race Melee),
        /// falling back to the anchor stats when none is on the field.
        /// </summary>
        UnitCombatStats FindMinionSourceStats(int ownerSlot, UnitRole role, MatchUnitState anchor)
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit != null && unit.IsAlive && unit.OwnerSlot == ownerSlot && unit.Role == role)
                {
                    return unit.Stats;
                }
            }

            return anchor.Stats;
        }

        public bool HasActiveHealZone(int casterUnitId)
        {
            for (var i = 0; i < _healZones.Count; i++)
            {
                if (_healZones[i].CasterUnitId == casterUnitId
                    && _healZones[i].RemainingSeconds > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        public void ReplaceHealZone(HeroHealZoneState zone)
        {
            for (var i = _healZones.Count - 1; i >= 0; i--)
            {
                if (_healZones[i].CasterUnitId == zone.CasterUnitId)
                {
                    _healZones.RemoveAt(i);
                }
            }

            _healZones.Add(zone);
        }

        void TickHealZones(float deltaTime)
        {
            for (var i = _healZones.Count - 1; i >= 0; i--)
            {
                var zone = _healZones[i];
                zone.RemainingSeconds -= deltaTime;
                if (zone.RemainingSeconds <= 0f)
                {
                    _healZones.RemoveAt(i);
                    continue;
                }

                if (zone.FollowUnitId != 0)
                {
                    var bearer = GetUnitById(zone.FollowUnitId);
                    if (bearer == null || !bearer.IsAlive)
                    {
                        _healZones.RemoveAt(i);
                        continue;
                    }

                    zone.Center = bearer.WorldPosition;
                }

                var heal = zone.HealPerSecond * deltaTime;
                // FACELESS-012: Feast Zone heals allies on damage dealt (handled in ApplyDamage),
                // so it carries no flat per-tick heal of its own.
                if (heal <= 0f || zone.HealFractionOfDamageDealt > 0f)
                {
                    continue;
                }

                var allies = HeroAbilityRules.GatherAlliesAround(
                    zone.OwnerSlot,
                    zone.Center,
                    _units,
                    zone.Radius);
                for (var a = 0; a < allies.Count; a++)
                {
                    allies[a].CurrentHp = HeroAbilityRules.ApplyHeal(
                        allies[a].CurrentHp,
                        GetEffectiveMaxHp(allies[a]),
                        heal);
                }
            }
        }

        /// <summary>
        /// Applies HP/s regeneration from passive HpRegen auras: every living owner unit inside the
        /// bearer's radius regenerates (the bearer heals itself too, distance 0).
        /// </summary>
        void TickAuraRegen(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var regen = GetAuraPercent(unit.OwnerSlot, AuraStat.HpRegen, unit.WorldPosition);
                if (regen <= 0f)
                {
                    continue;
                }

                unit.CurrentHp = HeroAbilityRules.ApplyHeal(
                    unit.CurrentHp,
                    GetEffectiveMaxHp(unit),
                    regen * deltaTime);
            }
        }

        void ClampUnitsToEffectiveMaxHp()
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var maxHp = GetEffectiveMaxHp(unit);
                if (unit.CurrentHp > maxHp)
                {
                    unit.CurrentHp = maxHp;
                }
            }
        }

        public MatchUnitState FindMostInjuredAlly(IReadOnlyList<MatchUnitState> allies)
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
                var maxHp = GetEffectiveMaxHp(candidate);
                if (candidate.CurrentHp >= maxHp - 0.001f)
                {
                    continue;
                }

                var fraction = maxHp > 0f ? candidate.CurrentHp / maxHp : 1f;
                if (fraction < bestFraction)
                {
                    bestFraction = fraction;
                    best = candidate;
                }
            }

            return best;
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
