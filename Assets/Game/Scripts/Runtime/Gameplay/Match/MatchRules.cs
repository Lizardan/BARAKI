using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>Timings and economy constants from <c>Match Flow.md</c> / <c>Balance.md</c>.</summary>
    public static class MatchRules
    {
        public const float EarlyEndSeconds = 8f * 60f;
        public const float MidEndSeconds = 18f * 60f;
        public const int StartingGold = 250;
        /// <summary>GDD PHASE_START: camera intro may delay Early, but never longer than this.</summary>
        public const float StartPhaseMaxWaitSeconds = 5f;

        public static int GetStartingGold(string raceId) => StartingGold;

        public static MatchPhase ResolveTimePhase(float matchTimeSeconds)
        {
            if (matchTimeSeconds < EarlyEndSeconds)
            {
                return MatchPhase.Early;
            }

            if (matchTimeSeconds < MidEndSeconds)
            {
                return MatchPhase.Mid;
            }

            return MatchPhase.Late;
        }

        public static string ToPhaseId(MatchPhase phase) => phase switch
        {
            MatchPhase.Lobby => GameIds.Match.PhaseLobby,
            MatchPhase.Start => GameIds.Match.PhaseStart,
            MatchPhase.Early => GameIds.Match.PhaseEarly,
            MatchPhase.Mid => GameIds.Match.PhaseMid,
            MatchPhase.Late => GameIds.Match.PhaseLate,
            MatchPhase.End => GameIds.Match.PhaseEnd,
            _ => GameIds.Match.PhaseLobby,
        };
    }
}
