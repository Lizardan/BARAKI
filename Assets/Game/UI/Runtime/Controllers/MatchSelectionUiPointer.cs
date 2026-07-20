using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>Pointer helpers for UI Toolkit panels under Input System Only.</summary>
    public static class MatchSelectionUiPointer
    {
        public static bool TryGetScreenPosition(out Vector2 screenPosition)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            var pointer = Pointer.current;
            if (pointer != null)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        /// <summary>
        /// Input System / uGUI use bottom-left origin; UI Toolkit screen space uses top-left.
        /// </summary>
        public static Vector2 ToUiToolkitScreenPosition(Vector2 bottomLeftScreenPosition, float screenHeight) =>
            new(bottomLeftScreenPosition.x, screenHeight - bottomLeftScreenPosition.y);

        public static Vector2 ScreenToPanelPosition(IPanel panel, Vector2 bottomLeftScreenPosition)
        {
            var topLeft = ToUiToolkitScreenPosition(bottomLeftScreenPosition, Screen.height);
            return RuntimePanelUtils.ScreenToPanel(panel, topLeft);
        }

        public static bool IsDescendantOf(VisualElement ancestor, VisualElement element)
        {
            if (ancestor == null)
            {
                return false;
            }

            var current = element;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        public static bool IsPointerOverBlockedUi(
            VisualElement picked,
            VisualElement bottomDock,
            VisualElement topBar,
            VisualElement debugHudPanel = null) =>
            picked != null
            && (IsDescendantOf(bottomDock, picked)
                || IsDescendantOf(topBar, picked)
                || IsDescendantOf(debugHudPanel, picked));
    }
}
