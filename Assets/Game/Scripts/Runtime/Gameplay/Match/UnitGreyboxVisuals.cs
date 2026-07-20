using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Shared greybox unit visual scale for presenter prefabs.</summary>
    public static class UnitGreyboxVisuals
    {
        public const float Scale = 2f;

        /// <summary>Animated Human (WC3-derived) models are authored larger than greybox capsules.</summary>
        public const float AnimatedHumanScaleFactor = 0.5f;

        /// <summary>Melee is slightly under baseline greybox height.</summary>
        public const float AnimatedHumanMeleeScaleFactor = 0.8f;

        /// <summary>Baseline large-role multiplier (Caster), then −20%.</summary>
        public const float AnimatedHumanLargeRoleScaleFactor = 2f * 0.8f;

        /// <summary>Siege is 25% smaller than the pre-tweak large-role baseline (2×).</summary>
        public const float AnimatedHumanSiegeScaleFactor = 2f * 0.75f;

        /// <summary>Super is 35% smaller than the pre-tweak large-role baseline (2×).</summary>
        public const float AnimatedHumanSuperScaleFactor = 2f * 0.65f;

        /// <summary>Extra world lift so flying units stay clearly above the road.</summary>
        public const float FlyingHoverHeight = 4f;

        /// <summary>
        /// Super Walk resets armature root position; keep a small clearance so feet stay above the road.
        /// </summary>
        public const float SuperGroundClearance = 1.5f;

        /// <summary>
        /// WC3 meshes face +X; match locomotion faces +Z. Prefab yaw aligns model forward.
        /// </summary>
        public const float AnimatedHumanModelYawDegrees = 90f;

        /// <summary>Flying (airship) uses the same +X→+Z yaw as other Human units.</summary>
        public const float AnimatedHumanFlyingModelYawDegrees = 90f;

        /// <summary>
        /// GodsPaladin Walk leans the torso back in the facing plane; positive Z roll tips it upright
        /// when prefab yaw is 90°.
        /// </summary>
        public const float AnimatedHumanSuperModelRollDegrees = 8f;

        /// <summary>Extra per-role multiplier baked into Human animated prefab scale.</summary>
        public static float GetAnimatedHumanRoleScale(UnitRole role) =>
            role switch
            {
                UnitRole.Melee => AnimatedHumanMeleeScaleFactor,
                UnitRole.Caster => AnimatedHumanLargeRoleScaleFactor,
                UnitRole.Siege => AnimatedHumanSiegeScaleFactor,
                UnitRole.Super => AnimatedHumanSuperScaleFactor,
                _ => 1f,
            };

        /// <summary>Prefab root yaw for Human animated models.</summary>
        public static float GetAnimatedHumanModelYawDegrees(UnitRole role) =>
            role == UnitRole.Flying
                ? AnimatedHumanFlyingModelYawDegrees
                : AnimatedHumanModelYawDegrees;

        /// <summary>Prefab root euler for Human animated models (yaw + optional Super roll).</summary>
        public static Vector3 GetAnimatedHumanModelEuler(UnitRole role)
        {
            var yaw = GetAnimatedHumanModelYawDegrees(role);
            var roll = role == UnitRole.Super ? AnimatedHumanSuperModelRollDegrees : 0f;
            return new Vector3(0f, yaw, roll);
        }

        /// <summary>Local model offset applied by the combat presenter.</summary>
        public static Vector3 GetModelLocalOffset(UnitRole role) =>
            role switch
            {
                UnitRole.Flying => Vector3.up * FlyingHoverHeight,
                UnitRole.Super => Vector3.up * SuperGroundClearance,
                _ => Vector3.zero,
            };
    }
}
