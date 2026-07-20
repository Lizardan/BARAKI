using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>World HP bars above all buildings (ally and enemy).</summary>
    public sealed class MatchBuildingStatusPresenter : MonoBehaviour
    {
        const float BuildingBarSizeScale = 2f;

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private float _barHeight = 5.5f;

        readonly Dictionary<int, UnitWorldStatusBars> _bars = new();
        Transform _root;

        void Awake()
        {
            if (_runtime == null)
            {
                _runtime = FindAnyObjectByType<MatchRuntime>();
            }
        }

        void Update()
        {
            if (_runtime == null)
            {
                _runtime = FindAnyObjectByType<MatchRuntime>();
            }

            if (_runtime == null || !_runtime.IsMatchStarted || _runtime.Controller?.Buildings == null)
            {
                ClearBars();
                return;
            }

            EnsureRoot();
            Sync(_runtime.Controller.Buildings);
        }

        void Sync(BuildingRegistry buildings)
        {
            var alive = new HashSet<int>();
            foreach (var building in buildings.Buildings)
            {
                alive.Add(building.InstanceId);
                if (!_bars.TryGetValue(building.InstanceId, out var bars))
                {
                    var holder = new GameObject($"BuildingHp_{building.InstanceId}");
                    holder.transform.SetParent(_root, false);
                    holder.transform.position = building.WorldPosition + Vector3.up * _barHeight;
                    bars = UnitWorldStatusBars.Create(holder.transform, 0f, showManaBar: false);
                    bars.transform.localScale = Vector3.one * BuildingBarSizeScale;
                    _bars[building.InstanceId] = bars;
                }

                bars.transform.localScale = Vector3.one * BuildingBarSizeScale;
                bars.transform.parent.position = building.WorldPosition + Vector3.up * _barHeight;
                if (building.IsRuins || building.CurrentHp <= 0f)
                {
                    bars.gameObject.SetActive(false);
                    continue;
                }

                var ratio = building.MaxHp > 0f ? building.CurrentHp / building.MaxHp : 0f;
                bars.SetHealth(ratio);
                bars.gameObject.SetActive(true);
            }

            var remove = new List<int>();
            foreach (var pair in _bars)
            {
                if (alive.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    Destroy(pair.Value.transform.parent.gameObject);
                }

                remove.Add(pair.Key);
            }

            for (var i = 0; i < remove.Count; i++)
            {
                _bars.Remove(remove[i]);
            }
        }

        void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            var go = new GameObject("BuildingStatusBars");
            _root = go.transform;
            _root.SetParent(transform, false);
        }

        void ClearBars()
        {
            foreach (var pair in _bars)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.transform.parent.gameObject);
                }
            }

            _bars.Clear();
        }

        void OnDisable() => ClearBars();
    }
}
