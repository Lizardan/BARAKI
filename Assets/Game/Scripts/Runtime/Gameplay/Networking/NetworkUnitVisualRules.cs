using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Client-side unit visual smoothing toward authoritative snapshot positions.</summary>
    public static class NetworkUnitVisualRules
    {
        public const float DefaultCatchUpPerSecond = 14f;

        /// <summary>Snapshot publish rate (must match MatchNetworkAuthority.SnapshotHz).</summary>
        public const float SnapshotHz = 30f;
        
        /// <summary>Default interpolation delay: 4 snapshots at 30 Hz = 0.1333s exactly.</summary>
        public const int DefaultInterpSnapshotCount = 4;
        
        /// <summary>Minimum interpolation delay: 3 snapshots at 30 Hz = 0.1s exactly.</summary>
        public const int MinInterpSnapshotCount = 3;
        
        /// <summary>Hysteresis threshold: max interval above this triggers n=4, below this allows n=3.</summary>
        public const float JitterThresholdSnapshotIntervals = 1.5f;

        /// <summary>StepToward catch-up for host/offline presentation driven by 30 Hz sim ticks.</summary>
        public const float HostCatchUpPerSecond = 40f;
        
        /// <summary>
        /// Compute adaptive interpolation delay (n snapshots at SnapshotHz).
        /// Switches between n=3 and n=4 with hysteresis to avoid flicker.
        /// Returns n/SnapshotHz where n is 3 or 4.
        /// </summary>
        public static float ComputeAdaptiveDelay(
            IEnumerable<float> recentIntervals,
            int previousSnapshotCount)
        {
            if (recentIntervals == null)
            {
                return DefaultInterpSnapshotCount / SnapshotHz;
            }
            
            var count = 0;
            var maxInterval = 0f;
            var nominalInterval = 1f / SnapshotHz;
            
            foreach (var interval in recentIntervals)
            {
                if (interval > maxInterval)
                {
                    maxInterval = interval;
                }
                count++;
            }
            
            if (count == 0)
            {
                return DefaultInterpSnapshotCount / SnapshotHz;
            }
            
            // Hysteresis: if on n=4, need jitter clearly low to drop to n=3
            // If on n=3, need jitter spike above threshold to bump to n=4
            var threshold = nominalInterval * JitterThresholdSnapshotIntervals;
            
            int targetCount;
            if (previousSnapshotCount >= DefaultInterpSnapshotCount)
            {
                // On n=4: drop to n=3 only if max jitter is clearly below threshold
                targetCount = maxInterval < threshold * 0.8f ? MinInterpSnapshotCount : DefaultInterpSnapshotCount;
            }
            else
            {
                // On n=3: bump to n=4 if max jitter exceeds threshold
                targetCount = maxInterval > threshold ? DefaultInterpSnapshotCount : MinInterpSnapshotCount;
            }
            
            return targetCount / SnapshotHz;
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
