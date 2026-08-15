using UnityEngine;

namespace Game.Gameplay.Data
{
    /// <summary>
    /// Ability list stored on a unit prefab as shared <see cref="UnitAbilityDef"/> asset references.
    /// Runtime copies these references onto <see cref="Combat.MatchUnitState"/> at spawn.
    /// Empty / missing = no spells.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BARAKI/Unit Ability Kit")]
    public sealed class UnitAbilityKit : MonoBehaviour
    {
        [SerializeField] private UnitAbilityDef[] _abilities = System.Array.Empty<UnitAbilityDef>();

        public UnitAbilityDef[] Abilities => _abilities ?? System.Array.Empty<UnitAbilityDef>();

        public void ReplaceAbilities(UnitAbilityDef[] abilities)
        {
            _abilities = abilities ?? System.Array.Empty<UnitAbilityDef>();
        }
    }
}
