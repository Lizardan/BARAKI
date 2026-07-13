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
    }
}
