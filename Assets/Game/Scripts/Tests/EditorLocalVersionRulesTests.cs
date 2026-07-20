using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EditorLocalVersionRulesTests
    {
        [Test]
        public void TryBumpPatch_IncrementsPatch()
        {
            Assert.IsTrue(EditorLocalVersionRules.TryBumpPatch("0.1.7", out var next));
            Assert.AreEqual("0.1.8", next);
        }

        [Test]
        public void TryBumpPatch_StripsVPrefix()
        {
            Assert.IsTrue(EditorLocalVersionRules.TryBumpPatch("v0.2.0", out var next));
            Assert.AreEqual("0.2.1", next);
        }

        [Test]
        public void TryBumpPatch_RejectsInvalid()
        {
            Assert.IsFalse(EditorLocalVersionRules.TryBumpPatch("", out _));
            Assert.IsFalse(EditorLocalVersionRules.TryBumpPatch("not-a-version", out _));
        }

        [Test]
        public void Resolve_UsesLatestTagPlusOne()
        {
            Assert.AreEqual("0.1.8", EditorLocalVersionRules.Resolve("v0.1.7", "0.0.1"));
        }

        [Test]
        public void Resolve_FallsBackWhenTagMissing()
        {
            Assert.AreEqual("0.0.1", EditorLocalVersionRules.Resolve(null, "0.0.1"));
            Assert.AreEqual("0.0.1", EditorLocalVersionRules.Resolve("   ", "v0.0.1"));
        }
    }
}
