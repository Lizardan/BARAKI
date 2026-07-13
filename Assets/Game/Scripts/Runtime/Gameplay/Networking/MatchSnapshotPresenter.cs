using System.Collections.Generic;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Minimal client renderer for authoritative network unit snapshots.</summary>
    public sealed class MatchSnapshotPresenter : MonoBehaviour
    {
        [SerializeField] private MatchRuntime _matchRuntime;
        [SerializeField] private float _markerScale = 1.5f;

        private readonly Dictionary<int, Transform> _markers = new();
        private Transform _root;
        private MatchSnapshot _lastAppliedSnapshot;

        private void Awake()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }
        }

        private void Update()
        {
            if (_matchRuntime == null || _matchRuntime.TickMode != MatchTickMode.Client)
            {
                return;
            }

            var snapshot = _matchRuntime.LastNetworkSnapshot;
            if (snapshot == null || ReferenceEquals(snapshot, _lastAppliedSnapshot))
            {
                return;
            }

            _lastAppliedSnapshot = snapshot;
            ApplySnapshot(snapshot);
        }

        private void OnDestroy()
        {
            _markers.Clear();
        }

        private void ApplySnapshot(MatchSnapshot snapshot)
        {
            EnsureRoot();
            var aliveIds = new HashSet<int>();
            foreach (var unit in snapshot.Units)
            {
                if (!unit.IsAlive)
                {
                    continue;
                }

                aliveIds.Add(unit.UnitId);
                if (!_markers.TryGetValue(unit.UnitId, out var marker) || marker == null)
                {
                    marker = CreateMarker(unit);
                    _markers[unit.UnitId] = marker;
                }

                marker.position = new Vector3(unit.PosX, _markerScale * 0.5f, unit.PosZ);
            }

            var staleIds = new List<int>();
            foreach (var pair in _markers)
            {
                if (!aliveIds.Contains(pair.Key))
                {
                    staleIds.Add(pair.Key);
                }
            }

            foreach (var unitId in staleIds)
            {
                if (_markers.TryGetValue(unitId, out var marker) && marker != null)
                {
                    Destroy(marker.gameObject);
                }

                _markers.Remove(unitId);
            }
        }

        private void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            var rootObject = new GameObject("NetworkGhostUnits");
            _root = rootObject.transform;
            _root.SetParent(transform, false);
        }

        private Transform CreateMarker(MatchUnitSnapshot unit)
        {
            var markerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            markerObject.name = $"NetworkUnit_{unit.UnitId}";
            markerObject.transform.SetParent(_root, false);
            markerObject.transform.localScale = Vector3.one * _markerScale;

            if (markerObject.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (markerObject.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.material.color = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
            }

            return markerObject.transform;
        }
    }
}
