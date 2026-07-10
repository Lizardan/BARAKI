using Game.Gameplay.Combat;
using System.Collections.Generic;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchSelectionTargetRules
    {
        public static bool IsUnitTargetAlive(IReadOnlyList<MatchUnitState> units, int unitId)
        {
            if (units == null)
            {
                return false;
            }

            foreach (var unit in units)
            {
                if (unit.UnitId == unitId)
                {
                    return unit.IsAlive;
                }
            }

            return false;
        }
    }
}
