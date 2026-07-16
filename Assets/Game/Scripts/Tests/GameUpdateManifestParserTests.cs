using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameUpdateManifestParserTests
    {
        [Test]
        public void TryParse_ValidJson_ReadsFields()
        {
            const string json =
                "{\"version\":\"0.3.1\",\"minVersion\":\"0.3.1\",\"url\":\"https://x/a.zip\",\"sha256\":\"abc\"}";
            Assert.IsTrue(GameUpdateManifestParser.TryParse(json, out var manifest));
            Assert.AreEqual("0.3.1", manifest.version);
            Assert.AreEqual("0.3.1", manifest.EffectiveMinVersion);
            Assert.AreEqual("https://x/a.zip", manifest.url);
        }

        [Test]
        public void TryParse_Empty_ReturnsFalse()
        {
            Assert.IsFalse(GameUpdateManifestParser.TryParse("", out _));
        }
    }
}
