using System;
using Game.Gameplay.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.Match
{
    /// <summary>Greybox unit prefabs and baked portraits keyed by race and combat role.</summary>
    [CreateAssetMenu(fileName = "UnitVisualCatalog", menuName = "Game/Unit Visual Catalog")]
    public sealed class UnitVisualCatalog : ScriptableObject
    {
        [SerializeField] private UnitVisualSet _human;

        public bool TryGetPrefab(string raceId, UnitRole role, out GameObject prefab) =>
            TryGetPrefab(raceId, role, heroSlot: 0, out prefab);

        public bool TryGetPrefab(string raceId, UnitRole role, int heroSlot, out GameObject prefab)
        {
            prefab = GetSet(raceId)?.GetPrefab(role, heroSlot);
            return prefab != null;
        }

        public bool TryGetPortrait(string raceId, UnitRole role, out Texture2D portrait) =>
            TryGetPortrait(raceId, role, heroSlot: 0, out portrait);

        public bool TryGetPortrait(string raceId, UnitRole role, int heroSlot, out Texture2D portrait)
        {
            portrait = GetSet(raceId)?.GetPortrait(role, heroSlot);
            return portrait != null;
        }

        UnitVisualSet GetSet(string raceId) => _human;

        [Serializable]
        public sealed class UnitVisualSet
        {
            [SerializeField] private GameObject _melee;
            [SerializeField] private GameObject _ranged;
            [SerializeField] private GameObject _caster;
            [SerializeField] private GameObject _siege;
            [SerializeField] private GameObject _flying;
            [SerializeField] private GameObject _super;
            [FormerlySerializedAs("_hero")]
            [SerializeField] private GameObject _hero1;
            [SerializeField] private GameObject _hero2;
            [SerializeField] private GameObject _hero3;
            [SerializeField] private GameObject _titan;

            [SerializeField] private Texture2D _meleePortrait;
            [SerializeField] private Texture2D _rangedPortrait;
            [SerializeField] private Texture2D _casterPortrait;
            [SerializeField] private Texture2D _siegePortrait;
            [SerializeField] private Texture2D _flyingPortrait;
            [SerializeField] private Texture2D _superPortrait;
            [FormerlySerializedAs("_heroPortrait")]
            [SerializeField] private Texture2D _hero1Portrait;
            [SerializeField] private Texture2D _hero2Portrait;
            [SerializeField] private Texture2D _hero3Portrait;
            [SerializeField] private Texture2D _titanPortrait;

            public GameObject Melee => _melee;
            public GameObject Ranged => _ranged;
            public GameObject Caster => _caster;
            public GameObject Siege => _siege;
            public GameObject Flying => _flying;
            public GameObject Super => _super;
            public GameObject Hero1 => _hero1;
            public GameObject Hero2 => _hero2;
            public GameObject Hero3 => _hero3;
            public GameObject Titan => _titan;

            public GameObject GetPrefab(UnitRole role) => GetPrefab(role, 0);

            public GameObject GetPrefab(UnitRole role, int heroSlot) => role switch
            {
                UnitRole.Melee => _melee,
                UnitRole.Ranged => _ranged,
                UnitRole.Caster => _caster,
                UnitRole.Siege => _siege,
                UnitRole.Flying => _flying,
                UnitRole.Super => _super,
                UnitRole.Hero => ResolveHeroPrefab(heroSlot),
                UnitRole.Titan => _titan != null ? _titan : _hero1,
                _ => null,
            };

            public Texture2D GetPortrait(UnitRole role) => GetPortrait(role, 0);

            public Texture2D GetPortrait(UnitRole role, int heroSlot) => role switch
            {
                UnitRole.Melee => _meleePortrait,
                UnitRole.Ranged => _rangedPortrait,
                UnitRole.Caster => _casterPortrait,
                UnitRole.Siege => _siegePortrait,
                UnitRole.Flying => _flyingPortrait,
                UnitRole.Super => _superPortrait,
                UnitRole.Hero => ResolveHeroPortrait(heroSlot),
                UnitRole.Titan => _titanPortrait != null ? _titanPortrait : _hero1Portrait,
                _ => null,
            };

            GameObject ResolveHeroPrefab(int heroSlot) => heroSlot switch
            {
                2 => _hero2 != null ? _hero2 : _hero1,
                3 => _hero3 != null ? _hero3 : _hero1,
                _ => _hero1,
            };

            Texture2D ResolveHeroPortrait(int heroSlot) => heroSlot switch
            {
                2 => _hero2Portrait != null ? _hero2Portrait : _hero1Portrait,
                3 => _hero3Portrait != null ? _hero3Portrait : _hero1Portrait,
                _ => _hero1Portrait,
            };
        }
    }
}
