using System;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Match.Selection
{
    public sealed class MatchSelectionInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _maxRayDistance = 500f;

        MatchPickRegistry _registry;
        MatchSelection _selection;
        Func<bool> _isPointerOverUi;
        Action<MatchPickTarget> _onRightClickTarget;
        Func<MatchPickTarget, bool> _onLeftClickTarget;
        Func<MatchPickTarget, bool> _canSelectTarget;

        public void Initialize(
            MatchPickRegistry registry,
            MatchSelection selection,
            Func<bool> isPointerOverUi = null,
            Action<MatchPickTarget> onRightClickTarget = null,
            Func<MatchPickTarget, bool> canSelectTarget = null,
            Func<MatchPickTarget, bool> onLeftClickTarget = null)
        {
            _registry = registry;
            _selection = selection;
            if (isPointerOverUi != null)
            {
                _isPointerOverUi = isPointerOverUi;
            }

            if (onRightClickTarget != null)
            {
                _onRightClickTarget = onRightClickTarget;
            }

            if (canSelectTarget != null)
            {
                _canSelectTarget = canSelectTarget;
            }

            if (onLeftClickTarget != null)
            {
                _onLeftClickTarget = onLeftClickTarget;
            }
        }

        public void SetCanSelectFilter(Func<MatchPickTarget, bool> canSelectTarget) =>
            _canSelectTarget = canSelectTarget;

        public void SetUiBlocker(Func<bool> isPointerOverUi) => _isPointerOverUi = isPointerOverUi;

        public void SetRightClickHandler(Action<MatchPickTarget> onRightClickTarget) =>
            _onRightClickTarget = onRightClickTarget;

        public void SetLeftClickHandler(Func<MatchPickTarget, bool> onLeftClickTarget) =>
            _onLeftClickTarget = onLeftClickTarget;

        /// <summary>Raycast pick under the current mouse position (for targeting hover).</summary>
        public bool TryPickUnderPointer(out MatchPickTarget target)
        {
            target = MatchPickTarget.None;
            if (_registry == null)
            {
                return false;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            if (_isPointerOverUi != null && _isPointerOverUi())
            {
                return false;
            }

            if (_camera == null)
            {
                _camera = CameraCache.Main;
            }

            if (_camera == null)
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            return TrySelectClosestPickable(ray, out target);
        }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = CameraCache.Main;
            }
        }

        private void Update()
        {
            if (_registry == null || _selection == null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (_isPointerOverUi != null && _isPointerOverUi())
            {
                return;
            }

            if (_camera == null)
            {
                _camera = CameraCache.Main;
            }

            if (_camera == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
                if (!TrySelectClosestPickable(ray, out var target))
                {
                    if (_onLeftClickTarget != null && _onLeftClickTarget(MatchPickTarget.None))
                    {
                        return;
                    }

                    _selection.Clear();
                    return;
                }

                if (_onLeftClickTarget != null && _onLeftClickTarget(target))
                {
                    return;
                }

                _selection.Select(target);
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame && _onRightClickTarget != null)
            {
                var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
                if (TrySelectClosestPickable(ray, out var target) && target.HasTarget)
                {
                    _onRightClickTarget.Invoke(target);
                }
            }
        }

        bool TrySelectClosestPickable(Ray ray, out MatchPickTarget target)
        {
            target = MatchPickTarget.None;
            var hits = Physics.RaycastAll(
                ray,
                _maxRayDistance,
                MatchPickLayers.PickableLayerMask,
                QueryTriggerInteraction.Collide);

            if (hits.Length == 0)
            {
                return false;
            }

            var bestDistance = float.MaxValue;
            var found = false;
            foreach (var hit in hits)
            {
                if (!_registry.TryResolve(hit.collider, out var candidate) || !candidate.HasTarget)
                {
                    continue;
                }

                if (_canSelectTarget != null && !_canSelectTarget(candidate))
                {
                    continue;
                }

                if (!MatchPickMeshRaycast.TryResolveHit(
                        hit.collider,
                        ray,
                        hit.distance,
                        _maxRayDistance,
                        out var distance)
                    || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                target = candidate;
                found = true;
            }

            return found;
        }
    }
}
