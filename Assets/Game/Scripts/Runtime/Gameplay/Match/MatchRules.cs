using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>Timings and economy constants from <c>Match Flow.md</c> / <c>Balance.md</c>.</summary>
    public static class MatchRules
    {
        public const float EarlyEndSeconds = 8f * 60f;
        public const float MidEndSeconds = 18f * 60f;
        public const int DefaultStartingGold = 500;
        public const int HumanStartingGoldPenalty = 250;

        public static int GetStartingGold(string raceId)
        {
            return raceId == GameIds.Races.Human
                ? DefaultStartingGold - HumanStartingGoldPenalty
                : DefaultStartingGold;
        }

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
