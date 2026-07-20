using System.Collections.Generic;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Center-lane march retarget: when the current enemy is eliminated,
    /// waves turn toward the next alive slot clockwise (GDD E1).
    /// </summary>
    public static class CenterMarchRetargetRules
    {
        /// <summary>
        /// First alive slot clockwise from <paramref name="eliminatedSlot"/>,
        /// skipping eliminated players and the marching owner.
        /// </summary>
        public static int? ResolveNextAliveClockwise(
            int eliminatedSlot,
            IReadOnlyList<MatchPlayerState> players,
            int ownerSlot = -1)
        {
            if (players == null || players.Count == 0)
            {
                return null;
            }

            var n = players.Count;
            if (eliminatedSlot < 0 || eliminatedSlot >= n)
            {
                return null;
            }

            for (var step = 1; step < n; step++)
            {
                var slot = (eliminatedSlot + step) % n;
                if (slot == ownerSlot || players[slot].IsEliminated)
                {
                    continue;
                }

                return slot;
            }

            return null;
        }
    }
}
