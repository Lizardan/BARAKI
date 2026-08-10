using Game.Core;
using Game.Gameplay.Match.Fog;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Owns pick registry, selection state, and click input for the active match.</summary>
    public sealed class MatchSelectionBridge : MonoBehaviour
    {
        readonly MatchPickRegistry _registry = new();
        readonly MatchSelection _selection = new();

        MatchSelectionInput _input;
        MatchRuntime _runtime;
        MatchFogOfWar _fogOfWar;
        int _localPlayerSlot;

        public MatchPickRegistry Registry => _registry;
        public MatchSelection Selection => _selection;

        public void BeginMatch()
        {
            _registry.Clear();
            _selection.Clear();
            _runtime = GetComponent<MatchRuntime>() ?? MatchRuntime.Current;
            _fogOfWar = GetComponent<MatchFogOfWar>() ?? MatchFogOfWar.Current;
            _localPlayerSlot = MatchNetworkSession.LocalSlot >= 0
                ? MatchNetworkSession.LocalSlot
                : (GameSession.ActiveSetup?.LocalPlayerSlot ?? 0);
            EnsureInput();
        }

        public void EndMatch()
        {
            _registry.Clear();
            _selection.Clear();
        }

        public void RegisterPickCollider(Collider collider, MatchPickTarget target)
        {
            _registry.Register(collider, target);
        }

        public void UnregisterPickCollider(Collider collider)
        {
            _registry.Unregister(collider);
        }

        void EnsureInput()
        {
            if (_input == null)
            {
                _input = GetComponent<MatchSelectionInput>();
                if (_input == null)
                {
                    _input = gameObject.AddComponent<MatchSelectionInput>();
                }
            }

            _input.Initialize(
                _registry,
                _selection,
                onRightClickTarget: OnRightClickTarget,
                canSelectTarget: CanSelectTarget);
        }

        bool CanSelectTarget(MatchPickTarget target)
        {
            if (!target.HasTarget || _fogOfWar == null || !_fogOfWar.IsInitialized || _fogOfWar.FogDisabled)
            {
                return true;
            }

            var controller = _runtime != null ? _runtime.Controller : null;
            if (controller == null)
            {
                return true;
            }

            if (target.IsBuilding)
            {
                var building = controller.Buildings.GetByInstanceId(target.EntityId);
                if (building == null)
                {
                    return false;
                }

                return _fogOfWar.CanSelectHostileAt(building.OwnerSlot, building.WorldPosition);
            }

            if (target.IsUnit)
            {
                foreach (var unit in controller.Combat.Units)
                {
                    if (unit.UnitId != target.EntityId)
                    {
                        continue;
                    }

                    if (!controller.Combat.TryGetUnitWorldPosition(unit, out var position))
                    {
                        return false;
                    }

                    return _fogOfWar.CanSelectHostileAt(unit.OwnerSlot, position);
                }

                return false;
            }

            return true;
        }

        void Update()
        {
            if (_selection == null || !_selection.Current.HasTarget || _fogOfWar == null)
            {
                return;
            }

            if (!CanSelectTarget(_selection.Current))
            {
                _selection.Clear();
            }
        }

        void OnRightClickTarget(MatchPickTarget target)
        {
            if (!target.IsUnit)
            {
                return;
            }

            if (!_selection.Current.IsBuilding)
            {
                return;
            }

            var controller = _runtime != null ? _runtime.Controller : null;
            if (controller == null)
            {
                return;
            }

            var buildingInstanceId = _selection.Current.EntityId;
            var building = controller.Buildings.GetByInstanceId(buildingInstanceId);
            if (building == null
                || building.OwnerSlot != _localPlayerSlot
                || !BuildingRules.IsDefensiveBuilding(building.BuildingId)
                || !building.IsIntact)
            {
                return;
            }

            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestSetTowerTarget(buildingInstanceId, target.EntityId);
                return;
            }

            controller.TrySetTowerTarget(_localPlayerSlot, buildingInstanceId, target.EntityId);
        }
    }
}
