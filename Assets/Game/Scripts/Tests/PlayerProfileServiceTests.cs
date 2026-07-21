using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PlayerProfileServiceTests
    {
        [Test]
        public void ClampAvatarId_ClampsToValidRange()
        {
            Assert.AreEqual(0, PlayerProfileService.ClampAvatarId(-3));
            Assert.AreEqual(0, PlayerProfileService.ClampAvatarId(0));
            Assert.AreEqual(PlayerProfileService.AvatarCount - 1,
                PlayerProfileService.ClampAvatarId(PlayerProfileService.AvatarCount + 5));
        }

        [Test]
        public void PrimeFromLocalPrefs_LoadsDisplayNameWithoutCloud()
        {
            const string key = "baraki.profile.displayName";
            var previous = PlayerPrefs.GetString(key, string.Empty);
            try
            {
                PlayerPrefs.SetString(key, "Lizardan");
                PlayerPrefs.Save();
                PlayerProfileService.PrimeFromLocalPrefs();
                Assert.AreEqual("Lizardan", PlayerProfileService.DisplayName);
            }
            finally
            {
                if (string.IsNullOrEmpty(previous))
                {
                    PlayerPrefs.DeleteKey(key);
                }
                else
                {
                    PlayerPrefs.SetString(key, previous);
                }

                PlayerPrefs.Save();
            }
        }

        [Test]
        public void GetAvatarGlyph_ReturnsStableNonEmptyValue()
        {
            for (var i = 0; i < PlayerProfileService.AvatarCount; i++)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(PlayerProfileService.GetAvatarGlyph(i)));
            }
        }

        [Test]
        public void GetAvatarGlyph_UsesGeometricDingbats()
        {
            var expected = new[] { "◆", "●", "▲", "■", "✦", "❖", "◉", "▣" };
            Assert.AreEqual(PlayerProfileService.AvatarCount, expected.Length);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], PlayerProfileService.GetAvatarGlyph(i));
            }
        }
    }
}
