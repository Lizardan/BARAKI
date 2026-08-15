using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>World HP bars above all buildings (ally and enemy); mana on main.</summary>
    public sealed class MatchBuildingStatusPresenter : MonoBehaviour
    {
        const float BuildingBarSizeScale = 2f;

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private float _barHeight = 5.5f;

        readonly Dictionary<int, UnitWorldStatusBars> _bars = new();
        readonly Dictionary<int, bool> _barShowsMana = new();
        Transform _root;

        void Awake()
        {
            if (_runtime == null)
            {
                _runtime = MatchRuntime.Current;
            }
        }

        void Update()
        {
            if (_runtime == null)
            {
                _runtime = MatchRuntime.Current;
            }

            if (_runtime == null || !_runtime.IsMatchStarted || _runtime.Controller?.Buildings == null)
            {
                ClearBars();
                return;
            }

            EnsureRoot();
            Sync(_runtime.Controller);
        }

        void Sync(MatchController controller)
        {
            var buildings = controller.Buildings;
            var alive = new HashSet<int>();
            foreach (var building in buildings.Buildings)
            {
                alive.Add(building.InstanceId);
                var showMana = ShouldShowMana(controller, building);
                if (!_bars.TryGetValue(building.InstanceId, out var bars)
                    || (_barShowsMana.TryGetValue(building.InstanceId, out var hadMana) && hadMana != showMana))
                {
                    if (bars != null)
                    {
                        Destroy(bars.transform.parent.gameObject);
                    }

                    var holder = new GameObject($"BuildingHp_{building.InstanceId}");
                    holder.transform.SetParent(_root, false);
                    holder.transform.position = building.WorldPosition + Vector3.up * _barHeight;
                    bars = UnitWorldStatusBars.Create(holder.transform, 0f, showManaBar: showMana);
                    bars.transform.localScale = Vector3.one * BuildingBarSizeScale;
                    _bars[building.InstanceId] = bars;
                    _barShowsMana[building.InstanceId] = showMana;
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
                if (showMana
                    && building.OwnerSlot >= 0
                    && building.OwnerSlot < controller.Players.Count)
                {
                    var player = controller.Players[building.OwnerSlot];
                    var manaRatio = player.MainManaMax > 0f ? player.MainMana / player.MainManaMax : 0f;
                    bars.SetMana(manaRatio);
                }

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
                _barShowsMana.Remove(remove[i]);
            }
        }

        static bool ShouldShowMana(MatchController controller, BuildingState building)
        {
            if (!BuildingRules.IsMain(building.BuildingId)
                || building.OwnerSlot < 0
                || building.OwnerSlot >= controller.Players.Count)
            {
                return false;
            }

            return controller.Players[building.OwnerSlot].MainManaMax > 0f;
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
            _barShowsMana.Clear();
        }

        void OnDisable() => ClearBars();
    }
}
