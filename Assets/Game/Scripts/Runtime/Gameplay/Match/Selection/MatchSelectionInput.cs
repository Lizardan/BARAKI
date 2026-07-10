using System;

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



        public void Initialize(

            MatchPickRegistry registry,

            MatchSelection selection,

            Func<bool> isPointerOverUi = null)

        {

            _registry = registry;

            _selection = selection;

            _isPointerOverUi = isPointerOverUi;

        }



        public void SetUiBlocker(Func<bool> isPointerOverUi) => _isPointerOverUi = isPointerOverUi;



        private void Awake()

        {

            if (_camera == null)

            {

                _camera = Camera.main;

            }

        }



        private void Update()

        {

            if (_registry == null || _selection == null)

            {

                return;

            }



            var mouse = Mouse.current;

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)

            {

                return;

            }



            if (_isPointerOverUi != null && _isPointerOverUi())

            {

                return;

            }



            if (_camera == null)

            {

                _camera = Camera.main;

            }



            if (_camera == null)

            {

                return;

            }



            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());

            if (!TrySelectClosestPickable(ray, out var target))

            {

                _selection.Clear();

                return;

            }



            _selection.Select(target);

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



                if (hit.distance >= bestDistance)

                {

                    continue;

                }



                bestDistance = hit.distance;

                target = candidate;

                found = true;

            }



            return found;

        }

    }

}


