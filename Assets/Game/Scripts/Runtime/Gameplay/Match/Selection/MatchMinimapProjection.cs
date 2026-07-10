using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchMinimapProjection
    {
        public const float ContentScale = 0.92f;

        public static Vector2 WorldToNormalized(Vector3 worldPosition, float arenaRadius)
        {
            var halfExtent = Mathf.Max(1f, arenaRadius);
            var x = Mathf.InverseLerp(-halfExtent, halfExtent, worldPosition.x);
            var z = Mathf.InverseLerp(-halfExtent, halfExtent, worldPosition.z);
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(1f - z));
        }

        public static Vector2 ApplyContentInset(Vector2 normalized)
        {
            return (normalized - Vector2.one * 0.5f) * ContentScale + Vector2.one * 0.5f;
        }

        public static Vector2 NormalizedToPanel(Vector2 normalized, float panelWidth, float panelHeight)
        {
            var inset = ApplyContentInset(normalized);
            return new Vector2(
                inset.x * panelWidth,
                inset.y * panelHeight);
        }
    }
}
