namespace Game.Gameplay.Networking
{
    /// <summary>Fixed-step host simulation (listen-host only; clients do not tick).</summary>
    public static class MatchNetworkSimTickRules
    {
        public const float TickHz = 30f;
        public const float FixedDeltaSeconds = 1f / TickHz;
        public const int MaxStepsPerFrame = 4;

        /// <summary>
        /// Accumulates frame time and returns how many fixed steps to run.
        /// Leftover stays in <paramref name="accumulator"/> (clamped after max steps).
        /// </summary>
        public static int ConsumeSteps(
            ref float accumulator,
            float frameDeltaSeconds,
            float fixedDeltaSeconds = FixedDeltaSeconds,
            int maxSteps = MaxStepsPerFrame)
        {
            if (frameDeltaSeconds < 0f)
            {
                frameDeltaSeconds = 0f;
            }

            if (fixedDeltaSeconds <= 0f || maxSteps <= 0)
            {
                return 0;
            }

            accumulator += frameDeltaSeconds;
            var steps = 0;
            while (accumulator >= fixedDeltaSeconds && steps < maxSteps)
            {
                accumulator -= fixedDeltaSeconds;
                steps++;
            }

            if (steps >= maxSteps && accumulator > fixedDeltaSeconds)
            {
                accumulator = fixedDeltaSeconds;
            }

            return steps;
        }
    }
}
