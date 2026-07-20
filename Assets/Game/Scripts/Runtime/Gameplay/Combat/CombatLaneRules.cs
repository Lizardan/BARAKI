using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    public static class CombatLaneRules
    {
        /// <summary>
        /// Hostile to any living unit that is not owned by the same player.
        /// Lane / original matchup do not gate aggro — distance does.
        /// </summary>
        public static bool CanEngage(MatchUnitState a, MatchUnitState b, LaneGraph graph)
        {
            _ = graph;
            return a != null
                   && b != null
                   && a.IsAlive
                   && b.IsAlive
                   && a.OwnerSlot != b.OwnerSlot;
        }
    }
}
