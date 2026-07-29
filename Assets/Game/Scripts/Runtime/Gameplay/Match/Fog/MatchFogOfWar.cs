using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match.Fog
{
    /// <summary>
    /// Local-client fog of war: permanent base/road halves + dynamic clear around living own units.
    /// No networking — presentation only.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class MatchFogOfWar : MonoBehaviour
    {
        [SerializeField] private float _visionRadius = 25f;
        [SerializeField] private FogSimulation _simulation;
        [SerializeField] private bool _fogEnabled = true;

        MatchRuntime _runtime;
        FogPermanentZones _permanent;
        int _localPlayerSlot;
        readonly List<Vector3> _localUnitPositions = new();
        bool _initialized;

        public float VisionRadius => _visionRadius;
        public FogPermanentZones PermanentZones => _permanent;
        public bool FogDisabled => !_fogEnabled || IsLocalPlayerEliminated();
        public bool IsInitialized => _initialized;

        public void Configure(MatchRuntime runtime, int localPlayerSlot)
        {
            _runtime = runtime;
            _localPlayerSlot = localPlayerSlot;
            _initialized = false;
            _visionRadius = 25f;

            var controller = runtime != null ? runtime.Controller : null;
            if (controller?.Layout == null || controller.Graph == null)
            {
                return;
            }

            var centerArenaRadius = controller.Graph.CenterArenaRadius > 0.01f
                ? controller.Graph.CenterArenaRadius
                : LaneGraphBuilder.DefaultCenterArenaRadius;

            _permanent = FogPermanentMaskBaker.Bake(
                controller.Layout,
                controller.Graph,
                localPlayerSlot,
                centerArenaRadius);

            EnsureSimulation();
            _simulation.Initialize(
                controller.Layout.ArenaRadius,
                _permanent,
                _visionRadius);
            _initialized = true;
            RefreshEnabledState();
        }

        void Update()
        {
            if (!_initialized || _runtime == null || !_runtime.IsMatchStarted)
            {
                return;
            }

            RefreshEnabledState();
            CollectLocalUnitPositions();
            if (FogDisabled)
            {
                _simulation?.SetActiveVisual(false);
                return;
            }

            _simulation?.SetActiveVisual(true);
            _simulation?.Tick(_localUnitPositions, Time.deltaTime);
        }

        public bool IsRevealed(Vector3 worldPosition)
        {
            if (!_initialized)
            {
                return true;
            }

            return FogVisionRules.IsRevealed(
                _permanent,
                _localUnitPositions,
                _visionRadius,
                worldPosition,
                FogDisabled);
        }

        public bool CanSelectHostileAt(int targetOwnerSlot, Vector3 worldPosition)
        {
            return FogVisionRules.CanSelectTarget(
                _localPlayerSlot,
                targetOwnerSlot,
                worldPosition,
                _permanent,
                _localUnitPositions,
                _visionRadius,
                FogDisabled);
        }

        void CollectLocalUnitPositions()
        {
            _localUnitPositions.Clear();
            var combat = _runtime.Controller?.Combat;
            if (combat?.Units == null)
            {
                return;
            }

            foreach (var unit in combat.Units)
            {
                if (!unit.IsAlive || unit.OwnerSlot != _localPlayerSlot)
                {
                    continue;
                }

                if (combat.TryGetUnitWorldPosition(unit, out var position))
                {
                    _localUnitPositions.Add(position);
                }
            }
        }

        bool IsLocalPlayerEliminated()
        {
            var players = _runtime?.Controller?.Players;
            if (players == null || _localPlayerSlot < 0 || _localPlayerSlot >= players.Count)
            {
                return false;
            }

            return players[_localPlayerSlot].IsEliminated;
        }

        void RefreshEnabledState()
        {
            if (FogDisabled)
            {
                _simulation?.SetActiveVisual(false);
            }
        }

        void EnsureSimulation()
        {
            if (_simulation == null)
            {
                _simulation = GetComponent<FogSimulation>();
            }

            if (_simulation == null)
            {
                _simulation = gameObject.AddComponent<FogSimulation>();
            }
        }
    }
}
