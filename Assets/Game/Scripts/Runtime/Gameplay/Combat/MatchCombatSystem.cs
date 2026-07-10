using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public sealed class MatchCombatSystem
    {
        readonly List<MatchUnitState> _units = new();
        readonly List<MatchPlayerState> _players = new();
        readonly List<CombatProjectileState> _projectiles = new();
        readonly List<CombatMeleeStrikeState> _meleeStrikes = new();

        LaneGraph _graph;
        LaneRouteRegistry _routes;
        BuildingRegistry _buildings;
        int _nextUnitId = 1;
        int _nextProjectileId = 1;
        System.Random _random;

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
            _players.Clear();
            _projectiles.Clear();
            _meleeStrikes.Clear();
            _players.AddRange(players);
            _graph = graph;
            _routes = LaneRouteRegistry.Build(graph);
            _nextUnitId = 1;
            _nextProjectileId = 1;
            _random = new System.Random(randomSeed);
        }

        public void SetBuildings(BuildingRegistry buildings) => _buildings = buildings;

        public void DespawnUnitsForOwner(int ownerSlot)
        {
            for (var i = _units.Count - 1; i >= 0; i--)
            {
                if (_units[i].OwnerSlot == ownerSlot)
                {
                    _units.RemoveAt(i);
                }
            }

            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                if (_projectiles[i].AttackerOwnerSlot == ownerSlot)
                {
                    _projectiles.RemoveAt(i);
                }
            }

            for (var i = _meleeStrikes.Count - 1; i >= 0; i--)
            {
                var attacker = GetUnitById(_meleeStrikes[i].AttackerUnitId);
                if (attacker != null && attacker.OwnerSlot == ownerSlot)
                {
                    _meleeStrikes.RemoveAt(i);
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
                    spawnUnitIndex++);
                var worldPosition = route.ResolveSpawnPosition(spawnDistance, formationOffset);
                var progressDistance = Mathf.Clamp(
                    route.ProjectDistanceForward(worldPosition, spawnDistance),
                    spawnDistance - 0.5f,
                    spawnDistance + 1.25f);
                var unit = new MatchUnitState(
                    _nextUnitId++,
                    wave.OwnerSlot,
                    wave.LaneId,
                    slot.Role,
                    stats,
                    stats.MaxHp,
                    worldPosition,
                    route.FindMarchWaypointIndex(worldPosition),
                    unitMarchSpeed,
                    spawnDistance);
                unit.MarchProgressDistance = progressDistance;
                _units.Add(unit);
            }
        }

        /// <summary>Explicit spawn for tests and scripted scenarios.</summary>
        public MatchUnitState SpawnUnit(
            int ownerSlot,
            string laneId,
            UnitRole role,
            UnitCombatStats stats,
            float distanceAlongLane = 0f,
            Vector3 formationOffset = default)
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
                marchSpawnDistance: distanceAlongLane);
            unit.MarchProgressDistance = progressDistance;
            _units.Add(unit);
            return unit;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (_units.Count == 0 && _projectiles.Count == 0 && _meleeStrikes.Count == 0)
            {
                return;
            }

            var snapshot = new List<MatchUnitState>(_units);
            snapshot.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
            foreach (var unit in snapshot)
            {
                if (unit.IsAlive)
                {
                    TickUnit(unit, deltaTime);
                }
            }

            TickProjectiles(deltaTime);
            TickMeleeStrikes(deltaTime);
        }

        void TickUnit(MatchUnitState unit, float deltaTime)
        {
            if (!_routes.TryGetRoute(unit.OwnerSlot, unit.LaneId, out var route))
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
                if (unit.Role == UnitRole.Siege && _buildings != null)
                {
                    var building = _buildings.FindSiegeTarget(
                        unit.OwnerSlot,
                        unit.LaneId,
                        unit.WorldPosition,
                        CombatRules.GetAggroRadius(unit.Stats));
                    if (building != null)
                    {
                        unit.CurrentTargetBuildingInstanceId = building.InstanceId;
                    }
                }
                else
                {
                    var scanned = ScanForEnemy(unit);
                    if (scanned != null)
                    {
                        unit.CurrentTargetId = scanned.UnitId;
                    }
                }
            }

            var buildingTarget = GetBuildingByInstanceId(unit.CurrentTargetBuildingInstanceId);
            if (buildingTarget != null && unit.Role == UnitRole.Siege)
            {
                var buildingDistance = HorizontalDistance(unit.WorldPosition, buildingTarget.WorldPosition);
                if (buildingDistance <= unit.Stats.AttackRange)
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
            unit.WorldPosition = UnitLocomotionRules.MoveTowards(
                unit.WorldPosition,
                destination,
                maxStep,
                allies,
                out _,
                unit.UnitId);
            unit.WorldPosition = UnitLocomotionRules.ClampToLaneDrift(
                route,
                unit.WorldPosition,
                UnitLocomotionRules.MaxMarchDriftFromLane,
                unit.MarchProgressDistance);
            unit.MarchProgressDistance = Mathf.Max(
                unit.MarchProgressDistance,
                route.ProjectDistanceForward(unit.WorldPosition, unit.MarchProgressDistance));
            var marchFacing = route.EvaluateDirectionAtDistance(
                Mathf.Min(route.TotalLength, unit.MarchProgressDistance + 0.75f));
            marchFacing.y = 0f;
            unit.FacingDirection = marchFacing.sqrMagnitude > 0.0001f
                ? marchFacing.normalized
                : unit.FacingDirection;
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
            unit.WorldPosition = UnitLocomotionRules.MoveTowards(
                unit.WorldPosition,
                target.WorldPosition,
                maxStep,
                allies,
                out var facing,
                unit.UnitId);
            unit.FacingDirection = facing;
            unit.WorldPosition = UnitLocomotionRules.ClampToLaneDrift(
                route,
                unit.WorldPosition,
                UnitLocomotionRules.MaxCombatDriftFromLane);
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

            UpdateFacingTowards(unit, target.WorldPosition);

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
                if (unit.BehaviorState is UnitBehaviorState.Chase or UnitBehaviorState.Attack)
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

        MatchUnitState ScanForEnemy(MatchUnitState unit)
        {
            var myPosition = unit.WorldPosition;
            var aggroRadius = CombatRules.GetAggroRadius(unit.Stats);
            var aggroRadiusSq = aggroRadius * aggroRadius;
            MatchUnitState best = null;
            var bestScore = float.MaxValue;

            foreach (var other in _units)
            {
                if (!CombatLaneRules.CanEngage(unit, other, _graph))
                {
                    continue;
                }

                if (!CombatRules.CanAttackTarget(unit.Role, other.Role))
                {
                    continue;
                }

                var distanceSq = HorizontalDistanceSq(myPosition, other.WorldPosition);
                if (distanceSq > aggroRadiusSq)
                {
                    continue;
                }

                var score = distanceSq;
                if (IsEngagedByAlly(other, unit.OwnerSlot))
                {
                    score *= 0.5f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }

            return best;
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
                || building.OwnerSlot == unit.OwnerSlot
                || unit.Role != UnitRole.Siege
                || !BuildingRules.CanSiegeTarget(unit.LaneId, building.BuildingId))
            {
                unit.CurrentTargetBuildingInstanceId = null;
                return;
            }

            if (HorizontalDistance(unit.WorldPosition, building.WorldPosition)
                > CombatRules.GetAggroRadius(unit.Stats))
            {
                unit.CurrentTargetBuildingInstanceId = null;
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
            unit.WorldPosition = UnitLocomotionRules.MoveTowards(
                unit.WorldPosition,
                building.WorldPosition,
                maxStep,
                allies,
                out var facing,
                unit.UnitId);
            unit.FacingDirection = facing;
            unit.WorldPosition = UnitLocomotionRules.ClampToLaneDrift(
                route,
                unit.WorldPosition,
                UnitLocomotionRules.MaxCombatDriftFromLane);
        }

        void TickBuildingAttack(MatchUnitState unit, BuildingState building, float deltaTime)
        {
            if (building == null || building.IsRuins)
            {
                unit.CurrentTargetBuildingInstanceId = null;
                return;
            }

            var distance = HorizontalDistance(unit.WorldPosition, building.WorldPosition);
            if (distance > unit.Stats.AttackRange)
            {
                unit.BehaviorState = UnitBehaviorState.Chase;
                return;
            }

            UpdateFacingTowards(unit, building.WorldPosition);

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

            var rawDamage = CombatRules.RollDamage(
                attacker.Stats.DamageMin,
                attacker.Stats.DamageMax,
                _random);

            if (!CombatAttackRules.UsesProjectile(attacker.Role))
            {
                _buildings.TryApplyDamage(building.InstanceId, rawDamage, attacker.OwnerSlot);
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
            if (unitId == null)
            {
                return null;
            }

            foreach (var unit in _units)
            {
                if (unit.UnitId == unitId.Value)
                {
                    return unit;
                }
            }

            return null;
        }

        List<MatchUnitState> CollectMarchAlliesForAvoidance(MatchUnitState unit, LaneRoute route)
        {
            var allies = new List<MatchUnitState>();
            var queryRadius = UnitLocomotionRules.AvoidanceRadius * 3f;
            var radiusSq = queryRadius * queryRadius;
            var myPosition = unit.WorldPosition;
            var myDistance = unit.MarchProgressDistance;
            var forward = route.EvaluateDirectionAtDistance(myDistance);
            var aheadGapMax = CombatFormationRules.MinLaneFollowGap * 1.5f;

            foreach (var other in _units)
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

                if (HorizontalDistanceSq(myPosition, other.WorldPosition) > radiusSq)
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
            var allies = new List<MatchUnitState>();
            var queryRadius = UnitLocomotionRules.AvoidanceRadius * 3f;
            var radiusSq = queryRadius * queryRadius;
            var myPosition = unit.WorldPosition;

            foreach (var other in _units)
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

            foreach (var other in _units)
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
            var enemyPosition = enemy.WorldPosition;
            foreach (var ally in _units)
            {
                if (!ally.IsAlive || ally.OwnerSlot != ownerSlot || ally.UnitId == enemy.UnitId)
                {
                    continue;
                }

                if (HorizontalDistance(ally.WorldPosition, enemyPosition) <= ally.Stats.AttackRange * 1.25f)
                {
                    return true;
                }
            }

            return false;
        }

        void UpdateFacingTowards(MatchUnitState unit, Vector3 targetPosition)
        {
            var direction = targetPosition - unit.WorldPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                unit.FacingDirection = direction.normalized;
            }
        }

        void BeginAttack(MatchUnitState attacker, MatchUnitState target)
        {
            if (attacker == null || target == null || !target.IsAlive)
            {
                return;
            }

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

            var snapshot = new List<CombatProjectileState>(_projectiles);
            foreach (var projectile in snapshot)
            {
                projectile.Elapsed += deltaTime;
                if (projectile.Elapsed < projectile.FlightDuration)
                {
                    continue;
                }

                _projectiles.Remove(projectile);
                if (projectile.TargetBuildingInstanceId.HasValue)
                {
                    _buildings?.TryApplyDamage(
                        projectile.TargetBuildingInstanceId.Value,
                        projectile.RawDamage,
                        projectile.AttackerOwnerSlot);
                    continue;
                }

                var attacker = GetUnitById(projectile.AttackerUnitId);
                var target = GetUnitById(projectile.TargetUnitId);
                if (target != null && target.IsAlive)
                {
                    ApplyDamage(attacker, target, projectile.RawDamage, projectile.AttackerOwnerSlot);
                }
            }
        }

        void TickMeleeStrikes(float deltaTime)
        {
            if (_meleeStrikes.Count == 0)
            {
                return;
            }

            var snapshot = new List<CombatMeleeStrikeState>(_meleeStrikes);
            foreach (var strike in snapshot)
            {
                strike.TimeRemaining -= deltaTime;
                if (strike.TimeRemaining > 0f)
                {
                    continue;
                }

                _meleeStrikes.Remove(strike);
                var attacker = GetUnitById(strike.AttackerUnitId);
                var target = GetUnitById(strike.TargetUnitId);
                if (target != null && target.IsAlive)
                {
                    var killerSlot = attacker?.OwnerSlot ?? GetUnitOwnerSlot(strike.AttackerUnitId);
                    ApplyDamage(attacker, target, strike.RawDamage, killerSlot);
                }
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

            var bounty = CombatRules.ComputeKillBounty(target.Stats.GoldBounty);
            GrantGold(killerOwnerSlot, bounty);
            _units.Remove(target);
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
