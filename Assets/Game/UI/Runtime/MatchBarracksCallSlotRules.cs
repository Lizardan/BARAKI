using Game.Gameplay.Data;

namespace Game.UI
{
    /// <summary>
    /// Barracks call buttons on the 3-col command grid (12 slots).
    /// Last row = first three roles; second-to-last row = the next three.
    /// </summary>
    public static class MatchBarracksCallSlotRules
    {
        public const int PredLastRowStart = 6;
        public const int LastRowStart = 9;

        public static bool TryGetCommandSlot(UnitRole role, out int slotIndex)
        {
            slotIndex = role switch
            {
                UnitRole.Melee => LastRowStart,
                UnitRole.Ranged => LastRowStart + 1,
                UnitRole.Caster => LastRowStart + 2,
                UnitRole.Siege => PredLastRowStart,
                UnitRole.Flying => PredLastRowStart + 1,
                UnitRole.Super => PredLastRowStart + 2,
                _ => -1,
            };
            return slotIndex >= 0;
        }
    }
}
