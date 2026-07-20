using Game.Gameplay.Match;

namespace Game.UI
{
    /// <summary>Pure HUD clear rules for match end vs idle states.</summary>
    public static class MatchHudVisibility
    {
        /// <summary>
        /// Full HUD clear (including results) only when there is no active/finished match to show.
        /// Phase.End keeps results visible even though <c>IsRunning</c> is false.
        /// </summary>
        public static bool ShouldClearRunningHud(bool hasController, bool isRunning, MatchPhase phase)
        {
            if (!hasController)
            {
                return true;
            }

            if (isRunning)
            {
                return false;
            }

            return phase != MatchPhase.End;
        }

        /// <summary>Wave countdown labels are local-player only (no enemy barracks intel).</summary>
        public static bool ShouldShowBarracksTimer(int barracksOwnerSlot, int localPlayerSlot) =>
            barracksOwnerSlot == localPlayerSlot;
    }
}
