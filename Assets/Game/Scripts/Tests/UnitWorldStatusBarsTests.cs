using NUnit.Framework;
using UnityEngine;
using Game.Gameplay.Match;

namespace Game.Tests
{
    public sealed class UnitWorldStatusBarsTests
    {
        [Test]
        public void ResolvePitchOnlyBillboard_DoesNotChangeYaw()
        {
            const float lockedYaw = 40f;
            var barPos = new Vector3(0f, 2f, 0f);
            var cameraPos = new Vector3(5f, 8f, -3f);

            var rotation = UnitWorldStatusBars.ResolvePitchOnlyBillboard(barPos, cameraPos, lockedYaw);
            var yaw = rotation.eulerAngles.y;

            Assert.AreEqual(lockedYaw, yaw, 0.05f);
            Assert.AreEqual(0f, rotation.eulerAngles.z, 0.05f);
        }

        [Test]
        public void ResolvePitchOnlyBillboard_PitchesTowardElevatedCamera()
        {
            var barPos = Vector3.zero;
            var cameraPos = new Vector3(0f, 4f, -4f);

            var rotation = UnitWorldStatusBars.ResolvePitchOnlyBillboard(barPos, cameraPos, lockedYawDegrees: 0f);
            var pitch = rotation.eulerAngles.x;
            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            // Camera at 45° elevation → pitch ≈ +45°.
            Assert.AreEqual(45f, pitch, 0.5f);
        }

        [Test]
        public void ResolvePitchOnlyBillboard_SameHeight_HasNoPitch()
        {
            var rotation = UnitWorldStatusBars.ResolvePitchOnlyBillboard(
                Vector3.zero,
                new Vector3(3f, 0f, -2f),
                lockedYawDegrees: 15f);

            var pitch = rotation.eulerAngles.x;
            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            Assert.AreEqual(0f, pitch, 0.05f);
            Assert.AreEqual(15f, rotation.eulerAngles.y, 0.05f);
        }
    }
}
