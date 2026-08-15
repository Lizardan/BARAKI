using Game.Gameplay.Match.Selection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// While main-extra targeting is pending: red ground ring under a valid hover target
    /// (same footprint as click selection).
    /// </summary>
    public sealed class MatchMainExtraTargetingRingPresenter : MonoBehaviour
    {
        const string RingObjectName = "MainExtraTargetingRing";
        static readonly Color RingColor = new(0.95f, 0.22f, 0.2f, 1f);

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private MatchCombatPresenter _combatPresenter;
        [SerializeField] private Material _ringMaterial;

        Transform _ringTransform;
        MeshFilter _meshFilter;
        Renderer _ringRenderer;
        MatchSelectionBridge _bridge;
        MatchPickTarget _currentTarget = MatchPickTarget.None;
        float _lockedDiameter;

        static float GroundY => MatchArenaGreyboxBuilder.RoadHeight + 0.025f;

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

        void OnDisable()
        {
            SetRingVisible(false);
        }

        void LateUpdate()
        {
            if (_bridge == null)
            {
                _bridge = MatchSelectionBridge.Current
                          ?? (_runtime != null ? _runtime.GetComponent<MatchSelectionBridge>() : null);
            }

            if (_bridge == null || !_bridge.IsMainExtraCastPending)
            {
                _currentTarget = MatchPickTarget.None;
                SetRingVisible(false);
                return;
            }

            var hover = _bridge.HoverTarget;
            if (!hover.HasTarget)
            {
                _currentTarget = MatchPickTarget.None;
                SetRingVisible(false);
                return;
            }

            if (!_currentTarget.Equals(hover) || _meshFilter == null || _meshFilter.sharedMesh == null)
            {
                if (!TryResolveFootprint(hover, out _, out var diameter))
                {
                    SetRingVisible(false);
                    return;
                }

                _currentTarget = hover;
                _lockedDiameter = diameter;
                ApplyRingMesh(_lockedDiameter);
            }

            if (!TryResolveCenter(hover, out var center))
            {
                SetRingVisible(false);
                return;
            }

            EnsureRing();
            SetRingVisible(true);
            _ringTransform.position = new Vector3(center.x, GroundY, center.z);
        }

        void ApplyRingMesh(float diameter)
        {
            EnsureRing();
            _meshFilter.sharedMesh = SelectionRingMeshBuilder.BuildAnnulus(diameter * 0.5f);
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

            var building = _runtime?.Controller?.Buildings?.GetByInstanceId(target.EntityId);
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

            var building = _runtime?.Controller?.Buildings?.GetByInstanceId(target.EntityId);
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
            material.renderQueue = (int)RenderQueue.Geometry + 11;
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
