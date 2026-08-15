using System.Collections.Generic;
using Game.Gameplay.Match.Selection;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Pick colliders for buildings. Uses a box from the visual mesh bounds, then
    /// <see cref="MatchPickMeshRaycast"/> rejects rays that only graze empty AABB space.
    /// </summary>
    public sealed class MatchBuildingPickPresenter : MonoBehaviour
    {
        const string PickRootName = "MatchBuildingPickRoot";

        [SerializeField] private MatchRuntime _runtime;

        readonly List<Collider> _colliders = new();
        readonly List<GameObject> _ownedProxies = new();
        readonly List<GameObject> _attachedVisualHosts = new();
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
                if (!TryAttachVisualPick(building, bridge, out var collider))
                {
                    collider = CreateFallbackProxyPick(building, bridge);
                }

                if (collider != null)
                {
                    _colliders.Add(collider);
                }
            }
        }

        bool TryAttachVisualPick(BuildingState building, MatchSelectionBridge bridge, out Collider collider)
        {
            collider = null;
            var visual = FindBuildingVisual(building);
            if (visual == null)
            {
                return false;
            }

            var meshFilter = visual.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            var host = meshFilter.gameObject;
            collider = MatchPickColliderUtility.EnsureVisualPickCollider(host, meshFilter.sharedMesh);
            if (collider == null)
            {
                return false;
            }

            var handle = host.GetComponent<MatchPickHandle>();
            if (handle == null)
            {
                handle = host.AddComponent<MatchPickHandle>();
            }

            handle.ConfigureBuilding(building.InstanceId);
            bridge.RegisterPickCollider(collider, MatchPickTarget.Building(building.InstanceId));
            _attachedVisualHosts.Add(host);
            return true;
        }

        Collider CreateFallbackProxyPick(BuildingState building, MatchSelectionBridge bridge)
        {
            var size = MatchPickFootprint.GetBuildingPickSize(building.BuildingId);
            var proxy = new GameObject($"Pick_{building.BuildingId}_{building.InstanceId}");
            proxy.transform.SetParent(_pickRoot, false);
            proxy.transform.position = building.WorldPosition;

            var greybox = MatchArenaGreybox.Current;
            if (greybox != null
                && greybox.Layout != null
                && building.OwnerSlot >= 0
                && building.OwnerSlot < greybox.Layout.Slots.Count)
            {
                var slot = greybox.Layout.Slots[building.OwnerSlot];
                proxy.transform.rotation = slot.BaseRotation * BaseLayoutDefinition.GetLocalRotation(building.BuildingId);
            }

            var collider = MatchPickColliderUtility.EnsurePickCollider(
                proxy,
                new Vector3(0f, size.y * 0.5f, 0f),
                size);

            var handle = proxy.AddComponent<MatchPickHandle>();
            handle.ConfigureBuilding(building.InstanceId);
            bridge.RegisterPickCollider(collider, MatchPickTarget.Building(building.InstanceId));
            _ownedProxies.Add(proxy);
            return collider;
        }

        static Transform FindBuildingVisual(BuildingState building) =>
            MatchArenaGreybox.FindBuildingVisual(building);

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

            foreach (var host in _attachedVisualHosts)
            {
                MatchPickColliderUtility.RemovePickCollider(host);
            }

            _attachedVisualHosts.Clear();

            foreach (var proxy in _ownedProxies)
            {
                DestroyPickObject(proxy);
            }

            _ownedProxies.Clear();

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
