using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Measures unit mesh height for HP bars / pick colliders.
    /// Ignores particles, trails, and lines so VFX (e.g. titan aura) cannot lift bars into the sky.
    /// </summary>
    public static class UnitVisualHeight
    {
        const float FallbackHeight = 1.8f;

        /// <summary>
        /// World height from <paramref name="feetRoot"/> up to the top of mesh/skinned renderers
        /// under <paramref name="measureFrom"/> (defaults to <paramref name="feetRoot"/>).
        /// </summary>
        public static float MeasureAboveFeet(Transform feetRoot, Transform measureFrom = null)
        {
            if (feetRoot == null)
            {
                return FallbackHeight;
            }

            var searchRoot = measureFrom != null ? measureFrom : feetRoot;
            var renderers = searchRoot.GetComponentsInChildren<Renderer>();
            var hasBounds = false;
            var bounds = default(Bounds);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!ShouldMeasure(renderer))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return FallbackHeight;
            }

            return Mathf.Max(0.1f, bounds.max.y - feetRoot.position.y);
        }

        public static bool ShouldMeasure(Renderer renderer) =>
            renderer != null && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer);
    }
}
