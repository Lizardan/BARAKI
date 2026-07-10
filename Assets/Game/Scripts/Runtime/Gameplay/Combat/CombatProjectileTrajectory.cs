using UnityEngine;

namespace Game.Gameplay.Combat
{
    public static class CombatProjectileTrajectory
    {
        public static float ComputeFlightDuration(Vector3 start, Vector3 end, float speed)
        {
            var delta = end - start;
            delta.y = 0f;
            return Mathf.Max(0.06f, delta.magnitude / speed);
        }

        public static Vector3 Evaluate(Vector3 start, Vector3 end, float progress, bool parabolic)
        {
            progress = Mathf.Clamp01(progress);
            var position = Vector3.Lerp(start, end, progress);
            if (parabolic)
            {
                position.y += 4f * CombatAttackRules.ParabolicArcHeight * progress * (1f - progress);
            }

            return position;
        }

        public static Vector3 GetProjectileOrigin(Vector3 unitPosition)
        {
            return unitPosition + Vector3.up * CombatAttackRules.ProjectileBodyHeight;
        }

        public static Vector3 GetProjectileTarget(Vector3 unitPosition)
        {
            return unitPosition + Vector3.up * (CombatAttackRules.ProjectileBodyHeight * 0.65f);
        }
    }
}
