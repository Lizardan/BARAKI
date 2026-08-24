using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SnapshotSyncRulesTests
    {
        [Test]
        public void VersionGate_MixedBuilds_AreIncompatible()
        {
            const int current = MatchSnapshotCodec.CurrentVersion;
            Assert.IsTrue(SnapshotVersionGate.IsCompatible(current, current));
            Assert.IsTrue(SnapshotVersionGate.IsCompatible(current, SnapshotVersionGate.Unknown),
                "Legacy lobby without a published version must not block.");
            Assert.IsFalse(SnapshotVersionGate.IsCompatible(current, current - 1));
            Assert.IsFalse(SnapshotVersionGate.IsCompatible(current, current + 1));
        }

        [Test]
        public void VersionGate_StartRequiresExactServerVersion()
        {
            Assert.IsFalse(SnapshotVersionGate.CanStartMatch(0), "Uninitialized lobby must not start.");
            Assert.IsTrue(SnapshotVersionGate.CanStartMatch(MatchSnapshotCodec.CurrentVersion));
            Assert.IsFalse(SnapshotVersionGate.CanStartMatch(MatchSnapshotCodec.CurrentVersion - 1));
        }

        [Test]
        public void DesyncRules_FirstMismatchesResync_PersistentReporterKicked()
        {
            Assert.IsFalse(SnapshotDesyncRules.ShouldResync(0));
            Assert.IsFalse(SnapshotDesyncRules.ShouldResync(SnapshotDesyncRules.ResyncAfterReports - 1));
            Assert.IsTrue(SnapshotDesyncRules.ShouldResync(SnapshotDesyncRules.ResyncAfterReports));
            Assert.IsFalse(SnapshotDesyncRules.ShouldKick(SnapshotDesyncRules.KickAfterReports - 1));
            Assert.IsTrue(SnapshotDesyncRules.ShouldKick(SnapshotDesyncRules.KickAfterReports));
        }

        [Test]
        public void DesyncRules_ReportCounter_ResetsAfterWindow()
        {
            Assert.AreEqual(1, SnapshotDesyncRules.NextReportCount(0, 0f));
            Assert.AreEqual(4, SnapshotDesyncRules.NextReportCount(3, 10f));
            Assert.AreEqual(
                1,
                SnapshotDesyncRules.NextReportCount(SnapshotDesyncRules.KickAfterReports - 1,
                    SnapshotDesyncRules.WindowSeconds + 1f),
                "Old reports must not accumulate into a kick forever.");
            Assert.AreEqual(
                5,
                SnapshotDesyncRules.NextReportCount(4, SnapshotDesyncRules.WindowSeconds));
        }
    }
}
