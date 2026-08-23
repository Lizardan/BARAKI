namespace Game.Core
{
    /// <summary>
    /// Visual catch-up for the launcher update bar: work may finish early,
    /// but the fill still eases to the target before the next phase.
    /// </summary>
    public static class LauncherProgressBarRules
    {
        public const float CatchUpPerSecond = 1.2f;
        public const float CaughtUpEpsilon = 0.005f;

        public static float StepDisplayed(float displayed, float target, float deltaTime)
        {
            var current = GameUpdateApplyProgressRules.Clamp01(displayed);
            var clampedTarget = GameUpdateApplyProgressRules.Clamp01(target);
            var step = CatchUpPerSecond * (deltaTime < 0f ? 0f : deltaTime);
            if (current < clampedTarget)
            {
                var next = current + step;
                return next > clampedTarget ? clampedTarget : next;
            }

            if (current > clampedTarget)
            {
                var next = current - step;
                return next < clampedTarget ? clampedTarget : next;
            }

            return current;
        }

        public static bool HasCaughtUp(float displayed, float target, float epsilon = CaughtUpEpsilon) =>
            GameUpdateApplyProgressRules.Clamp01(displayed) + epsilon
            >= GameUpdateApplyProgressRules.Clamp01(target);
    }
}
