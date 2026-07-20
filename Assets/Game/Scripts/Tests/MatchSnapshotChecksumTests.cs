using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchSnapshotChecksumTests
    {
        [Test]
        public void Compute_StableForSameSnapshot()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 777;

            var snapshot = MatchSnapshotCodec.Capture(controller);
            Assert.AreNotEqual(0u, snapshot.Checksum);
            Assert.AreEqual(snapshot.Checksum, MatchSnapshotChecksum.Compute(snapshot));
            Assert.IsTrue(MatchSnapshotChecksum.Matches(snapshot, snapshot.Checksum));
        }

        [Test]
        public void RoundTrip_PreservesChecksum()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            var snapshot = MatchSnapshotCodec.Capture(controller);
            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(snapshot));
            Assert.AreEqual(snapshot.Checksum, restored.Checksum);
        }
    }
}
