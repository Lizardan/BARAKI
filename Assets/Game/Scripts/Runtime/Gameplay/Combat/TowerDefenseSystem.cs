using System.Collections.Generic;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>Server-side defensive building auto/manual fire (towers, main, barracks).</summary>
    public sealed class TowerDefenseSystem
    {
        readonly Dictionary<int, int?> _manualTargetByBuildingInstance = new();
        readonly Dictionary<int, float> _cooldownByBuildingInstance = new();
        readonly Dictionary<int, float> _scanCooldownByBuildingInstance = new();
        readonly List<MatchUnitState> _candidates = new(16);
        readonly System.Random _random = new(4242);

        public void Reset()
        {
            _manualTargetByBuildingInstance.Clear();
            _cooldownByBuildingInstance.Clear();
            _scanCooldownByBuildingInstance.Clear();
        }

        public bool TrySetManualTarget(int buildingInstanceId, int? unitId)
        {
            _manualTargetByBuildingInstance[buildingInstanceId] = unitId;
            return true;
        }

        public int? GetManualTarget(int buildingInstanceId) =>
            _manualTargetByBuildingInstance.TryGetValue(buildingInstanceId, out var id) ? id : null;

        public void Tick(
            float deltaTime,
            BuildingRegistry buildings,
            MatchCombatSystem combat,
            IReadOnlyList<MatchPlayerState> players)
        {
            if (buildings == null || combat == null || deltaTime < 0f)
            {
                return;
            }

            foreach (var building in buildings.Buildings)
            {
                if (!BuildingRules.IsDefensiveBuilding(building.BuildingId) || !building.IsIntact)
                {
                    continue;
                }

                if (building.OwnerSlot < 0
                    || building.OwnerSlot >= players.Count
                    || players[building.OwnerSlot].IsEliminated)
                {
                    continue;
                }

                TickBuilding(building, deltaTime, combat);
            }
        }

        void TickBuilding(BuildingState building, float deltaTime, MatchCombatSystem combat)
        {
            if (_cooldownByBuildingInstance.TryGetValue(building.InstanceId, out var cooldown)
                && cooldown > 0f)
            {
                _cooldownByBuildingInstance[building.InstanceId] = cooldown - deltaTime;
            }

            if (!_scanCooldownByBuildingInstance.TryGetValue(building.InstanceId, out var scan)
                || scan <= 0f)
            {
                _scanCooldownByBuildingInstance[building.InstanceId] = TowerCombatRules.ScanInterval;
            }
            else
            {
                _scanCooldownByBuildingInstance[building.InstanceId] = scan - deltaTime;
            }

            var target = ResolveTarget(building, combat);
            if (target == null)
            {
                return;
            }

            if (_cooldownByBuildingInstance.TryGetValue(building.InstanceId, out var remaining)
                && remaining > 0f)
            {
                return;
            }

            var raw = CombatRules.RollDamage(
                TowerCombatRules.DamageMin,
                TowerCombatRules.DamageMax,
                _random);
            if (!combat.TryFireBuildingProjectile(
                    building.InstanceId,
                    building.OwnerSlot,
                    building.WorldPosition,
                    target.UnitId,
                    raw))
            {
                return;
            }

            _cooldownByBuildingInstance[building.InstanceId] = TowerCombatRules.GetAttackIntervalSeconds();
        }

        MatchUnitState ResolveTarget(BuildingState building, MatchCombatSystem combat)
        {
            var hadManual = _manualTargetByBuildingInstance.TryGetValue(building.InstanceId, out var manualId)
                            && manualId.HasValue;
            if (hadManual)
            {
                var manual = combat.GetUnit(manualId.Value);
                if (TowerCombatRules.CanTargetUnit(building.OwnerSlot, manual)
                    && TowerCombatRules.IsInRange(building.WorldPosition, manual.WorldPosition))
                {
                    return manual;
                }

                // Prior target died / left range — pick a random enemy and stick to it.
                _manualTargetByBuildingInstance[building.InstanceId] = null;
                var random = PickRandomInRange(building, combat);
                if (random != null)
                {
                    _manualTargetByBuildingInstance[building.InstanceId] = random.UnitId;
                }

                return random;
            }

            return PickNearestInRange(building, combat);
        }

        MatchUnitState PickNearestInRange(BuildingState building, MatchCombatSystem combat)
        {
            MatchUnitState best = null;
            var bestDist = float.MaxValue;
            foreach (var unit in combat.Units)
            {
                if (!TowerCombatRules.CanTargetUnit(building.OwnerSlot, unit))
                {
                    continue;
                }

                var dist = TowerCombatRules.HorizontalDistance(building.WorldPosition, unit.WorldPosition);
                if (dist > TowerCombatRules.Range || dist >= bestDist)
                {
                    continue;
                }

                best = unit;
                bestDist = dist;
            }

            return best;
        }

        MatchUnitState PickRandomInRange(BuildingState building, MatchCombatSystem combat)
        {
            _candidates.Clear();
            foreach (var unit in combat.Units)
            {
                if (!TowerCombatRules.CanTargetUnit(building.OwnerSlot, unit))
                {
                    continue;
                }

                if (!TowerCombatRules.IsInRange(building.WorldPosition, unit.WorldPosition))
                {
                    continue;
                }

                _candidates.Add(unit);
            }

            if (_candidates.Count == 0)
            {
                return null;
            }

            return _candidates[_random.Next(_candidates.Count)];
        }
    }
}
