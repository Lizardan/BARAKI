using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RuntimeDebugConsoleLogBufferTests
    {
        [Test]
        public void Add_TrimsOldestEntries_WhenCapacityExceeded()
        {
            var buffer = new RuntimeDebugConsoleLogBuffer(capacity: 2);

            buffer.Add("first", string.Empty, LogType.Log);
            buffer.Add("second", string.Empty, LogType.Warning);
            buffer.Add("third", string.Empty, LogType.Error);

            var entries = buffer.GetSnapshot();
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("second", entries[0].Message);
            Assert.AreEqual("third", entries[1].Message);
        }

        [Test]
        public void BuildCopyText_IncludesTypeMessageAndStackTrace()
        {
            var buffer = new RuntimeDebugConsoleLogBuffer(capacity: 4);

            buffer.Add("boom", "at Test()", LogType.Exception);

            var text = buffer.BuildCopyText();
            StringAssert.Contains("[Exception]", text);
            StringAssert.Contains("boom", text);
            StringAssert.Contains("at Test()", text);
        }

        [Test]
        public void Add_CompactsLongMessagesForDisplayAndCopy()
        {
            var buffer = new RuntimeDebugConsoleLogBuffer(capacity: 4);
            var longMessage = new string('x', RuntimeDebugConsoleLogBuffer.MaxMessageChars + 40);

            buffer.Add(longMessage, string.Empty, LogType.Log);

            var entry = buffer.GetSnapshot()[0];
            Assert.Less(entry.Message.Length, longMessage.Length);
            StringAssert.Contains("truncated", entry.Message);

            var copyText = buffer.BuildCopyText();
            Assert.Less(copyText.Length, longMessage.Length + 32);
            StringAssert.Contains("truncated", copyText);
        }

        [Test]
        public void Add_CompactsLongStackTraces()
        {
            var buffer = new RuntimeDebugConsoleLogBuffer(capacity: 4);
            var stackTrace = string.Empty;
            for (var i = 0; i < RuntimeDebugConsoleLogBuffer.MaxStackTraceLines + 4; i++)
            {
                stackTrace += $"at Frame{i}()\n";
            }

            buffer.Add("boom", stackTrace, LogType.Exception);

            var entry = buffer.GetSnapshot()[0];
            StringAssert.Contains("at Frame0()", entry.StackTrace);
            StringAssert.Contains($"at Frame{RuntimeDebugConsoleLogBuffer.MaxStackTraceLines - 1}()", entry.StackTrace);
            StringAssert.DoesNotContain($"at Frame{RuntimeDebugConsoleLogBuffer.MaxStackTraceLines}()", entry.StackTrace);
            StringAssert.Contains("stack trace truncated", entry.StackTrace);
        }

        [Test]
        public void Clear_RemovesEntries()
        {
            var buffer = new RuntimeDebugConsoleLogBuffer(capacity: 4);
            buffer.Add("message", string.Empty, LogType.Log);

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(string.Empty, buffer.BuildCopyText());
        }
    }
}
