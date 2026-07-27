using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    public readonly struct RuntimeDebugConsoleLogEntry
    {
        public RuntimeDebugConsoleLogEntry(
            DateTime timestamp,
            string message,
            string stackTrace,
            LogType type,
            int repeatCount = 1)
        {
            Timestamp = timestamp;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Type = type;
            RepeatCount = Math.Max(1, repeatCount);
        }

        public DateTime Timestamp { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public LogType Type { get; }
        public int RepeatCount { get; }
    }

    public sealed class RuntimeDebugConsoleLogBuffer
    {
        public const int DefaultCapacity = 120;
        public const int MaxMessageChars = 360;
        public const int MaxMessageLines = 4;
        public const int MaxStackTraceLines = 16;
        private const string TruncatedSuffix = " ... [truncated]";
        private const string StackTraceTruncatedSuffix = "... [stack trace truncated]";

        private readonly object _gate = new();
        private readonly Queue<RuntimeDebugConsoleLogEntry> _entries;
        private readonly int _capacity;

        public RuntimeDebugConsoleLogBuffer(int capacity = DefaultCapacity)
        {
            _capacity = Math.Max(1, capacity);
            _entries = new Queue<RuntimeDebugConsoleLogEntry>(_capacity);
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Count;
                }
            }
        }

        public void Add(string message, string stackTrace, LogType type)
        {
            lock (_gate)
            {
                var compactedMessage = CompactText(message, MaxMessageLines, MaxMessageChars, TruncatedSuffix);
                var compactedStack = CompactStackTrace(stackTrace);

                if (_entries.Count > 0)
                {
                    var last = PeekLastUnlocked();
                    if (last.Message == compactedMessage
                        && last.StackTrace == compactedStack
                        && last.Type == type)
                    {
                        ReplaceLastUnlocked(new RuntimeDebugConsoleLogEntry(
                            last.Timestamp,
                            last.Message,
                            last.StackTrace,
                            last.Type,
                            last.RepeatCount + 1));
                        return;
                    }
                }

                while (_entries.Count >= _capacity)
                {
                    _entries.Dequeue();
                }

                _entries.Enqueue(new RuntimeDebugConsoleLogEntry(
                    DateTime.Now,
                    compactedMessage,
                    compactedStack,
                    type));
            }
        }

        public IReadOnlyList<RuntimeDebugConsoleLogEntry> GetSnapshot()
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }

        public string BuildCopyText()
        {
            var snapshot = GetSnapshot();
            if (snapshot.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(snapshot.Count * 96);
            for (var i = 0; i < snapshot.Count; i++)
            {
                AppendEntry(builder, snapshot[i]);
            }

            return builder.ToString().TrimEnd();
        }

        public void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
            }
        }

        private RuntimeDebugConsoleLogEntry PeekLastUnlocked()
        {
            var array = _entries.ToArray();
            return array[array.Length - 1];
        }

        private void ReplaceLastUnlocked(RuntimeDebugConsoleLogEntry entry)
        {
            var array = _entries.ToArray();
            _entries.Clear();
            for (var i = 0; i < array.Length - 1; i++)
            {
                _entries.Enqueue(array[i]);
            }

            _entries.Enqueue(entry);
        }

        private static void AppendEntry(StringBuilder builder, RuntimeDebugConsoleLogEntry entry)
        {
            builder
                .Append('[')
                .Append(entry.Timestamp.ToString("HH:mm:ss"))
                .Append("] [")
                .Append(entry.Type)
                .Append("] ")
                .Append(entry.Message);
            if (entry.RepeatCount > 1)
            {
                builder.Append(" x").Append(entry.RepeatCount);
            }

            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(entry.StackTrace)
                && entry.Type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                builder.AppendLine(entry.StackTrace);
            }
        }

        private static string CompactStackTrace(string stackTrace) =>
            CompactText(
                stackTrace,
                MaxStackTraceLines,
                MaxMessageChars * 6,
                StackTraceTruncatedSuffix);

        private static string CompactText(
            string text,
            int maxLines,
            int maxChars,
            string truncatedSuffix)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            var builder = new StringBuilder(Math.Min(normalized.Length, maxChars) + truncatedSuffix.Length);
            var lineCount = Math.Min(lines.Length, Math.Max(1, maxLines));
            for (var i = 0; i < lineCount; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(lines[i]);
            }

            var truncated = lines.Length > lineCount;
            if (builder.Length > maxChars)
            {
                builder.Length = Math.Max(0, maxChars);
                truncated = true;
            }

            if (truncated)
            {
                builder.Append(truncatedSuffix);
            }

            return builder.ToString();
        }
    }
}
