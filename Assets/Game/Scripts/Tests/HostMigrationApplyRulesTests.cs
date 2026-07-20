using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HostMigrationApplyRulesTests
    {
        [Test]
        public void TryApplyLastGood_RestoresGoldAndEliminatesPreviousHost()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            host.Players[0].Gold = 400;
            host.Players[1].Gold = 250;
            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(host));

            var resume = new MatchController();
            resume.StartMatch(MatchConfig.MvpDefault(2));
            resume.BeginEarlyPhase();

            Assert.IsTrue(HostMigrationApplyRules.TryApplyLastGood(resume, bytes, previousHostSlot: 0));
            Assert.AreEqual(250, resume.Players[1].Gold);
            Assert.IsTrue(resume.Players[0].IsEliminated);
        }

        [Test]
        public void BeginStateTransfer_PrefersLastGoodBytes()
        {
            var go = new GameObject("HostMigrationLastGood");
            var coordinator = go.AddComponent<HostMigrationCoordinator>();
            var runtimeGo = new GameObject("Runtime");
            var runtime = runtimeGo.AddComponent<MatchRuntime>();

            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[1].Gold = 999;
            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(controller));
            runtime.StoreLastNetworkSnapshot(MatchSnapshotCodec.Deserialize(bytes), bytes);

            // Inject controller via reflection-free path: StoreLastNetworkSnapshot is enough for prefer.
            coordinator.BeginHostLost(0, new[] { true, true }, true);
            coordinator.BeginStateTransferFromMatch();

            Assert.IsNotNull(coordinator.CapturedStateBytes);
            Assert.AreEqual(bytes.Length, coordinator.CapturedStateBytes.Length);
            Assert.AreEqual(
                HostMigrationRules.MigrationPhase.TransferringState,
                coordinator.Phase);

            Time.timeScale = 1f;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(runtimeGo);
            HostMigrationSession.Clear();
        }

        [Test]
        public void PreferLastGoodOverLiveCapture_WhenBytesPresent()
        {
            Assert.IsTrue(HostMigrationApplyRules.PreferLastGoodOverLiveCapture(new byte[] { 1 }, true));
            Assert.IsFalse(HostMigrationApplyRules.PreferLastGoodOverLiveCapture(null, true));
        }
    }
}
