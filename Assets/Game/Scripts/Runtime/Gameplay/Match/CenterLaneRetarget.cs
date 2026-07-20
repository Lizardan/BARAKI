using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;

namespace Game.Gameplay.Match
{
    /// <summary>Applies center-lane opponent retarget after a player is eliminated.</summary>
    public static class CenterLaneRetarget
    {
        public static void Apply(
            int eliminatedSlot,
            IReadOnlyList<MatchPlayerState> players,
            MatchArenaLayout layout,
            LaneGraph graph,
            MatchCombatSystem combat)
        {
            if (players == null || layout == null || graph == null)
            {
                return;
            }

            foreach (var lane in graph.Lanes)
            {
                if (lane == null || !lane.IsCenterLane)
                {
                    continue;
                }

                if (lane.OwnerSlot < 0 || lane.OwnerSlot >= players.Count)
                {
                    continue;
                }

                if (players[lane.OwnerSlot].IsEliminated)
                {
                    continue;
                }

                if (lane.OpponentSlot != eliminatedSlot)
                {
                    continue;
                }

                var next = CenterMarchRetargetRules.ResolveNextAliveClockwise(
                    eliminatedSlot,
                    players,
                    lane.OwnerSlot);
                if (!next.HasValue)
                {
                    continue;
                }

                var owner = layout.Slots[lane.OwnerSlot];
                var nextOpponent = layout.Slots[next.Value];
                lane.OpponentSlot = next.Value;
                lane.Path = LaneGraphBuilder.BuildCenterPath(owner, nextOpponent, graph.CenterArenaRadius);
                combat?.ReplaceLaneRoute(lane.OwnerSlot, GameIds.Lanes.Center, lane.Path);
            }
        }
    }
}
