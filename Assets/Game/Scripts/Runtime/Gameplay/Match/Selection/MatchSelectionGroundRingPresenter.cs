using Game.Gameplay.Match.Selection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Match
{
    /// <summary>Green ground ring under the currently selected unit or building.</summary>
    public sealed class MatchSelectionGroundRingPresenter : MonoBehaviour
    {
        const string RingObjectName = "SelectionGroundRing";
        static readonly Color RingColor = new(0.31f, 0.86f, 0.39f, 1f);

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private MatchCombatPresenter _combatPresenter;
        [SerializeField] private Material _ringMaterial;

        Transform _ringTransform;
        MeshFilter _meshFilter;
        Renderer _ringRenderer;
        MatchSelection _selection;
        MatchPickTarget _currentTarget = MatchPickTarget.None;
        float _lockedDiameter;
        bool _hasLockedDiameter;

        static float GroundY => MatchArenaGreyboxBuilder.RoadHeight + 0.02f;

        void Awake()
        {
            if (_runtime == null)
            {
                _runtime = GetComponent<MatchRuntime>();
            }

            if (_combatPresenter == null)
            {
                _combatPresenter = GetComponent<MatchCombatPresenter>();
            }

            EnsureRing();
        }

        void OnEnable()
        {
            SubscribeSelection();
        }

        void OnDisable()
        {
            UnsubscribeSelection();
            SetRingVisible(false);
        }

        void LateUpdate()
        {
            if (_selection == null)
            {
                SubscribeSelection();
                return;
            }

            if (!_currentTarget.HasTarget || !_hasLockedDiameter)
            {
                return;
            }

            if (!TryResolveCenter(_currentTarget, out var center))
            {
                SetRingVisible(false);
                return;
            }

            EnsureRing();
            SetRingVisible(true);
            _ringTransform.position = new Vector3(center.x, GroundY, center.z);
        }

        void SubscribeSelection()
        {
            UnsubscribeSelection();

            var bridge = _runtime != null ? _runtime.GetComponent<MatchSelectionBridge>() : null;
            _selection = bridge != null ? bridge.Selection : null;
            if (_selection != null)
            {
                _selection.Changed += OnSelectionChanged;
                OnSelectionChanged(_selection.Current);
            }
        }

        void UnsubscribeSelection()
        {
            if (_selection != null)
            {
                _selection.Changed -= OnSelectionChanged;
                _selection = null;
            }
        }

        void OnSelectionChanged(MatchPickTarget target)
        {
            _currentTarget = target;
            _hasLockedDiameter = false;

            if (!target.HasTarget)
            {
                SetRingVisible(false);
                return;
            }

            if (!TryResolveFootprint(target, out _, out var diameter))
            {
                SetRingVisible(false);
                return;
            }

            _lockedDiameter = diameter;
            _hasLockedDiameter = true;
            ApplyRingMesh(_lockedDiameter);

            if (TryResolveCenter(target, out var center))
            {
                EnsureRing();
                SetRingVisible(true);
                _ringTransform.position = new Vector3(center.x, GroundY, center.z);
            }
        }

        void ApplyRingMesh(float diameter)
        {
            EnsureRing();
            var outerRadius = diameter * 0.5f;
            _meshFilter.sharedMesh = SelectionRingMeshBuilder.BuildAnnulus(outerRadius);
        }

        bool TryResolveCenter(MatchPickTarget target, out Vector3 center)
        {
            center = default;

            if (target.IsUnit)
            {
                if (_combatPresenter != null
                    && _combatPresenter.TryGetUnitGroundRing(target.EntityId, out center, out _))
                {
                    return true;
                }

                var controller = _runtime != null ? _runtime.Controller : null;
                if (controller == null)
                {
                    return false;
                }

                foreach (var unit in controller.Combat.Units)
                {
                    if (unit.UnitId != target.EntityId)
                    {
                        continue;
                    }

                    center = unit.WorldPosition;
                    return true;
                }

                return false;
            }

            if (!target.IsBuilding)
            {
                return false;
            }

            var buildings = _runtime?.Controller?.Buildings;
            var building = buildings?.GetByInstanceId(target.EntityId);
            if (building == null)
            {
                return false;
            }

            center = building.WorldPosition;
            return true;
        }

        bool TryResolveFootprint(MatchPickTarget target, out Vector3 center, out float diameter)
        {
            center = default;
            diameter = MatchPickFootprint.DefaultUnitDiameter * MatchPickFootprint.RingMargin;

            if (target.IsUnit)
            {
                if (_combatPresenter != null
                    && _combatPresenter.TryGetUnitGroundRing(target.EntityId, out center, out diameter))
                {
                    return true;
                }

                var controller = _runtime != null ? _runtime.Controller : null;
                if (controller == null)
                {
                    return false;
                }

                foreach (var unit in controller.Combat.Units)
                {
                    if (unit.UnitId != target.EntityId)
                    {
                        continue;
                    }

                    center = unit.WorldPosition;
                    return true;
                }

                return false;
            }

            if (!target.IsBuilding)
            {
                return false;
            }

            var buildings = _runtime?.Controller?.Buildings;
            var building = buildings?.GetByInstanceId(target.EntityId);
            if (building == null)
            {
                return false;
            }

            center = building.WorldPosition;
            diameter = MatchPickFootprint.GetBuildingDiameter(building.BuildingId);
            return true;
        }

        void EnsureRing()
        {
            if (_ringTransform != null)
            {
                return;
            }

            var ringObject = new GameObject(RingObjectName);
            ringObject.transform.SetParent(transform, false);

            _meshFilter = ringObject.AddComponent<MeshFilter>();
            _ringRenderer = ringObject.AddComponent<MeshRenderer>();
            _ringRenderer.sharedMaterial = ResolveRingMaterial();
            _ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;
            _ringTransform = ringObject.transform;
            SetRingVisible(false);
        }

        Material ResolveRingMaterial()
        {
            if (_ringMaterial != null)
            {
                return _ringMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", RingColor);
            material.SetFloat("_Surface", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetInt("_Cull", (int)CullMode.Off);
            material.renderQueue = (int)RenderQueue.Geometry + 10;
            return material;
        }

        void SetRingVisible(bool visible)
        {
            if (_ringRenderer != null)
            {
                _ringRenderer.enabled = visible;
            }
        }
    }
}
