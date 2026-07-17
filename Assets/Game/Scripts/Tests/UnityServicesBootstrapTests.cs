using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnityServicesBootstrapTests
    {
        const string PrefsUgsPlayerName = "baraki.profile.ugsPlayerName";

        static readonly FieldInfo InitGateField = typeof(UnityServicesBootstrap).GetField(
            "s_initGate",
            BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo InitInFlightField = typeof(UnityServicesBootstrap).GetField(
            "s_initInFlight",
            BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void EnsureInitializedAsync_InEditMode_SkipsSdkWithoutMarkingReady()
        {
            Assume.That(Application.isPlaying, Is.False);
            Assume.That(UnityServicesBootstrap.IsReady, Is.False);
            Assume.That(InitGateField, Is.Not.Null);
            Assume.That(InitInFlightField, Is.Not.Null);

            InitInFlightField.SetValue(null, false);
            InitGateField.SetValue(null, null);

            var task = UnityServicesBootstrap.EnsureInitializedAsync();

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.IsFalse(
                UnityServicesBootstrap.IsReady,
                "Edit Mode must not initialize Unity Services.");
        }

        [Test]
        public void PrimeCachedPlayerNameFromPrefs_ExposesValidNameForOfflineUi()
        {
            Assume.That(Application.isPlaying, Is.False);
            Assume.That(UnityServicesBootstrap.IsReady, Is.False);

            var previous = PlayerPrefs.GetString(PrefsUgsPlayerName, string.Empty);
            try
            {
                PlayerPrefs.SetString(PrefsUgsPlayerName, "Lizardan#4821");
                PlayerPrefs.Save();

                UnityServicesBootstrap.PrimeCachedPlayerNameFromPrefs();

                Assert.AreEqual("Lizardan#4821", UnityServicesBootstrap.PlayerName);
                Assert.IsTrue(FriendsHubRules.IsValidUgsPlayerName(UnityServicesBootstrap.PlayerName));
                Assert.That(
                    FriendsHubRules.FormatProfileNameRichText("Lizardan", UnityServicesBootstrap.PlayerName),
                    Does.Contain("#4821"));
            }
            finally
            {
                if (string.IsNullOrEmpty(previous))
                {
                    PlayerPrefs.DeleteKey(PrefsUgsPlayerName);
                }
                else
                {
                    PlayerPrefs.SetString(PrefsUgsPlayerName, previous);
                }

                PlayerPrefs.Save();
                UnityServicesBootstrap.PrimeCachedPlayerNameFromPrefs();
            }
        }

        [Test]
        public void GetRecommendedRetryDelaySeconds_WhenNotCoolingDown_ReturnsMinimum()
        {
            Assume.That(Application.isPlaying, Is.False);
            Assert.AreEqual(2.5f, UnityServicesBootstrap.GetRecommendedRetryDelaySeconds(2.5f), 0.01f);
        }
    }
}
