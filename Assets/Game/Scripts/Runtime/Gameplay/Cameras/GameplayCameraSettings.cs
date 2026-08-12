using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Cameras
{
    /// <summary>Which screen edge the local base should sit on after a compass yaw.</summary>
    public enum CameraBaseScreenEdge
    {
        Bottom = 0,
        Right = 1,
        Top = 2,
        Left = 3,
    }

    /// <summary>
    /// Shared RTS camera tuning matched to Warcraft 3 defaults (AoA 304°, FOV 70°).
    /// </summary>
    public static class GameplayCameraSettings
    {
        /// <summary>
        /// Pitch from horizontal in degrees. WC3 Angle of Attack 304° → 360 − 304 = 56°.
        /// </summary>
        public const float DefaultPitchDegrees = 56f;

        /// <summary>Vertical FOV matching WC3 <c>bj_CAMERA_DEFAULT_FOV</c> / MiscData FOV=70.</summary>
        public const float DefaultFieldOfViewDegrees = 70f;

        /// <summary>Unit direction for pitch-only follow at yaw 0 (camera south of target).</summary>
        public static readonly Vector3 IsometricFollowDirection = CreateFollowDirection(DefaultPitchDegrees);

        /// <summary>World-space follow offset — pitch only, yaw 0.</summary>
        public static readonly Vector3 IsometricFollowOffset =
            IsometricFollowDirection * DefaultZoomDistance;

        public const float DefaultZoomDistance = 32f;
        public const float DefaultMinZoomDistance = 16f;
        public const float DefaultMaxZoomDistance = 40f;
        public const float DefaultZoomScrollSpeed = 1.5f;
        public const float DefaultZoomSmoothTime = 0.14f;

        public const float DefaultEdgeScrollThresholdPixels = 24f;
        public const float DefaultPanSpeed = 55f;
        public const float DefaultPanBoundsRadius = MatchArenaGenerator.DefaultArenaRadius + 32f;
        public const float DefaultFocusMoveSpeed = 100f;
        public const float DefaultMinimapFocusMoveSpeed = 520f;
        public const float DefaultMinimapFocusSmoothTime = 0.04f;
        /// <summary>Very short SmoothDampAngle time for compass / pad yaw turns.</summary>
        public const float DefaultYawSmoothTime = 0.06f;

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

        /// <summary>
        /// Yaw that places <paramref name="baseWorldPosition"/> on the chosen screen edge
        /// (arena center toward the opposite edge).
        /// </summary>
        public static float ComputeYawDegreesForBaseAtScreenEdge(
            Vector3 baseWorldPosition,
            Vector3 arenaCenter,
            CameraBaseScreenEdge edge)
        {
            var toBase = baseWorldPosition - arenaCenter;
            toBase.y = 0f;
            if (toBase.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            // Default view: screen-down is −Z. Rotate so screen-down aligns with toBase.
            var yawBottom = Mathf.Atan2(-toBase.x, -toBase.z) * Mathf.Rad2Deg;
            return edge switch
            {
                CameraBaseScreenEdge.Right => yawBottom + 90f,
                CameraBaseScreenEdge.Top => yawBottom + 180f,
                CameraBaseScreenEdge.Left => yawBottom - 90f,
                _ => yawBottom,
            };
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

        /// <summary>
        /// Horizontal yaw baked into a follow offset (0 = default south-looking isometric).
        /// </summary>
        public static float GetYawDegreesFromFollowOffset(Vector3 followOffset)
        {
            var flat = new Vector3(followOffset.x, 0f, followOffset.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            // RotateY(yaw) * (0,0,-1) = flat → (-sin, -cos) = flat.normalized
            return Mathf.Atan2(-flat.x, -flat.z) * Mathf.Rad2Deg;
        }

        public static float ClampZoomDistance(float distance, float minDistance, float maxDistance)
        {
            return Mathf.Clamp(distance, minDistance, maxDistance);
        }

        public static Vector3 FollowOffsetFromZoomDistance(float distance, float yawDegrees = 0f)
        {
            var direction = Mathf.Abs(yawDegrees) < 0.001f
                ? IsometricFollowDirection
                : Quaternion.Euler(0f, yawDegrees, 0f) * IsometricFollowDirection;
            return direction * distance;
        }

        private static Vector3 CreateFollowDirection(float pitchDegrees)
        {
            var pitchRad = pitchDegrees * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(pitchRad), -Mathf.Cos(pitchRad));
        }
    }
}
