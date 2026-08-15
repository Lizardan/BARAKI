using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Ability def registry used to resolve <see cref="AbilityCastEvent"/>s by id (clients).</summary>
    [CreateAssetMenu(fileName = "UnitAbilityCatalog", menuName = "BARAKI/Ability Catalog")]
    public sealed class UnitAbilityCatalog : ScriptableObject
    {
        [SerializeField] private UnitAbilityDef[] _abilities = System.Array.Empty<UnitAbilityDef>();

        public System.Collections.Generic.IReadOnlyList<UnitAbilityDef> Abilities => _abilities;

        public UnitAbilityDef Find(int abilityId)
        {
            for (var i = 0; i < _abilities.Length; i++)
            {
                if (_abilities[i] != null && _abilities[i].AbilityId == abilityId)
                {
                    return _abilities[i];
                }
            }

            return null;
        }

        public bool TryGet(int abilityId, out UnitAbilityDef def)
        {
            def = Find(abilityId);
            return def != null;
        }

        public void ReplaceAbilities(UnitAbilityDef[] abilities)
        {
            _abilities = abilities ?? System.Array.Empty<UnitAbilityDef>();
        }
    }
}
