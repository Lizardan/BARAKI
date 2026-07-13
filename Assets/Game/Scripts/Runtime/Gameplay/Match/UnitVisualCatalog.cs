using System;

using Game.Core;

using Game.Gameplay.Data;

using UnityEngine;



namespace Game.Gameplay.Match

{

    /// <summary>Greybox unit prefabs and baked portraits keyed by race and combat role.</summary>

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



        public bool TryGetPortrait(string raceId, UnitRole role, out Texture2D portrait)

        {

            portrait = GetSet(raceId)?.GetPortrait(role);

            return portrait != null;

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



            [SerializeField] private Texture2D _meleePortrait;

            [SerializeField] private Texture2D _rangedPortrait;

            [SerializeField] private Texture2D _casterPortrait;

            [SerializeField] private Texture2D _siegePortrait;

            [SerializeField] private Texture2D _flyingPortrait;

            [SerializeField] private Texture2D _superPortrait;



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



            public Texture2D GetPortrait(UnitRole role) => role switch

            {

                UnitRole.Melee => _meleePortrait,

                UnitRole.Ranged => _rangedPortrait,

                UnitRole.Caster => _casterPortrait,

                UnitRole.Siege => _siegePortrait,

                UnitRole.Flying => _flyingPortrait,

                UnitRole.Super => _superPortrait,

                _ => null,

            };

        }

    }

}

