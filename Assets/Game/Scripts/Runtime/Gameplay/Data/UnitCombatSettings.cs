using UnityEngine;

namespace Game.Gameplay.Data
{
    /// <summary>
    /// Runtime combat profile stored on the unit prefab. The prefab owns NO numbers — it only holds
    /// a reference to its source definition asset (<see cref="UnitDefinition"/> or
    /// <see cref="HeroDefinition"/>, exactly one) and references to the <see cref="UnitAbilityDef"/>
    /// kit it carries (0/1/…/4 abilities). Stats resolve live from the referenced ScriptableObject;
    /// hero/titan/veteran variants are derived by <see cref="Game.Gameplay.Combat.UnitStatsResolver"/>
    /// with the canonical multipliers. Editing a definition asset applies everywhere immediately —
    /// no sync step.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BARAKI/Unit Combat Settings")]
    public sealed class UnitCombatSettings : MonoBehaviour
    {
        [SerializeField] private UnitDefinition _unitDefinition;
        [SerializeField] private HeroDefinition _heroDefinition;
        [SerializeField] private UnitAbilityDef[] _abilities = System.Array.Empty<UnitAbilityDef>();

        /// <summary>Stats source for a plain unit (incl. Human bonus units). Null for heroes/titan.</summary>
        public UnitDefinition UnitDefinition => _unitDefinition;

        /// <summary>Stats source for a hero / titan / veteran (titan &amp; veteran variants multiply it).</summary>
        public HeroDefinition HeroDefinition => _heroDefinition;

        public UnitAbilityDef[] Abilities => _abilities ?? System.Array.Empty<UnitAbilityDef>();

        public bool HasStatsSource => _unitDefinition != null || _heroDefinition != null;

        public void AssignSource(UnitDefinition definition)
        {
            _unitDefinition = definition;
            _heroDefinition = null;
        }

        public void AssignSource(HeroDefinition definition)
        {
            _heroDefinition = definition;
            _unitDefinition = null;
        }

        public void ReplaceAbilities(UnitAbilityDef[] abilities)
        {
            _abilities = abilities ?? System.Array.Empty<UnitAbilityDef>();
        }

        public void CopyFrom(UnitCombatSettings other)
        {
            if (other == null)
            {
                return;
            }

            _unitDefinition = other._unitDefinition;
            _heroDefinition = other._heroDefinition;
            _abilities = other.Abilities;
        }
    }
}