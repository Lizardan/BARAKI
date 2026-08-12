using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchMinimapProjection
    {
        public const float ContentScale = 0.92f;

        public static Vector2 WorldToNormalized(Vector3 worldPosition, float arenaRadius)
        {
            var normalized = WorldToNormalizedUnclamped(worldPosition, arenaRadius);
            return new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }

        /// <summary>
        /// Same mapping as <see cref="WorldToNormalized"/> but allows values outside 0..1
        /// so frustum overlays can extend past the map and be clipped by the panel.
        /// </summary>
        public static Vector2 WorldToNormalizedUnclamped(Vector3 worldPosition, float arenaRadius)
        {
            var halfExtent = Mathf.Max(1f, arenaRadius);
            var x = Mathf.InverseLerp(-halfExtent, halfExtent, worldPosition.x);
            var z = Mathf.InverseLerp(-halfExtent, halfExtent, worldPosition.z);
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

        public static Vector3 NormalizedToWorld(Vector2 normalized, float arenaRadius)
        {
            var halfExtent = Mathf.Max(1f, arenaRadius);
            var x = Mathf.Lerp(-halfExtent, halfExtent, normalized.x);
            var z = Mathf.Lerp(-halfExtent, halfExtent, 1f - normalized.y);
            return new Vector3(x, 0f, z);
        }

        public static Vector3 PanelToWorld(
            Vector2 panelPosition,
            float panelWidth,
            float panelHeight,
            float arenaRadius)
        {
            var normalized = PanelToNormalized(panelPosition, panelWidth, panelHeight);
            return NormalizedToWorld(normalized, arenaRadius);
        }
    }
}
