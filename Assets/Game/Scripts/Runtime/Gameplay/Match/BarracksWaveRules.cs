using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>Per-barracks wave timing from <c>Balance.md</c>.</summary>
    public static class BarracksWaveRules
    {
        public const float BaseIntervalSeconds = 35f;
        public const float SpawnSpeedPerLevel = 0.05f;

        public static float GetWaveIntervalSeconds(int barracksLevel, bool isRuins)
        {
            var interval = isRuins
                ? BaseIntervalSeconds
                : BaseIntervalSeconds / PowSpawnSpeed(barracksLevel);

            return interval;
        }

        public static int GetEffectiveSquadLevel(int barracksLevel, bool isRuins, int frozenSquadLevel)
        {
            return isRuins ? frozenSquadLevel : barracksLevel;
        }

        public static string GetSquadId(int squadLevel) => squadLevel switch
        {
            1 => GameIds.Squads.BarracksL1,
            2 => GameIds.Squads.BarracksL2,
            3 => GameIds.Squads.BarracksL3,
            4 => GameIds.Squads.BarracksL4,
            _ => GameIds.Squads.BarracksL1,
        };

        private static float PowSpawnSpeed(int barracksLevel)
        {
            var clampedLevel = barracksLevel < 1 ? 1 : barracksLevel;
            return UnityEngine.Mathf.Pow(1f + SpawnSpeedPerLevel, clampedLevel - 1);
        }
    }
}
