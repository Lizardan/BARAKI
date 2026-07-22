using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameplayCameraPanControllerTests
    {
        [Test]
        public void ReadEdgeScrollInput_LeftEdge_ReturnsNegativeX()
        {
            var input = GameplayCameraSettings.ReadEdgeScrollInput(new Vector2(10f, 400f), 24f);
            Assert.AreEqual(-1f, input.x, 0.001f);
            Assert.AreEqual(0f, input.y, 0.001f);
        }

        [Test]
        public void ReadEdgeScrollInput_TopEdge_ReturnsPositiveY()
        {
            var input = GameplayCameraSettings.ReadEdgeScrollInput(new Vector2(400f, 1910f), 24f);
            Assert.AreEqual(0f, input.x, 0.001f);
            Assert.AreEqual(1f, input.y, 0.001f);
        }

        [Test]
        public void ComputeEdgePanDirection_UsesCameraAxesOnXZ()
        {
            var cameraObject = new GameObject("TestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(54f, 0f, 0f);

            var direction = GameplayCameraSettings.ComputeEdgePanDirection(camera, Vector2.right);

            Assert.AreEqual(0f, direction.y, 0.001f);
            Assert.AreEqual(1f, direction.x, 0.001f);
            Assert.AreEqual(0f, direction.z, 0.001f);

            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void ClampPanPosition_KeepsYZeroAndWithinBounds()
        {
            var clamped = GameplayCameraSettings.ClampPanPosition(
                new Vector3(200f, 5f, -200f),
                GameplayCameraSettings.DefaultPanBoundsRadius);

            Assert.AreEqual(0f, clamped.y, 0.001f);
            Assert.AreEqual(GameplayCameraSettings.DefaultPanBoundsRadius, clamped.x, 0.001f);
            Assert.AreEqual(-GameplayCameraSettings.DefaultPanBoundsRadius, clamped.z, 0.001f);
        }

        [Test]
        public void FollowOffsetFromZoomDistance_PreservesPitchOnlyNoYaw()
        {
            var offset = GameplayCameraSettings.FollowOffsetFromZoomDistance(100f);

            Assert.AreEqual(0f, offset.x, 0.001f);
            Assert.AreEqual(100f, offset.magnitude, 0.001f);
            Assert.Less(offset.z, 0f);
            Assert.Greater(offset.y, 0f);
        }

        [Test]
        public void FollowOffsetFromZoomDistance_UsesWarcraft3Pitch()
        {
            var offset = GameplayCameraSettings.FollowOffsetFromZoomDistance(100f);
            var pitchDegrees = Mathf.Atan2(offset.y, -offset.z) * Mathf.Rad2Deg;

            Assert.AreEqual(GameplayCameraSettings.DefaultPitchDegrees, pitchDegrees, 0.01f);
            Assert.AreEqual(56f, GameplayCameraSettings.DefaultPitchDegrees, 0.001f);
            Assert.AreEqual(70f, GameplayCameraSettings.DefaultFieldOfViewDegrees, 0.001f);
        }

        [Test]
        public void ClampZoomDistance_RespectsMinMax()
        {
            Assert.AreEqual(16f, GameplayCameraSettings.ClampZoomDistance(10f, 16f, 64f), 0.001f);
            Assert.AreEqual(64f, GameplayCameraSettings.ClampZoomDistance(200f, 16f, 64f), 0.001f);
            Assert.AreEqual(32f, GameplayCameraSettings.ClampZoomDistance(32f, 16f, 64f), 0.001f);
        }

        [Test]
        public void ReadKeyboardPanInput_ArrowUp_ReturnsForward()
        {
            var input = GameplayCameraSettings.ReadKeyboardPanInput(false, false, false, true);
            Assert.AreEqual(0f, input.x, 0.001f);
            Assert.AreEqual(1f, input.y, 0.001f);
        }

        [Test]
        public void ReadKeyboardPanInput_Diagonal_IsNormalized()
        {
            var input = GameplayCameraSettings.ReadKeyboardPanInput(true, false, false, true);
            Assert.AreEqual(-0.7071067f, input.x, 0.001f);
            Assert.AreEqual(0.7071067f, input.y, 0.001f);
        }

        [Test]
        public void GetPlayerBaseFocusPosition_UsesMainBuildingWorldPosition()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var focus = GameplayCameraSettings.GetPlayerBaseFocusPosition(layout, 0);
            var expected = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.Main);

            Assert.AreEqual(expected, focus);
        }

        [Test]
        public void GetPlayerBaseFocusPosition_SlotOnRingMatchesArenaAngle()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var slot2 = layout.Slots[2];

            Assert.AreEqual(-MatchArenaGenerator.DefaultArenaRadius, slot2.BasePosition.x, 0.5f);
            Assert.AreEqual(0f, slot2.BasePosition.z, 0.5f);
        }

        [Test]
        public void CombinePanInput_MergesEdgeAndKeyboard()
        {
            var combined = GameplayCameraSettings.CombinePanInput(Vector2.right, Vector2.up);
            Assert.AreEqual(0.7071067f, combined.x, 0.001f);
            Assert.AreEqual(0.7071067f, combined.y, 0.001f);
        }
    }
}
