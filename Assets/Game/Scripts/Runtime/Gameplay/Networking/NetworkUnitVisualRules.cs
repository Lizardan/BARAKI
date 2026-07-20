using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Client-side unit visual smoothing toward authoritative snapshot positions.</summary>
    public static class NetworkUnitVisualRules
    {
        public const float DefaultCatchUpPerSecond = 14f;

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
    }
}
