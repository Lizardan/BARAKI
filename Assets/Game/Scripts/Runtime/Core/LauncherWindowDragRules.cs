using UnityEngine;

namespace Game.Core
{
    /// <summary>Pure rules for borderless launcher window drag without breaking UITK clicks.</summary>
    public static class LauncherWindowDragRules
    {
        /// <summary>
        /// Panel pixels before a press becomes a window drag.
        /// Below this, Buttons / Clickable keep a normal click.
        /// </summary>
        public const float DragThresholdPixels = 6f;

        /// <summary>Text input / close chrome: never arm drag (selection &amp; click must win).</summary>
        public static bool ShouldBlockDragArm(bool isTextInput, bool isCloseControl) =>
            isTextInput || isCloseControl;

        public static bool ShouldBeginDrag(Vector2 pressPanelPosition, Vector2 currentPanelPosition) =>
            ShouldBeginDrag(pressPanelPosition, currentPanelPosition, DragThresholdPixels);

        public static bool ShouldBeginDrag(
            Vector2 pressPanelPosition,
            Vector2 currentPanelPosition,
            float thresholdPixels)
        {
            var threshold = Mathf.Max(0f, thresholdPixels);
            return (currentPanelPosition - pressPanelPosition).sqrMagnitude >= threshold * threshold;
        }

        public static Vector2Int OffsetWindowPosition(Vector2Int windowPosition, Vector2Int cursorDelta) =>
            new(windowPosition.x + cursorDelta.x, windowPosition.y + cursorDelta.y);
    }
}
