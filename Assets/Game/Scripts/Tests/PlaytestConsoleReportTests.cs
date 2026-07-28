using System;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PlaytestConsoleReportTests
    {
        [Test]
        public void Filter_AcceptsErrorsAndTaggedInfo_Only()
        {
            Assert.IsTrue(RuntimeDebugConsoleLogFilter.ShouldAccept("boom", LogType.Error));
            Assert.IsFalse(RuntimeDebugConsoleLogFilter.ShouldAccept("careful", LogType.Warning));
            Assert.IsTrue(RuntimeDebugConsoleLogFilter.ShouldAccept("[P] Net.Warn", LogType.Warning));
            Assert.IsTrue(RuntimeDebugConsoleLogFilter.ShouldAccept("[P] Net.Connected role=Host", LogType.Log));
            Assert.IsFalse(RuntimeDebugConsoleLogFilter.ShouldAccept("UnityServices signed in", LogType.Log));
        }

        [Test]
        public void PlaytestLog_Format_UsesStableEventName()
        {
            var line = PlaytestLog.Format("Net", "Connected", ("role", "Client"), ("slot", 1));
            Assert.AreEqual("[P] Net.Connected role=Client slot=1", line);
        }

        [Test]
        public void SendGate_BlocksDuringCooldown()
        {
            var gate = new DebugReportSendGate();
            var now = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(gate.TryBeginSend(now, out _));
            gate.MarkSuccess(now);

            Assert.IsFalse(gate.TryBeginSend(now.AddSeconds(10), out var reason));
            StringAssert.Contains("Подождите", reason);
            Assert.IsTrue(gate.TryBeginSend(now.AddSeconds(61), out _));
        }

        [Test]
        public void ReportBuilder_ContainsSectionsAndLabel()
        {
            var utc = new DateTime(2026, 7, 28, 1, 12, 44, DateTimeKind.Utc);
            var net = "name=Vasya role=Client slot=1\nmode=4p room=ABCD filled=2/4 phase=RacePick matchElapsed=-";
            var report = DebugPlaytestReportBuilder.BuildReport(
                "[01:12:01] [Log] [P] Net.Connected role=Client",
                net,
                utc);

            StringAssert.Contains("=== META ===", report);
            StringAssert.Contains("=== NET ===", report);
            StringAssert.Contains("=== EVENTS ===", report);
            StringAssert.Contains("=== END ===", report);
            StringAssert.Contains("role=Client", report);
            StringAssert.Contains("name=Vasya", report);
            StringAssert.Contains("mode=4p", report);
            StringAssert.Contains("Net.Connected", report);

            var label = DebugPlaytestReportBuilder.BuildIssueTitle(net, utc);
            StringAssert.Contains("Vasya", label);
            StringAssert.Contains("room=ABCD", label);
            StringAssert.Contains("mode=4p", label);
            StringAssert.Contains("role=Client", label);
            StringAssert.Contains("phase=RacePick", label);
            StringAssert.Contains("filled=2/4", label);
            StringAssert.Contains("playtest |", label);
        }

        [Test]
        public void BuildIssueTitle_Match_IncludesElapsed()
        {
            var utc = new DateTime(2026, 7, 28, 1, 12, 44, DateTimeKind.Utc);
            var net = "name=Vasya role=Host slot=0 mode=2p room=ABCD filled=2/2 phase=Match matchElapsed=10+";
            var label = DebugPlaytestReportBuilder.BuildIssueTitle(net, utc);
            StringAssert.Contains("phase=Match", label);
            StringAssert.Contains("filled=2/2", label);
            StringAssert.Contains("elapsed=10+", label);
        }

        [Test]
        public void ReportBuilder_EmptyEvents_StillBuildsSkeleton()
        {
            var report = DebugPlaytestReportBuilder.BuildReport(
                string.Empty,
                "role=Offline phase=Offline",
                DateTime.UtcNow);
            StringAssert.Contains("(empty)", report);
        }
    }
}
