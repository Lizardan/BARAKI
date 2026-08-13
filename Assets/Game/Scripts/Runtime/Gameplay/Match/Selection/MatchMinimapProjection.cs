using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchMinimapProjection
    {
        public const float ContentScale = 0.92f;

        /// <summary>
        /// Extra world space beyond the arena ring that the minimap projection must cover so the
        /// outer base pads (greybox base arena shifted toward the map edge) stay fully visible.
        /// Without it, <see cref="WorldToNormalized"/> clamps the pads and the clamped corners
        /// shear/tear as the camera yaws.
        /// </summary>
        public static float MapOuterMargin =>
            MatchArenaGreyboxBuilder.BaseArenaOutwardOffset
            + MatchArenaGreyboxBuilder.BaseArenaDepth * 0.5f
            + 2f;

        /// <summary>Half extent shared by every minimap mapping (geometry, blips, viewport, click-pan).</summary>
        public static float MapHalfExtent(float arenaRadius) =>
            Mathf.Max(1f, arenaRadius + MapOuterMargin);

        public static Vector2 WorldToNormalized(
            Vector3 worldPosition,
            float arenaRadius,
            float viewYawDegrees = 0f)
        {
            var normalized = WorldToNormalizedUnclamped(worldPosition, arenaRadius, viewYawDegrees);
            return new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }

        /// <summary>
        /// Same mapping as <see cref="WorldToNormalized"/> but allows values outside 0..1
        /// so frustum overlays can extend past the map and be clipped by the panel.
        /// <paramref name="viewYawDegrees"/> matches the gameplay camera yaw so the minimap
        /// rotates with the view (world is rotated into view space before mapping).
        /// </summary>
        public static Vector2 WorldToNormalizedUnclamped(
            Vector3 worldPosition,
            float arenaRadius,
            float viewYawDegrees = 0f)
        {
            var view = RotateYaw(worldPosition, viewYawDegrees);
            var halfExtent = Mathf.Max(1f, arenaRadius);
            var x = Mathf.InverseLerp(-halfExtent, halfExtent, view.x);
            var z = Mathf.InverseLerp(-halfExtent, halfExtent, view.z);
            return new Vector2(x, 1f - z);
        }

        public static Vector2 ApplyContentInset(Vector2 normalized)
        {
            return (normalized - Vector2.one * 0.5f) * ContentScale + Vector2.one * 0.5f;
        }

        public static Vector2 RemoveContentInset(Vector2 insetNormalized)
        {
            return (insetNormalized - Vector2.one * 0.5f) / ContentScale + Vector2.one * 0.5f;
        }

        public static Vector2 NormalizedToPanel(Vector2 normalized, float panelWidth, float panelHeight)
        {
            var inset = ApplyContentInset(normalized);
            return new Vector2(
                inset.x * panelWidth,
                inset.y * panelHeight);
        }

        public static Vector2 PanelToNormalized(Vector2 panelPosition, float panelWidth, float panelHeight)
        {
            var width = Mathf.Max(1f, panelWidth);
            var height = Mathf.Max(1f, panelHeight);
            var inset = new Vector2(panelPosition.x / width, panelPosition.y / height);
            return RemoveContentInset(inset);
        }

        public static Vector3 NormalizedToWorld(
            Vector2 normalized,
            float arenaRadius,
            float viewYawDegrees = 0f)
        {
            var halfExtent = Mathf.Max(1f, arenaRadius);
            var x = Mathf.Lerp(-halfExtent, halfExtent, normalized.x);
            var z = Mathf.Lerp(-halfExtent, halfExtent, 1f - normalized.y);
            return RotateYaw(new Vector3(x, 0f, z), -viewYawDegrees);
        }

        public static Vector3 PanelToWorld(
            Vector2 panelPosition,
            float panelWidth,
            float panelHeight,
            float arenaRadius,
            float viewYawDegrees = 0f)
        {
            var normalized = PanelToNormalized(panelPosition, panelWidth, panelHeight);
            return NormalizedToWorld(normalized, arenaRadius, viewYawDegrees);
        }

        /// <summary>Y-axis rotation used to align world ↔ camera/minimap view yaw.</summary>
        public static Vector3 RotateYaw(Vector3 world, float yawDegrees)
        {
            if (Mathf.Abs(yawDegrees) < 0.001f)
            {
                return world;
            }

            var rad = yawDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector3(
                world.x * cos - world.z * sin,
                world.y,
                world.x * sin + world.z * cos);
        }
    }
}
