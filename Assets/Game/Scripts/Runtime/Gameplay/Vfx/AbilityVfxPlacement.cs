using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Vfx
{
    /// <summary>
    /// Shared spawn height / scale / lifetime for ability VFX.
    /// Match presenter and BARAKI Studio both call these so preview matches combat.
    /// </summary>
    public static class AbilityVfxPlacement
    {
        /// <summary>World lift from unit feet to the body center (Caster and Target follow anchors).</summary>
        public const float BodyHeight = 0.9f;

        /// <summary>Absolute world Y for Ground rings (Frost / Consecration).</summary>
        public const float GroundY = 0.1f;

        /// <summary>Fallback Y when impact/center has no height (matches presenter Elevate).</summary>
        public const float ElevateY = 0.6f;

        /// <summary>Safety-net <c>Destroy</c> for one-shot casts in the presenter.</summary>
        public const float CastLifetimeSeconds = 3f;

        public static Vector3 ResolveWorld(
            AbilityVfxAnchor anchor,
            Vector3 casterFeet,
            Vector3 targetFeet,
            Vector3 impactPosition = default) =>
            anchor switch
            {
                AbilityVfxAnchor.Target => targetFeet + Vector3.up * BodyHeight,
                AbilityVfxAnchor.Ground => SnapGroundY(casterFeet),
                AbilityVfxAnchor.Impact => Elevate(impactPosition),
                _ => casterFeet + Vector3.up * BodyHeight,
            };

        /// <summary>
        /// Caster / Target parent to the unit and travel with it.
        /// Ground / Impact stay in the world at the cast-time point.
        /// </summary>
        public static bool FollowsHost(AbilityVfxAnchor anchor) =>
            anchor == AbilityVfxAnchor.Caster || anchor == AbilityVfxAnchor.Target;

        public static Vector3 ResolveFollowLocalPosition(AbilityVfxAnchor anchor) =>
            FollowsHost(anchor) ? Vector3.up * BodyHeight : Vector3.zero;

        public static Quaternion ResolveAuthoredRotation(Vector3 euler, Quaternion prefabRotation) =>
            Quaternion.Euler(euler) * prefabRotation;

        /// <summary>
        /// Parents a one-shot to the caster/target root, or leaves it in the world (optional match root).
        /// </summary>
        public static void ApplyOneShotTransform(
            Transform instance,
            AbilityVfxAnchor anchor,
            Transform casterRoot,
            Transform targetRoot,
            Vector3 impactWorld,
            Vector3 euler,
            Quaternion prefabRotation,
            Transform worldParent = null)
        {
            if (instance == null)
            {
                return;
            }

            var rotation = ResolveAuthoredRotation(euler, prefabRotation);
            var host = FollowsHost(anchor)
                ? (anchor == AbilityVfxAnchor.Target ? targetRoot : casterRoot)
                : null;
            if (host != null)
            {
                instance.SetParent(host, false);
                instance.localPosition = ResolveFollowLocalPosition(anchor);
                instance.localRotation = rotation;
                return;
            }

            if (worldParent != null)
            {
                instance.SetParent(worldParent, true);
            }

            var casterFeet = casterRoot != null ? casterRoot.position : impactWorld;
            var targetFeet = targetRoot != null ? targetRoot.position : impactWorld;
            instance.position = ResolveWorld(anchor, casterFeet, targetFeet, impactWorld);
            instance.rotation = rotation;
        }

        public static Vector3 SnapGroundY(Vector3 position) =>
            new(position.x, GroundY, position.z);

        public static Vector3 Elevate(Vector3 position) =>
            position.y > 0.05f ? position : new Vector3(position.x, ElevateY, position.z);

        /// <summary>
        /// Preview stand-in for <c>cast.CenterPosition</c>: Last Call at caster feet (death root),
        /// Catapult / building smite at the target.
        /// </summary>
        public static Vector3 ResolvePreviewImpact(int abilityId, Vector3 casterFeet, Vector3 targetFeet) =>
            abilityId == AbilityIds.FlyingSpawn ? casterFeet : targetFeet;

        public static float ResolveAuraRadius(float defRadius) =>
            defRadius > 0f ? defRadius : HeroAbilityRules.AuraRadius;

        public static float ResolveFrostRadius(float defRadius) =>
            defRadius > 0f ? defRadius : CasterSpellRules.FrostRadius;

        /// <summary>
        /// One-shot visual size: authored prefab scale × <see cref="AbilityFx.Scale"/>.
        /// Combat radius (Frost AoE, etc.) must not change the effect — same prefab + Scale
        /// look the same on every ability.
        /// </summary>
        public static Vector3 ResolveOneShotLocalScale(GameObject prefab, float visualScale)
        {
            var source = prefab != null ? prefab.transform.localScale : Vector3.one;
            return source * Mathf.Max(0.01f, visualScale);
        }

        /// <summary>
        /// How long a one-shot stays before the presenter destroys it (and the viewer replays).
        /// Frost follows stun; everything else uses the 3s safety net.
        /// </summary>
        public static float ResolveOneShotLifetimeSeconds(int abilityId, float stunSeconds)
        {
            if (abilityId == AbilityIds.Frost)
            {
                return stunSeconds > 0f ? stunSeconds : CasterSpellRules.FrostFreezeSeconds;
            }

            return CastLifetimeSeconds;
        }
    }
}
