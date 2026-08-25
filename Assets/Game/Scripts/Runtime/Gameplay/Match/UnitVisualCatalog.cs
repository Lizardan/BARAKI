using System;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.Match
{
    /// <summary>Greybox unit prefabs and baked portraits keyed by race and combat role.</summary>
    [CreateAssetMenu(fileName = "UnitVisualCatalog", menuName = "Game/Unit Visual Catalog")]
    public sealed class UnitVisualCatalog : ScriptableObject
    {
        [SerializeField] private RaceVisualSet[] _races = Array.Empty<RaceVisualSet>();

        public bool TryGetPrefab(string raceId, UnitRole role, out GameObject prefab) =>
            TryGetPrefab(raceId, role, heroSlot: 0, bonusSlot: 0, out prefab);

        public bool TryGetPrefab(string raceId, UnitRole role, int heroSlot, out GameObject prefab) =>
            TryGetPrefab(raceId, role, heroSlot, bonusSlot: 0, out prefab);

        public bool TryGetPrefab(
            string raceId,
            UnitRole role,
            int heroSlot,
            int bonusSlot,
            out GameObject prefab)
        {
            prefab = GetSet(raceId)?.GetPrefab(role, heroSlot, bonusSlot);
            return prefab != null;
        }

        public bool TryGetPortrait(string raceId, UnitRole role, out Texture2D portrait) =>
            TryGetPortrait(raceId, role, heroSlot: 0, out portrait);

        public bool TryGetPortrait(string raceId, UnitRole role, int heroSlot, out Texture2D portrait)
        {
            portrait = GetSet(raceId)?.GetPortrait(role, heroSlot);
            return portrait != null;
        }

        /// <summary>Portrait of the enhanced variant for a bonus pick slot (1..10); null if not baked.</summary>
        public bool TryGetBonusPortrait(string raceId, int bonusSlot, out Texture2D portrait)
        {
            portrait = GetSet(raceId)?.GetBonusPortrait(bonusSlot);
            return portrait != null;
        }

        /// <summary>True when an enhanced-variant prefab is explicitly registered for this slot.</summary>
        public bool HasBonusPrefab(string raceId, int bonusSlot) =>
            GetSet(raceId)?.GetBonusPrefab(bonusSlot) != null;

        UnitVisualSet GetSet(string raceId)
        {
            for (var i = 0; i < _races.Length; i++)
            {
                if (_races[i] != null && _races[i].RaceId == raceId)
                {
                    return _races[i].Visuals;
                }
            }

            return null;
        }

        [Serializable]
        public sealed class RaceVisualSet
        {
            [SerializeField] private string _raceId;
            [SerializeField] private UnitVisualSet _visuals;

            public string RaceId => _raceId;
            public UnitVisualSet Visuals => _visuals;
        }

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
            [SerializeField] private GameObject _meleeBonus;
            [SerializeField] private GameObject _rangedBonus;
            [SerializeField] private GameObject _casterBonus;
            [SerializeField] private GameObject _siegeBonus;
            [SerializeField] private GameObject _flyingBonus;
            [SerializeField] private GameObject _superBonus;
            [SerializeField] private GameObject _hero1Bonus;
            [SerializeField] private GameObject _hero2Bonus;
            [SerializeField] private GameObject _hero3Bonus;
            [SerializeField] private GameObject _titanBonus;

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
            [SerializeField] private Texture2D _meleeBonusPortrait;
            [SerializeField] private Texture2D _rangedBonusPortrait;
            [SerializeField] private Texture2D _casterBonusPortrait;
            [SerializeField] private Texture2D _siegeBonusPortrait;
            [SerializeField] private Texture2D _flyingBonusPortrait;
            [SerializeField] private Texture2D _superBonusPortrait;
            [SerializeField] private Texture2D _hero1BonusPortrait;
            [SerializeField] private Texture2D _hero2BonusPortrait;
            [SerializeField] private Texture2D _hero3BonusPortrait;
            [SerializeField] private Texture2D _titanBonusPortrait;

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

            public GameObject GetPrefab(UnitRole role, int heroSlot) => GetPrefab(role, heroSlot, 0);

            public GameObject GetPrefab(UnitRole role, int heroSlot, int bonusSlot)
            {
                if (HumanBonusUnitRules.MatchesUnit(bonusSlot, role, heroSlot))
                {
                    return GetBonusPrefab(bonusSlot) ?? ResolveBasePrefab(role, heroSlot);
                }

                return ResolveBasePrefab(role, heroSlot);
            }

            GameObject ResolveBasePrefab(UnitRole role, int heroSlot) => role switch
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

            public GameObject GetBonusPrefab(int bonusSlot) => bonusSlot switch
            {
                1 => _meleeBonus,
                2 => _rangedBonus,
                3 => _casterBonus,
                4 => _siegeBonus,
                5 => _flyingBonus,
                6 => _superBonus,
                7 => _hero1Bonus,
                8 => _hero2Bonus,
                9 => _hero3Bonus,
                10 => _titanBonus,
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

            public Texture2D GetBonusPortrait(int bonusSlot) => bonusSlot switch
            {
                1 => _meleeBonusPortrait,
                2 => _rangedBonusPortrait,
                3 => _casterBonusPortrait,
                4 => _siegeBonusPortrait,
                5 => _flyingBonusPortrait,
                6 => _superBonusPortrait,
                7 => _hero1BonusPortrait,
                8 => _hero2BonusPortrait,
                9 => _hero3BonusPortrait,
                10 => _titanBonusPortrait,
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
