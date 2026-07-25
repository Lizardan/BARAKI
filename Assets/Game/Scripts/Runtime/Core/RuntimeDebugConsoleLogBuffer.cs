using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    public readonly struct RuntimeDebugConsoleLogEntry
    {
        public RuntimeDebugConsoleLogEntry(DateTime timestamp, string message, string stackTrace, LogType type)
        {
            Timestamp = timestamp;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Type = type;
        }

        public DateTime Timestamp { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public LogType Type { get; }
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
                while (_entries.Count >= _capacity)
                {
                    _entries.Dequeue();
                }

                _entries.Enqueue(new RuntimeDebugConsoleLogEntry(
                    DateTime.Now,
                    CompactText(message, MaxMessageLines, MaxMessageChars, TruncatedSuffix),
                    CompactStackTrace(stackTrace),
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
                var entry = snapshot[i];
                builder
                    .Append('[')
                    .Append(entry.Timestamp.ToString("HH:mm:ss"))
                    .Append("] [")
                    .Append(entry.Type)
                    .Append("] ")
                    .AppendLine(entry.Message);

                if (!string.IsNullOrWhiteSpace(entry.StackTrace)
                    && entry.Type is LogType.Error or LogType.Exception or LogType.Assert)
                {
                    builder.AppendLine(entry.StackTrace);
                }
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
            var truncated = false;
            var lineCount = Math.Min(lines.Length, Math.Max(1, maxLines));
            for (var i = 0; i < lineCount; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(lines[i]);
            }

            truncated = lines.Length > lineCount;
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
