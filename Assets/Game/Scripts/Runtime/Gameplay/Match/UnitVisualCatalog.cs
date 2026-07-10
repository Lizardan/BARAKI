using System;
using Game.Core;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Greybox unit prefabs keyed by race and combat role.</summary>
    [CreateAssetMenu(fileName = "UnitVisualCatalog", menuName = "Game/Unit Visual Catalog")]
    public sealed class UnitVisualCatalog : ScriptableObject
    {
        [SerializeField] private UnitVisualSet _human;
        [SerializeField] private UnitVisualSet _bug;

        public bool TryGetPrefab(string raceId, UnitRole role, out GameObject prefab)
        {
            prefab = GetSet(raceId)?.GetPrefab(role);
            return prefab != null;
        }

        UnitVisualSet GetSet(string raceId) => raceId switch
        {
            GameIds.Races.Human => _human,
            GameIds.Races.Bug => _bug,
            _ => null,
        };

        [Serializable]
        public sealed class UnitVisualSet
        {
            [SerializeField] private GameObject _melee;
            [SerializeField] private GameObject _ranged;
            [SerializeField] private GameObject _caster;
            [SerializeField] private GameObject _siege;
            [SerializeField] private GameObject _flying;
            [SerializeField] private GameObject _super;

            public GameObject Melee => _melee;
            public GameObject Ranged => _ranged;
            public GameObject Caster => _caster;
            public GameObject Siege => _siege;
            public GameObject Flying => _flying;
            public GameObject Super => _super;

            public GameObject GetPrefab(UnitRole role) => role switch
            {
                UnitRole.Melee => _melee,
                UnitRole.Ranged => _ranged,
                UnitRole.Caster => _caster,
                UnitRole.Siege => _siege,
                UnitRole.Flying => _flying,
                UnitRole.Super => _super,
                _ => null,
            };
        }
    }
}
