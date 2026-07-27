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
            Assert.IsTrue(RuntimeDebugConsoleLogFilter.ShouldAccept("careful", LogType.Warning));
            Assert.IsTrue(RuntimeDebugConsoleLogFilter.ShouldAccept("[P] Net.Connected role=Host", LogType.Log));
            Assert.IsFalse(RuntimeDebugConsoleLogFilter.ShouldAccept("UnityServices signed in", LogType.Log));
        }

        [Test]
        public void Buffer_DedupsConsecutiveIdenticalEntries()
        {
            var buffer = new RuntimeDebugConsoleLogBuffer(capacity: 8);
            buffer.Add("[P] Net.Tick", string.Empty, LogType.Log);
            buffer.Add("[P] Net.Tick", string.Empty, LogType.Log);
            buffer.Add("[P] Net.Tick", string.Empty, LogType.Log);

            var entries = buffer.GetSnapshot();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(3, entries[0].RepeatCount);
            StringAssert.Contains("x3", buffer.BuildCopyText());
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
            var net = "name=Vasya role=Client slot=1\nmode=4p room=ABCD phase=RacePick";
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

            var label = DebugPlaytestReportBuilder.BuildDiscordLabel(net, utc);
            StringAssert.Contains("Vasya", label);
            StringAssert.Contains("room=ABCD", label);
            StringAssert.Contains("mode=4p", label);
            StringAssert.Contains("role=Client", label);
            StringAssert.Contains("phase=RacePick", label);
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
