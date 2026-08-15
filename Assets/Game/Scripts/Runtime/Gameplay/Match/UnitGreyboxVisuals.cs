using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Shared greybox unit visual scale for presenter prefabs.</summary>
    public static class UnitGreyboxVisuals
    {
        public const float Scale = 2f;

        /// <summary>Animated Human models are authored larger than greybox capsules.</summary>
        public const float AnimatedHumanScaleFactor = 0.625f;

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

        /// <summary>Hero model is raised slightly so its status bar sits above the crown.</summary>
        public const float HeroVisualHeight = 0.15f;

        /// <summary>Hero world scale relative to a regular creep (melee baseline).</summary>
        public const float HeroVsCreepScale = 1.15f;

        /// <summary>Titan world scale relative to a regular creep (melee baseline).</summary>
        public const float TitanVsCreepScale = 3f;

        /// <summary>Local scale of the looping divine burn FX parented under the titan root.</summary>
        public const float TitanAuraFxScale = 0.5f;

        /// <summary>Extra multiplier on top of shared Human animated scale for champions.</summary>
        public static float GetChampionVisualScale(UnitRole role) =>
            role switch
            {
                UnitRole.Hero => HeroVsCreepScale,
                UnitRole.Titan => TitanVsCreepScale,
                _ => 1f,
            };

        /// <summary>
        /// Authored meshes face +X; match locomotion faces +Z. Prefab yaw aligns model forward.
        /// </summary>
        public const float AnimatedHumanModelYawDegrees = 90f;

        /// <summary>Flying (airship) uses the same +X→+Z yaw as other Human units.</summary>
        public const float AnimatedHumanFlyingModelYawDegrees = 90f;

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

        /// <summary>Prefab root euler for Human animated models (yaw only).</summary>
        public static Vector3 GetAnimatedHumanModelEuler(UnitRole role)
        {
            var yaw = GetAnimatedHumanModelYawDegrees(role);
            return new Vector3(0f, yaw, 0f);
        }

        /// <summary>Local model offset applied by the combat presenter.</summary>
        public static Vector3 GetModelLocalOffset(UnitRole role) =>
            role switch
            {
                UnitRole.Flying => Vector3.up * FlyingHoverHeight,
                UnitRole.Hero => Vector3.up * HeroVisualHeight,
                _ => Vector3.zero,
            };
    }
}
