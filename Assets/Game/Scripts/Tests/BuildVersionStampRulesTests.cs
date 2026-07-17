using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BuildVersionStampRulesTests
    {
        [Test]
        public void Normalize_StripsVPrefix()
        {
            Assert.AreEqual("0.1.3", BuildVersionStampRules.Normalize("v0.1.3"));
            Assert.AreEqual("0.1.3", BuildVersionStampRules.Normalize("0.1.3"));
        }

        [Test]
        public void Normalize_RejectsEmpty()
        {
            Assert.IsNull(BuildVersionStampRules.Normalize(""));
            Assert.IsNull(BuildVersionStampRules.Normalize("   "));
        }
    }
}
