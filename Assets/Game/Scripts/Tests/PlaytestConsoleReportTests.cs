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
    }
}
