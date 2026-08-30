using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Client-side unit visual smoothing toward authoritative snapshot positions.</summary>
    public static class NetworkUnitVisualRules
    {
        public const float DefaultCatchUpPerSecond = 14f;

        /// <summary>Snapshot publish rate (must match MatchNetworkAuthority.SnapshotHz).</summary>
        public const float SnapshotHz = 30f;
        
        /// <summary>
        /// Fixed interpolation delay for both host and client: 4 snapshots at 30 Hz = 0.1333s exactly.
        /// Competitive FFA требует одинаковый visual delay у всех peers для fair presentation.
        /// </summary>
        public const int InterpSnapshotCount = 4;
        
        /// <summary>Fixed interpolation delay in seconds (4/30).</summary>
        public static float InterpDelaySeconds => InterpSnapshotCount / SnapshotHz;

        /// <summary>StepToward catch-up for host/offline presentation driven by 30 Hz sim ticks.</summary>
        public const float HostCatchUpPerSecond = 40f;

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
