using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HostMigrationApplyRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            MatchPauseGate.ResetForTests();
            HostMigrationSession.Clear();
            Time.timeScale = 1f;
        }

        [Test]
        public void TryApplyLastGood_RestoresGoldWithoutEliminatingPreviousHost()
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
            Assert.IsFalse(resume.Players[0].IsEliminated);
        }

        [Test]
        public void TryApplyLastGood_CanEliminatePreviousHostOnKick()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(host));

            var resume = new MatchController();
            resume.StartMatch(MatchConfig.MvpDefault(2));
            resume.BeginEarlyPhase();

            Assert.IsTrue(HostMigrationApplyRules.TryApplyLastGood(
                resume,
                bytes,
                previousHostSlot: 0,
                eliminatePreviousHost: true));
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
            coordinator.BeginStateTransferFromMatch(runtime);

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

        [Test]
        public void TryCaptureState_PrefersLastGoodBytes()
        {
            var lastGood = new byte[] { 1, 2, 3 };
            Assert.IsTrue(HostMigrationApplyRules.TryCaptureState(lastGood, null, out var captured));
            Assert.AreSame(lastGood, captured);
        }

        [Test]
        public void TryCaptureState_FallsBackToLiveController()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 777;

            Assert.IsTrue(HostMigrationApplyRules.TryCaptureState(null, controller, out var captured));
            Assert.IsNotNull(captured);
            Assert.Greater(captured.Length, 0);

            var restored = new MatchController();
            restored.StartMatch(MatchConfig.MvpDefault(2));
            restored.BeginEarlyPhase();
            Assert.IsTrue(HostMigrationApplyRules.TryApplyLastGood(restored, captured, previousHostSlot: 0));
            Assert.AreEqual(777, restored.Players[0].Gold);
        }

        [Test]
        public void TryCaptureState_EmptyWithoutLiveController_Fails()
        {
            Assert.IsFalse(HostMigrationApplyRules.TryCaptureState(null, null, out var captured));
            Assert.IsNull(captured);
            Assert.IsFalse(HostMigrationApplyRules.TryCaptureState(new byte[0], null, out captured));
            Assert.IsNull(captured);
        }
    }
}
