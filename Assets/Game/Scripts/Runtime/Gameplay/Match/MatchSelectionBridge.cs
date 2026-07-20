using Game.Core;
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
        int _localPlayerSlot;

        public MatchPickRegistry Registry => _registry;
        public MatchSelection Selection => _selection;

        public void BeginMatch()
        {
            _registry.Clear();
            _selection.Clear();
            _runtime = GetComponent<MatchRuntime>() ?? FindAnyObjectByType<MatchRuntime>();
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

            _input.Initialize(_registry, _selection, onRightClickTarget: OnRightClickTarget);
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
