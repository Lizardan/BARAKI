using UnityEngine;

namespace Game.Gameplay.Data
{
    /// <summary>
    /// Ability list stored on a unit prefab. Empty / missing = no spells.
    /// Runtime copies these slots onto <see cref="Combat.MatchUnitState"/> at spawn.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BARAKI/Unit Ability Kit")]
    public sealed class UnitAbilityKit : MonoBehaviour
    {
        [SerializeField] private UnitAbilitySlot[] _slots = System.Array.Empty<UnitAbilitySlot>();

        public UnitAbilitySlot[] Slots => _slots ?? System.Array.Empty<UnitAbilitySlot>();

        public UnitAbilitySlot[] CloneSlots()
        {
            var source = Slots;
            if (source.Length == 0)
            {
                return System.Array.Empty<UnitAbilitySlot>();
            }

            var copy = new UnitAbilitySlot[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                copy[i] = source[i] != null ? source[i].Clone() : null;
            }

            return copy;
        }

        public void ReplaceSlots(UnitAbilitySlot[] slots)
        {
            _slots = slots ?? System.Array.Empty<UnitAbilitySlot>();
        }
    }
}
