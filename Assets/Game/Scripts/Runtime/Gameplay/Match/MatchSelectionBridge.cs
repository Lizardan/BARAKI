using System;
using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Match.Fog;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Match
{
    /// <summary>Owns pick registry, selection state, and click input for the active match.</summary>
    public sealed class MatchSelectionBridge : MonoBehaviour
    {
        public static MatchSelectionBridge Current { get; private set; }

        readonly MatchPickRegistry _registry = new();
        readonly MatchSelection _selection = new();

        MatchSelectionInput _input;
        MatchRuntime _runtime;
        MatchFogOfWar _fogOfWar;
        int _localPlayerSlot;
        bool _pendingMainExtraCast;
        MatchPickTarget _hoverTarget = MatchPickTarget.None;
        int _pendingBuildingAbilityId = BuildingAbilityRules.None;
        Vector3 _aimGroundPoint;
        bool _hasAimGroundPoint;
        bool _aimInCastRange;

        public MatchPickRegistry Registry => _registry;
        public MatchSelection Selection => _selection;
        public bool IsMainExtraCastPending => _pendingMainExtraCast;
        /// <summary>Valid hover target while aiming a main extra ability (enemy building/unit).</summary>
        public MatchPickTarget HoverTarget => _hoverTarget;
        /// <summary>True while aiming an always-available main building ability (MAIN-001).</summary>
        public bool IsBuildingAbilityPending =>
            _pendingBuildingAbilityId != BuildingAbilityRules.None;
        public int PendingBuildingAbilityId => _pendingBuildingAbilityId;
        public bool IsAimInCastRange => _aimInCastRange;

        /// <summary>Ground point under the pointer while aiming a building ability.</summary>
        public bool TryGetAimGroundPoint(out Vector3 point)
        {
            point = _aimGroundPoint;
            return _hasAimGroundPoint;
        }

        public event Action TargetingChanged;

        public void BeginMatch()
        {
            Current = this;
            _registry.Clear();
            _selection.Clear();
            SetPendingMainExtraCast(false);
            SetPendingBuildingAbility(BuildingAbilityRules.None);
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
            SetPendingMainExtraCast(false);
            SetPendingBuildingAbility(BuildingAbilityRules.None);
        }

        public void BeginMainExtraAbilityTargeting()
        {
            SetPendingBuildingAbility(BuildingAbilityRules.None);
            SetPendingMainExtraCast(true);
        }

        public void CancelMainExtraAbilityTargeting()
        {
            SetPendingMainExtraCast(false);
        }

        /// <summary>Start ground-point aiming for a main building ability (MAIN-001).</summary>
        public void BeginBuildingAbilityTargeting(int abilityId)
        {
            if (!BuildingAbilityRules.IsValidId(abilityId))
            {
                return;
            }

            SetPendingMainExtraCast(false);
            SetPendingBuildingAbility(abilityId);
        }

        public void CancelBuildingAbilityTargeting()
        {
            SetPendingBuildingAbility(BuildingAbilityRules.None);
        }

        public void RegisterPickCollider(Collider collider, MatchPickTarget target)
        {
            _registry.Register(collider, target);
        }

        public void UnregisterPickCollider(Collider collider)
        {
            _registry.Unregister(collider);
        }

        void SetPendingMainExtraCast(bool pending)
        {
            if (_pendingMainExtraCast == pending)
            {
                if (!pending)
                {
                    _hoverTarget = MatchPickTarget.None;
                }

                return;
            }

            _pendingMainExtraCast = pending;
            _hoverTarget = MatchPickTarget.None;
            if (pending)
            {
                MainExtraAbilityCursor.Apply();
            }
            else
            {
                MainExtraAbilityCursor.Clear();
            }

            TargetingChanged?.Invoke();
        }

        void SetPendingBuildingAbility(int abilityId)
        {
            if (_pendingBuildingAbilityId == abilityId)
            {
                return;
            }

            _pendingBuildingAbilityId = abilityId;
            _hasAimGroundPoint = false;
            _aimInCastRange = false;
            if (abilityId != BuildingAbilityRules.None)
            {
                BuildingAbilityCursor.Apply(inRange: true);
            }
            else
            {
                BuildingAbilityCursor.Clear();
            }

            TargetingChanged?.Invoke();
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
                canSelectTarget: CanSelectTarget,
                onLeftClickTarget: OnLeftClickTarget);
        }

        bool OnLeftClickTarget(MatchPickTarget target)
        {
            if (IsBuildingAbilityPending)
            {
                return HandleBuildingAbilityClick();
            }

            if (!_pendingMainExtraCast)
            {
                return false;
            }

            if (!target.HasTarget)
            {
                CancelMainExtraAbilityTargeting();
                return true;
            }

            var controller = _runtime != null ? _runtime.Controller : null;
            var player = controller != null
                && _localPlayerSlot >= 0
                && _localPlayerSlot < controller.Players.Count
                    ? controller.Players[_localPlayerSlot]
                    : null;
            if (player == null || !MainExtraAbilityRules.CanCast(player))
            {
                CancelMainExtraAbilityTargeting();
                return true;
            }

            if (!IsValidMainExtraTarget(target, player.MainExtraAbilityId))
            {
                return true;
            }

            var kind = MainExtraAbilityRules.GetTargetKind(player.MainExtraAbilityId);
            var buildingId = kind == MainExtraAbilityTargetKind.EnemyBuilding ? target.EntityId : 0;
            var unitId = kind == MainExtraAbilityTargetKind.EnemyUnit ? target.EntityId : 0;

            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestCastMainExtraAbility(buildingId, unitId);
            }
            else
            {
                controller.TryCastMainExtraAbility(_localPlayerSlot, buildingId, unitId);
            }

            CancelMainExtraAbilityTargeting();
            return true;
        }

        bool HandleBuildingAbilityClick()
        {
            // Out of range / no ground point: swallow the click, aiming continues.
            if (!_hasAimGroundPoint || !_aimInCastRange)
            {
                return true;
            }

            var controller = _runtime != null ? _runtime.Controller : null;
            if (controller == null)
            {
                CancelBuildingAbilityTargeting();
                return true;
            }

            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestCastBuildingAbility(_pendingBuildingAbilityId, _aimGroundPoint);
            }
            else
            {
                controller.TryCastBuildingAbility(
                    _localPlayerSlot,
                    _pendingBuildingAbilityId,
                    _aimGroundPoint);
            }

            CancelBuildingAbilityTargeting();
            return true;
        }

        void UpdateAimGroundPoint()
        {
            _hasAimGroundPoint = TryResolvePointerGroundPoint(out _aimGroundPoint);
            var inRange = false;
            if (_hasAimGroundPoint)
            {
                var controller = _runtime != null ? _runtime.Controller : null;
                var layout = controller?.Layout;
                var basePosition = layout != null
                    && _localPlayerSlot >= 0
                    && _localPlayerSlot < layout.Slots.Count
                        ? layout.Slots[_localPlayerSlot].GetBuildingWorldPosition(GameIds.Buildings.Main)
                        : Vector3.zero;
                var barracksDistance =
                    BuildingAbilityRules.GetBaseToBarracksDistance(layout, _localPlayerSlot);
                inRange = BuildingAbilityRules.IsInCastRange(
                    basePosition,
                    _aimGroundPoint,
                    BuildingAbilityRules.GetIceRingCastRange(barracksDistance));
            }

            if (inRange != _aimInCastRange)
            {
                _aimInCastRange = inRange;
                BuildingAbilityCursor.Apply(inRange);
            }
        }

        bool TryResolvePointerGroundPoint(out Vector3 point)
        {
            point = default;
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            var camera = CameraCache.Main;
            if (camera == null)
            {
                return false;
            }

            return GameplayCameraGroundView.TryRayToGround(
                camera.ScreenPointToRay(mouse.position.ReadValue()),
                out point);
        }

        bool IsValidMainExtraTarget(MatchPickTarget target, int abilityId)
        {
            if (!target.HasTarget || !CanSelectTarget(target))
            {
                return false;
            }

            var controller = _runtime != null ? _runtime.Controller : null;
            if (controller == null)
            {
                return false;
            }

            var kind = MainExtraAbilityRules.GetTargetKind(abilityId);
            if (kind == MainExtraAbilityTargetKind.EnemyBuilding)
            {
                if (!target.IsBuilding)
                {
                    return false;
                }

                var building = controller.Buildings.GetByInstanceId(target.EntityId);
                return building != null
                       && building.IsIntact
                       && building.OwnerSlot != _localPlayerSlot;
            }

            if (kind == MainExtraAbilityTargetKind.EnemyUnit)
            {
                if (!target.IsUnit)
                {
                    return false;
                }

                var unit = controller.Combat.GetUnit(target.EntityId);
                return unit != null
                       && unit.IsAlive
                       && unit.OwnerSlot != _localPlayerSlot;
            }

            return false;
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
            if (_pendingMainExtraCast)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    CancelMainExtraAbilityTargeting();
                }

                var mouse = Mouse.current;
                if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                {
                    CancelMainExtraAbilityTargeting();
                }

                UpdateHoverTarget();
            }
            else if (_hoverTarget.HasTarget)
            {
                _hoverTarget = MatchPickTarget.None;
            }

            if (IsBuildingAbilityPending)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    CancelBuildingAbilityTargeting();
                }

                var mouse = Mouse.current;
                if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                {
                    CancelBuildingAbilityTargeting();
                }

                UpdateAimGroundPoint();
            }

            if (_selection == null || !_selection.Current.HasTarget || _fogOfWar == null)
            {
                return;
            }

            if (!CanSelectTarget(_selection.Current))
            {
                _selection.Clear();
            }
        }

        void UpdateHoverTarget()
        {
            var next = MatchPickTarget.None;
            var controller = _runtime != null ? _runtime.Controller : null;
            var player = controller != null
                && _localPlayerSlot >= 0
                && _localPlayerSlot < controller.Players.Count
                    ? controller.Players[_localPlayerSlot]
                    : null;

            if (player != null
                && MainExtraAbilityRules.CanCast(player)
                && _input != null
                && _input.TryPickUnderPointer(out var picked)
                && IsValidMainExtraTarget(picked, player.MainExtraAbilityId))
            {
                next = picked;
            }

            if (_hoverTarget.Kind == next.Kind && _hoverTarget.EntityId == next.EntityId)
            {
                return;
            }

            _hoverTarget = next;
            TargetingChanged?.Invoke();
        }

        void OnRightClickTarget(MatchPickTarget target)
        {
            if (_pendingMainExtraCast)
            {
                CancelMainExtraAbilityTargeting();
                return;
            }

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

        void OnDisable()
        {
            SetPendingMainExtraCast(false);
            SetPendingBuildingAbility(BuildingAbilityRules.None);
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
