using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match.Selection;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>World-position pick proxies for buildings from <see cref="BuildingRegistry"/>.</summary>
    public sealed class MatchBuildingPickPresenter : MonoBehaviour
    {
        const string PickRootName = "MatchBuildingPickRoot";

        [SerializeField] private MatchRuntime _runtime;

        readonly List<Collider> _colliders = new();
        Transform _pickRoot;

        void Awake()
        {
            if (_runtime == null)
            {
                _runtime = GetComponent<MatchRuntime>();
            }
        }

        public void RefreshBuildingPicks()
        {
            if (_runtime == null)
            {
                _runtime = GetComponent<MatchRuntime>();
            }

            ClearPicks();
            EnsurePickRoot();

            var controller = _runtime != null ? _runtime.Controller : null;
            var bridge = _runtime != null ? _runtime.GetComponent<MatchSelectionBridge>() : null;
            if (controller == null || bridge == null)
            {
                return;
            }

            foreach (var building in controller.Buildings.Buildings)
            {
                var size = MatchPickFootprint.GetBuildingPickSize(building.BuildingId) * MatchPickFootprint.PickSizeMargin;
                var proxy = new GameObject($"Pick_{building.BuildingId}_{building.InstanceId}");
                proxy.transform.SetParent(_pickRoot, false);
                proxy.transform.position = building.WorldPosition;

                var collider = MatchPickColliderUtility.EnsurePickCollider(
                    proxy,
                    new Vector3(0f, size.y * 0.5f, 0f),
                    size);

                var handle = proxy.AddComponent<MatchPickHandle>();
                handle.ConfigureBuilding(building.InstanceId);
                bridge.RegisterPickCollider(collider, MatchPickTarget.Building(building.InstanceId));
                _colliders.Add(collider);
            }
        }

        void ClearPicks()
        {
            var bridge = _runtime != null ? _runtime.GetComponent<MatchSelectionBridge>() : null;
            if (bridge != null)
            {
                foreach (var collider in _colliders)
                {
                    if (collider != null)
                    {
                        bridge.UnregisterPickCollider(collider);
                    }
                }
            }

            _colliders.Clear();

            if (_pickRoot != null)
            {
                for (var i = _pickRoot.childCount - 1; i >= 0; i--)
                {
                    var child = _pickRoot.GetChild(i);
                    if (child != null)
                    {
                        DestroyPickObject(child.gameObject);
                    }
                }
            }
        }

        void EnsurePickRoot()
        {
            if (_pickRoot != null)
            {
                return;
            }

            var existing = transform.Find(PickRootName);
            if (existing != null)
            {
                _pickRoot = existing;
                return;
            }

            var rootObject = new GameObject(PickRootName);
            rootObject.transform.SetParent(transform, false);
            _pickRoot = rootObject.transform;
        }

        static void DestroyPickObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
