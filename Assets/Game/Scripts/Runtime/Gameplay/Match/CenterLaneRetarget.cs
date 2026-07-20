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

                // Snapshot old mid before rebuild.
                var oldPath = lane.Path;
                var atEnd = combat?.CollectUnitsAtRouteEnd(lane.OwnerSlot, GameIds.Lanes.Center);
                var pastHalfway = combat?.CollectUnitsPastMidHalfway(
                    lane.OwnerSlot,
                    GameIds.Lanes.Center,
                    excludeUnitIds: atEnd);

                if (combat != null && pastHalfway != null && pastHalfway.Count > 0 && oldPath != null)
                {
                    combat.CommitUnitsToMidPath(
                        lane.OwnerSlot,
                        pastHalfway,
                        oldPath,
                        next.Value);
                }

                var skip = MergeUnitIds(atEnd, pastHalfway);

                var owner = layout.Slots[lane.OwnerSlot];
                var nextOpponent = layout.Slots[next.Value];
                lane.OpponentSlot = next.Value;
                lane.Path = LaneGraphBuilder.BuildCenterPath(owner, nextOpponent, graph.CenterArenaRadius);

                combat?.ReplaceLaneRoute(
                    lane.OwnerSlot,
                    GameIds.Lanes.Center,
                    lane.Path,
                    skipUnitIds: skip);

                if (atEnd == null || atEnd.Count == 0 || combat == null)
                {
                    continue;
                }

                combat.RemountUnitsToFlankToward(
                    lane.OwnerSlot,
                    atEnd,
                    next.Value,
                    layout);
            }

            combat?.RetargetUnitsFocusingEliminated(eliminatedSlot, players, layout);
        }

        static HashSet<int> MergeUnitIds(HashSet<int> a, HashSet<int> b)
        {
            if ((a == null || a.Count == 0) && (b == null || b.Count == 0))
            {
                return null;
            }

            var merged = new HashSet<int>();
            if (a != null)
            {
                merged.UnionWith(a);
            }

            if (b != null)
            {
                merged.UnionWith(b);
            }

            return merged;
        }
    }
}
