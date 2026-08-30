using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Client-side unit visual smoothing toward authoritative snapshot positions.</summary>
    public static class NetworkUnitVisualRules
    {
        public const float DefaultCatchUpPerSecond = 14f;

        /// <summary>Minimum interpolation delay: 3 snapshots at 30 Hz (~100ms).</summary>
        public const float MinInterpDelaySeconds = 3f / 30f;
        
        /// <summary>Maximum interpolation delay to clamp adaptive jitter (~150ms, 4.5 snapshots at 30 Hz).</summary>
        public const float MaxInterpDelaySeconds = 0.15f;

        /// <summary>StepToward catch-up for host/offline presentation driven by 30 Hz sim ticks.</summary>
        public const float HostCatchUpPerSecond = 40f;
        
        /// <summary>
        /// Compute adaptive interpolation delay based on recent snapshot arrival intervals.
        /// Targets 3-4 snapshots delay, adapts to jitter, clamped to [MinInterpDelaySeconds, MaxInterpDelaySeconds].
        /// </summary>
        public static float ComputeAdaptiveDelay(IEnumerable<float> recentIntervals)
        {
            if (recentIntervals == null)
            {
                return MinInterpDelaySeconds;
            }
            
            var count = 0;
            var sum = 0f;
            var maxInterval = 0f;
            
            foreach (var interval in recentIntervals)
            {
                sum += interval;
                if (interval > maxInterval)
                {
                    maxInterval = interval;
                }
                count++;
            }
            
            if (count == 0)
            {
                return MinInterpDelaySeconds;
            }
            
            // Average interval + max jitter spike, clamped to min/max
            var avgInterval = sum / count;
            var jitterSpike = Mathf.Max(0f, maxInterval - avgInterval);
            var targetDelay = avgInterval * 3f + jitterSpike;
            
            return Mathf.Clamp(targetDelay, MinInterpDelaySeconds, MaxInterpDelaySeconds);
        }

        public static Vector3 StepToward(
            Vector3 current,
            Vector3 target,
            float deltaTime,
            float catchUpPerSecond = DefaultCatchUpPerSecond)
        {
            if (deltaTime <= 0f || catchUpPerSecond <= 0f)
            {
                return target;
            }

            var t = 1f - Mathf.Exp(-catchUpPerSecond * deltaTime);
            return Vector3.Lerp(current, target, t);
        }

        public static bool ShouldLerpPositions(MatchTickMode tickMode) =>
            tickMode == MatchTickMode.Client;

        /// <summary>Resolves the render facing between two snapshot facings without crossing the world-up axis.</summary>
        public static Quaternion ResolveRenderFacing(Vector3 prevFacing, Vector3 nextFacing, float alpha)
        {
            var from = prevFacing.sqrMagnitude > 0.0001f ? prevFacing.normalized : Vector3.forward;
            var to = nextFacing.sqrMagnitude > 0.0001f ? nextFacing.normalized : Vector3.forward;
            if (Vector3.Dot(from, to) < 0f)
            {
                return Quaternion.LookRotation(to, Vector3.up);
            }

            var direction = Vector3.Slerp(from, to, Mathf.Clamp01(alpha));
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = to;
            }

            return Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
