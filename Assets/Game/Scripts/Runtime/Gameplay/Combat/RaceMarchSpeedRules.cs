using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Race-wide march speed per GDD: identical within race; passives may modify.
    /// Per-unit override via <see cref="UnitDefinition.MarchSpeedOverride"/> for future abilities.
    /// </summary>
    public static class RaceMarchSpeedRules
    {
        public const float BaseMarchSpeed = 4f;

        public static float GetMarchSpeed(RaceDefinition race, UnitDefinition unit = null)
        {
            if (unit != null && unit.MarchSpeedOverride > 0f)
            {
                return unit.MarchSpeedOverride;
            }

            return BaseMarchSpeed;
        }
    }
}
