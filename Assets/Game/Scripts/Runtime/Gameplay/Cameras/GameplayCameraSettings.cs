using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Cameras
{
    /// <summary>
    /// Shared isometric RTS camera tuning for Cinemachine follow and pan bounds.
    /// </summary>
    public static class GameplayCameraSettings
    {
        /// <summary>World-space follow offset — pitch only (no yaw).</summary>
        public static readonly Vector3 IsometricFollowOffset = new(0f, 72f, -72f);

        /// <summary>Unit direction for pitch-only follow (Y and -Z equal magnitude).</summary>
        public static readonly Vector3 IsometricFollowDirection = new Vector3(0f, 1f, -1f).normalized;

        public const float DefaultZoomDistance = 101.823376f;
        public const float DefaultMinZoomDistance = 64f;
        public const float DefaultMaxZoomDistance = 144f;
        public const float DefaultZoomScrollSpeed = 1.5f;
        public const float DefaultZoomSmoothTime = 0.14f;

        public const float DefaultEdgeScrollThresholdPixels = 24f;
        public const float DefaultPanSpeed = 55f;
        public const float DefaultPanBoundsRadius = MatchArenaGenerator.DefaultArenaRadius + 32f;
        public const float DefaultFocusMoveSpeed = 100f;

        public static Vector3 GetPlayerBaseFocusPosition(MatchArenaLayout layout, int playerSlot)
        {
            if (layout == null)
            {
                throw new System.ArgumentNullException(nameof(layout));
            }

            if (playerSlot < 0 || playerSlot >= layout.Slots.Count)
            {
                throw new System.ArgumentOutOfRangeException(nameof(playerSlot));
            }

            return layout.Slots[playerSlot].GetBuildingWorldPosition(Game.Core.GameIds.Buildings.Main);
        }

        public static Vector3 ComputeEdgePanDirection(Camera camera, Vector2 edgeInput)
        {
            if (camera == null || edgeInput.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            var right = camera.transform.right;
            right.y = 0f;
            right.Normalize();

            var forward = camera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            return (right * edgeInput.x + forward * edgeInput.y).normalized;
        }

        public static Vector2 ReadEdgeScrollInput(Vector2 mousePosition, float edgeThresholdPixels)
        {
            if (edgeThresholdPixels <= 0f)
            {
                return Vector2.zero;
            }

            var input = Vector2.zero;
            var width = Screen.width;
            var height = Screen.height;

            if (mousePosition.x <= edgeThresholdPixels)
            {
                input.x -= 1f;
            }
            else if (mousePosition.x >= width - edgeThresholdPixels)
            {
                input.x += 1f;
            }

            if (mousePosition.y <= edgeThresholdPixels)
            {
                input.y -= 1f;
            }
            else if (mousePosition.y >= height - edgeThresholdPixels)
            {
                input.y += 1f;
            }

            return input;
        }

        public static Vector2 ReadKeyboardPanInput(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            return ReadKeyboardPanInput(
                keyboard.leftArrowKey.isPressed,
                keyboard.rightArrowKey.isPressed,
                keyboard.downArrowKey.isPressed,
                keyboard.upArrowKey.isPressed);
        }

        public static Vector2 ReadKeyboardPanInput(bool left, bool right, bool down, bool up)
        {
            var input = Vector2.zero;
            if (left)
            {
                input.x -= 1f;
            }

            if (right)
            {
                input.x += 1f;
            }

            if (down)
            {
                input.y -= 1f;
            }

            if (up)
            {
                input.y += 1f;
            }

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            return input;
        }

        public static Vector2 CombinePanInput(Vector2 edgeInput, Vector2 keyboardInput)
        {
            var combined = edgeInput + keyboardInput;
            if (combined.sqrMagnitude > 1f)
            {
                combined.Normalize();
            }

            return combined;
        }

        public static Vector3 ClampPanPosition(Vector3 position, float boundsRadius)
        {
            position.y = 0f;
            position.x = Mathf.Clamp(position.x, -boundsRadius, boundsRadius);
            position.z = Mathf.Clamp(position.z, -boundsRadius, boundsRadius);
            return position;
        }

        public static float GetZoomDistanceFromFollowOffset(Vector3 followOffset)
        {
            return followOffset.magnitude;
        }

        public static float ClampZoomDistance(float distance, float minDistance, float maxDistance)
        {
            return Mathf.Clamp(distance, minDistance, maxDistance);
        }

        public static Vector3 FollowOffsetFromZoomDistance(float distance)
        {
            return IsometricFollowDirection * distance;
        }
    }
}
