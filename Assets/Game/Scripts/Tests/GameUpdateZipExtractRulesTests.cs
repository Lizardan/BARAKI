using System.IO;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameUpdateZipExtractRulesTests
    {
        [Test]
        public void ProgressForBytes_Clamps()
        {
            Assert.AreEqual(0f, GameUpdateZipExtractRules.ProgressForBytes(0, 100), 0.0001f);
            Assert.AreEqual(0.5f, GameUpdateZipExtractRules.ProgressForBytes(50, 100), 0.0001f);
            Assert.AreEqual(1f, GameUpdateZipExtractRules.ProgressForBytes(100, 100), 0.0001f);
            Assert.AreEqual(0f, GameUpdateZipExtractRules.ProgressForBytes(10, 0), 0.0001f);
        }

        [Test]
        public void TryResolveSafeExtractPath_RejectsZipSlip()
        {
            var root = Path.Combine(Path.GetTempPath(), "baraki-zip-test");
            Assert.IsFalse(
                GameUpdateZipExtractRules.TryResolveSafeExtractPath(
                    root,
                    "../evil.exe",
                    out _,
                    out var error));
            StringAssert.Contains("escapes", error);
        }

        [Test]
        public void TryResolveSafeExtractPath_AcceptsNestedFile()
        {
            var root = Path.Combine(Path.GetTempPath(), "baraki-zip-test");
            Assert.IsTrue(
                GameUpdateZipExtractRules.TryResolveSafeExtractPath(
                    root,
                    Path.Combine("BARAKI_Data", "boot.config"),
                    out var fullPath,
                    out _));
            StringAssert.Contains("boot.config", fullPath);
        }
    }
}
