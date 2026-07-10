using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Data
{
    [CreateAssetMenu(fileName = "RaceDefinition", menuName = "Game/Race Definition")]
    public sealed class RaceDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private UnitDefinition _melee;
        [SerializeField] private UnitDefinition _ranged;
        [SerializeField] private UnitDefinition _caster;
        [SerializeField] private UnitDefinition _siege;
        [SerializeField] private UnitDefinition _flying;
        [SerializeField] private UnitDefinition _super;
        [SerializeField] private HeroDefinition[] _heroes;
        [SerializeField] private string[] _positivePassiveIds;
        [SerializeField] private string _negativePassiveId;

        public string Id => _id;
        public string DisplayName => _displayName;
        public UnitDefinition Melee => _melee;
        public UnitDefinition Ranged => _ranged;
        public UnitDefinition Caster => _caster;
        public UnitDefinition Siege => _siege;
        public UnitDefinition Flying => _flying;
        public UnitDefinition Super => _super;
        public IReadOnlyList<HeroDefinition> Heroes => _heroes;
        public IReadOnlyList<string> PositivePassiveIds => _positivePassiveIds;
        public string NegativePassiveId => _negativePassiveId;

        public UnitDefinition GetUnit(UnitRole role) => role switch
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
