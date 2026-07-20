using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public sealed class BuildingRegistry
    {
        private readonly List<BuildingState> _buildings = new();
        private int _nextInstanceId = 1;

        public event Action<BuildingDestroyedEvent> BuildingDestroyed;

        public IReadOnlyList<BuildingState> Buildings => _buildings;

        public void Initialize(MatchArenaLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            _buildings.Clear();
            _nextInstanceId = 1;

            for (var slot = 0; slot < layout.Slots.Count; slot++)
            {
                foreach (var buildingId in BuildingRules.EliminationBuildingIds)
                {
                    var worldPosition = layout.Slots[slot].GetBuildingWorldPosition(buildingId);
                    _buildings.Add(new BuildingState(
                        _nextInstanceId++,
                        slot,
                        buildingId,
                        worldPosition,
                        BuildingRules.GetMaxHp(buildingId),
                        BuildingRules.GetArmor(buildingId)));
                }
            }
        }

        public BuildingState GetByInstanceId(int instanceId)
        {
            foreach (var building in _buildings)
            {
                if (building.InstanceId == instanceId)
                {
                    return building;
                }
            }

            return null;
        }

        public bool TryApplyDamage(int buildingInstanceId, float rawDamage, int attackerOwnerSlot)
        {
            var building = GetByInstanceId(buildingInstanceId);
            if (building == null || building.IsRuins)
            {
                return false;
            }

            var damage = CombatRules.ApplyArmor(rawDamage, building.Armor);
            var becameRuins = building.ApplyDamage(damage);
            if (becameRuins)
            {
                BuildingDestroyed?.Invoke(new BuildingDestroyedEvent(
                    building.OwnerSlot,
                    building.BuildingId,
                    building.InstanceId,
                    attackerOwnerSlot));
            }

            return becameRuins;
        }

        public bool AreAllBuildingsRuined(int ownerSlot)
        {
            var found = false;
            foreach (var building in _buildings)
            {
                if (building.OwnerSlot != ownerSlot)
                {
                    continue;
                }

                found = true;
                if (building.IsIntact)
                {
                    return false;
                }
            }

            return found;
        }

        public int CountIntactBuildings(int ownerSlot)
        {
            var count = 0;
            foreach (var building in _buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.IsIntact)
                {
                    count++;
                }
            }

            return count;
        }

        public BuildingState FindBuildingTarget(
            int attackerOwnerSlot,
            string laneId,
            Vector3 attackerPosition,
            float aggroRadius,
            LaneGraph graph)
        {
            BuildingState best = null;
            var bestScore = float.MaxValue;

            foreach (var building in _buildings)
            {
                if (!BuildingRules.CanLaneAttackBuilding(attackerOwnerSlot, laneId, building, graph))
                {
                    continue;
                }

                var delta = building.WorldPosition - attackerPosition;
                delta.y = 0f;
                var centerDistance = delta.magnitude;
                var surfaceDistance = BuildingRules.GetSurfaceDistance(centerDistance, building.BuildingId);
                if (surfaceDistance > aggroRadius || surfaceDistance >= bestScore)
                {
                    continue;
                }

                bestScore = surfaceDistance;
                best = building;
            }

            return best;
        }

        /// <summary>
        /// Nearest intact enemy building with no aggro cap (end-of-lane push toward remaining base).
        /// </summary>
        public BuildingState FindNearestEnemyBuilding(
            int attackerOwnerSlot,
            string laneId,
            Vector3 attackerPosition,
            LaneGraph graph)
        {
            return FindBuildingTarget(
                attackerOwnerSlot,
                laneId,
                attackerPosition,
                aggroRadius: float.MaxValue,
                graph);
        }

        /// <summary>Legacy alias for <see cref="FindBuildingTarget"/>.</summary>
        public BuildingState FindSiegeTarget(
            int attackerOwnerSlot,
            string laneId,
            Vector3 attackerPosition,
            float attackRange) =>
            FindBuildingTarget(attackerOwnerSlot, laneId, attackerPosition, attackRange, graph: null);
    }
}
