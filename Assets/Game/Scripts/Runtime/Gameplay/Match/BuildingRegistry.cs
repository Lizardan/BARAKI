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

        public BuildingState FindSiegeTarget(
            int attackerOwnerSlot,
            string laneId,
            Vector3 attackerPosition,
            float attackRange)
        {
            BuildingState best = null;
            var bestDistanceSq = float.MaxValue;
            var rangeSq = attackRange * attackRange;

            foreach (var building in _buildings)
            {
                if (!building.IsIntact
                    || building.OwnerSlot == attackerOwnerSlot
                    || !BuildingRules.CanSiegeTarget(laneId, building.BuildingId))
                {
                    continue;
                }

                var delta = building.WorldPosition - attackerPosition;
                delta.y = 0f;
                var distanceSq = delta.sqrMagnitude;
                if (distanceSq > rangeSq || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                best = building;
            }

            return best;
        }
    }
}
