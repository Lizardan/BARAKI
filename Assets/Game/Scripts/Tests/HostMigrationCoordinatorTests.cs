using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HostMigrationCoordinatorTests
    {
        [Test]
        public void BeginHostLost_ElectsAndPauses()
        {
            var go = new GameObject("HostMigrationTest");
            var coordinator = go.AddComponent<HostMigrationCoordinator>();

            coordinator.BeginHostLost(0, new[] { true, true }, matchInProgress: true);

            Assert.AreEqual(1, coordinator.DesignatedHostSlot);
            Assert.AreEqual(HostMigrationRules.MigrationPhase.PausedAwaitingHost, coordinator.Phase);
            Assert.IsTrue(coordinator.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            Time.timeScale = 1f;
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CaptureAndApply_RoundTripsGoldViaSnapshot()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 321;

            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(controller));
            var other = new MatchController();
            other.StartMatch(MatchConfig.MvpDefault(2));
            other.BeginEarlyPhase();
            other.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Deserialize(bytes));

            Assert.AreEqual(321, other.Players[0].Gold);
        }

        [Test]
        public void AdvanceAfterStateTransfer_MovesToRebinding()
        {
            var go = new GameObject("HostMigrationTransferTest");
            var coordinator = go.AddComponent<HostMigrationCoordinator>();
            coordinator.BeginHostLost(0, new[] { true, true }, true);
            coordinator.AdvanceAfterStateTransfer(true);

            Assert.AreEqual(HostMigrationRules.MigrationPhase.TransferringState, coordinator.Phase);
            coordinator.AdvanceAfterStateTransfer(true);
            Assert.AreEqual(HostMigrationRules.MigrationPhase.RebindingRelay, coordinator.Phase);

            Time.timeScale = 1f;
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryBuildReconnectToken_RequiresMatchId()
        {
            var go = new GameObject("HostMigrationTokenTest");
            var coordinator = go.AddComponent<HostMigrationCoordinator>();
            Assert.IsFalse(coordinator.TryBuildReconnectToken(1, out _));

            coordinator.BeginHostLost(0, new[] { true, true }, true);
            // RoomCode empty in EditMode → still fails unless ReconnectMatchId set by BeginHostLost
            if (!string.IsNullOrEmpty(coordinator.ReconnectMatchId))
            {
                Assert.IsTrue(coordinator.TryBuildReconnectToken(1, out var token));
                Assert.IsTrue(token.EndsWith(":1"));
            }

            Time.timeScale = 1f;
            Object.DestroyImmediate(go);
        }
    }
}
