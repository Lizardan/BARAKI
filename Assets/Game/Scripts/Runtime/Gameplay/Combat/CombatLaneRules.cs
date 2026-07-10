using Game.Core;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    public static class CombatLaneRules
    {
        public static bool CanEngage(MatchUnitState a, MatchUnitState b, LaneGraph graph)
        {
            if (a.OwnerSlot == b.OwnerSlot || !a.IsAlive || !b.IsAlive)
            {
                return false;
            }

            if (a.LaneId == b.LaneId)
            {
                return true;
            }

            var isMirrorFlank =
                (a.LaneId == GameIds.Lanes.Left && b.LaneId == GameIds.Lanes.Right) ||
                (a.LaneId == GameIds.Lanes.Right && b.LaneId == GameIds.Lanes.Left);
            if (!isMirrorFlank)
            {
                return false;
            }

            if (graph == null)
            {
                return true;
            }

            if (!graph.TryGetLane(a.OwnerSlot, a.LaneId, out var splineA))
            {
                return false;
            }

            return splineA.OpponentSlot == b.OwnerSlot;
        }
    }
}
