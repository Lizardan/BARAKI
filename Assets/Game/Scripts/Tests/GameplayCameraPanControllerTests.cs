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
        public void FollowOffsetFromZoomDistance_AppliesYawAroundY()
        {
            var offset = GameplayCameraSettings.FollowOffsetFromZoomDistance(100f, yawDegrees: 90f);

            Assert.AreEqual(100f, offset.magnitude, 0.001f);
            Assert.AreEqual(90f, GameplayCameraSettings.GetYawDegreesFromFollowOffset(offset), 0.01f);
            Assert.Less(offset.x, 0f);
            Assert.AreEqual(0f, offset.z, 0.001f);
        }

        [Test]
        public void ComputeYawDegreesForBaseAtScreenEdge_Slot0Bottom_IsZero()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var basePos = layout.Slots[0].BasePosition;

            var yaw = GameplayCameraSettings.ComputeYawDegreesForBaseAtScreenEdge(
                basePos,
                Vector3.zero,
                CameraBaseScreenEdge.Bottom);

            Assert.AreEqual(0f, yaw, 0.5f);
        }

        [Test]
        public void ComputeYawDegreesForBaseAtScreenEdge_RightIsBottomPlus90()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var basePos = layout.Slots[1].BasePosition;
            var bottom = GameplayCameraSettings.ComputeYawDegreesForBaseAtScreenEdge(
                basePos,
                Vector3.zero,
                CameraBaseScreenEdge.Bottom);
            var right = GameplayCameraSettings.ComputeYawDegreesForBaseAtScreenEdge(
                basePos,
                Vector3.zero,
                CameraBaseScreenEdge.Right);

            Assert.AreEqual(Mathf.DeltaAngle(0f, bottom + 90f), Mathf.DeltaAngle(0f, right), 0.01f);
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
            Assert.AreEqual(16f, GameplayCameraSettings.ClampZoomDistance(10f, 16f, 40f), 0.001f);
            Assert.AreEqual(40f, GameplayCameraSettings.ClampZoomDistance(200f, 16f, 40f), 0.001f);
            Assert.AreEqual(32f, GameplayCameraSettings.ClampZoomDistance(32f, 16f, 40f), 0.001f);
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
            var slot3 = layout.Slots[3];

            Assert.AreEqual(-MatchArenaGenerator.DefaultArenaRadius, slot3.BasePosition.x, 0.5f);
            Assert.AreEqual(0f, slot3.BasePosition.z, 0.5f);
        }

        [Test]
        public void CombinePanInput_MergesEdgeAndKeyboard()
        {
            var combined = GameplayCameraSettings.CombinePanInput(Vector2.right, Vector2.up);
            Assert.AreEqual(0.7071067f, combined.x, 0.001f);
            Assert.AreEqual(0.7071067f, combined.y, 0.001f);
        }

        [Test]
        public void TryRayToGround_HitsYZeroPlane()
        {
            var ray = new Ray(new Vector3(10f, 20f, -5f), Vector3.down);
            Assert.IsTrue(GameplayCameraGroundView.TryRayToGround(ray, out var hit));
            Assert.AreEqual(10f, hit.x, 0.001f);
            Assert.AreEqual(0f, hit.y, 0.001f);
            Assert.AreEqual(-5f, hit.z, 0.001f);
        }

        [Test]
        public void TryRayToGround_ParallelRayFails()
        {
            var ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.forward);
            Assert.IsFalse(GameplayCameraGroundView.TryRayToGround(ray, out _));
        }

        [Test]
        public void TryGetGroundFrustumCorners_ReturnsFourGroundHits()
        {
            var cameraObject = new GameObject("FrustumTestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 40f, -30f);
            camera.transform.rotation = Quaternion.Euler(GameplayCameraSettings.DefaultPitchDegrees, 0f, 0f);
            camera.fieldOfView = GameplayCameraSettings.DefaultFieldOfViewDegrees;

            var corners = new Vector3[4];
            Assert.IsTrue(GameplayCameraGroundView.TryGetGroundFrustumCorners(camera, corners));
            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(0f, corners[i].y, 0.001f);
            }

            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void TryProjectViewportToGround_CenterRayHitsNearLookTarget()
        {
            var position = new Vector3(0f, 26.529202f, -17.894173f);
            var rotation = Quaternion.LookRotation(Vector3.zero - position, Vector3.up);
            var corners = new Vector3[4];

            Assert.IsTrue(GameplayCameraGroundView.TryProjectViewportToGround(
                position,
                rotation,
                GameplayCameraSettings.DefaultFieldOfViewDegrees,
                16f / 9f,
                corners));

            var center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            Assert.AreEqual(0f, center.y, 0.001f);
            Assert.Less(Mathf.Abs(center.x), 35f);
            Assert.Less(corners[2].z - corners[0].z, 220f);
        }

        [Test]
        public void BuildAxisAlignedRectangle_UsesMinMaxXZ()
        {
            var frustum = new[]
            {
                new Vector3(-10f, 0f, -5f),
                new Vector3(12f, 0f, -8f),
                new Vector3(8f, 0f, 20f),
                new Vector3(-14f, 0f, 15f),
            };
            var rect = new Vector3[4];
            GameplayCameraGroundView.BuildAxisAlignedRectangle(frustum, rect);

            Assert.AreEqual(new Vector3(-14f, 0f, -8f), rect[0]);
            Assert.AreEqual(new Vector3(12f, 0f, -8f), rect[1]);
            Assert.AreEqual(new Vector3(12f, 0f, 20f), rect[2]);
            Assert.AreEqual(new Vector3(-14f, 0f, 20f), rect[3]);
        }

        [Test]
        public void MidEdgeRectangle_IsNarrowerThanCornerAabbHorizontally()
        {
            var position = new Vector3(0f, 26.529202f, -17.894173f);
            var rotation = Quaternion.LookRotation(Vector3.zero - position, Vector3.up);
            const float aspect = 16f / 9f;
            const float fov = GameplayCameraSettings.DefaultFieldOfViewDegrees;

            var cornerHits = new Vector3[4];
            Assert.IsTrue(GameplayCameraGroundView.TryProjectViewportToGround(
                position, rotation, fov, aspect, cornerHits));
            var cornerRect = new Vector3[4];
            GameplayCameraGroundView.BuildAxisAlignedRectangle(cornerHits, cornerRect);
            var cornerWidth = cornerRect[1].x - cornerRect[0].x;

            Assert.IsTrue(GameplayCameraGroundView.TryViewportPointToGround(
                position, rotation, fov, aspect, new Vector2(0f, 0.5f), out var left));
            Assert.IsTrue(GameplayCameraGroundView.TryViewportPointToGround(
                position, rotation, fov, aspect, new Vector2(1f, 0.5f), out var right));
            Assert.IsTrue(GameplayCameraGroundView.TryViewportPointToGround(
                position, rotation, fov, aspect, new Vector2(0.5f, 0f), out var bottom));
            Assert.IsTrue(GameplayCameraGroundView.TryViewportPointToGround(
                position, rotation, fov, aspect, new Vector2(0.5f, 1f), out var top));

            var midHits = new[] { left, right, bottom, top };
            var midRect = new Vector3[4];
            GameplayCameraGroundView.BuildAxisAlignedRectangle(midHits, midRect);
            var midWidth = midRect[1].x - midRect[0].x;

            Assert.Less(midWidth, cornerWidth - 1f);
        }
    }
}
