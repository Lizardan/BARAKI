using Game.Gameplay.Networking;
using NUnit.Framework;

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
        public void GetAvatarGlyph_ReturnsStableNonEmptyValue()
        {
            for (var i = 0; i < PlayerProfileService.AvatarCount; i++)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(PlayerProfileService.GetAvatarGlyph(i)));
            }
        }
    }
}
