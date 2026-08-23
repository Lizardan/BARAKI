using Game.Gameplay.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameplayCameraPreferencesTests
    {
        [Test]
        public void PreferredBaseScreenEdge_RoundTrips()
        {
            var previous = GameplayCameraPreferences.PreferredBaseScreenEdge;
            try
            {
                GameplayCameraPreferences.PreferredBaseScreenEdge = CameraBaseScreenEdge.Left;
                Assert.AreEqual(
                    CameraBaseScreenEdge.Left,
                    GameplayCameraPreferences.PreferredBaseScreenEdge);
                Assert.AreEqual(
                    (int)CameraBaseScreenEdge.Left,
                    PlayerPrefs.GetInt(GameplayCameraPreferences.PrefsKey));
            }
            finally
            {
                GameplayCameraPreferences.PreferredBaseScreenEdge = previous;
            }
        }
    }
}
