using UnityEngine;

namespace Game.UI
{
    /// <summary>Positions command-button tooltips above the hovered slot.</summary>
    public static class MatchCommandTooltipRules
    {
        public const float GapAboveButton = 10f;

        public static Vector2 GetTooltipTopLeft(
            Rect buttonWorldBound,
            Vector2 tooltipSize,
            float panelWidth)
        {
            var x = buttonWorldBound.center.x - (tooltipSize.x * 0.5f);
            var y = buttonWorldBound.yMin - GapAboveButton - tooltipSize.y;
            if (panelWidth > 0f)
            {
                x = Mathf.Clamp(x, 0f, Mathf.Max(0f, panelWidth - tooltipSize.x));
            }

            if (y < 0f)
            {
                y = buttonWorldBound.yMax + GapAboveButton;
            }

            return new Vector2(x, y);
        }
    }
}
