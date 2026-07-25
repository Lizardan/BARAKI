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
        public const int DefaultCapacity = 300;

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
                    message,
                    stackTrace,
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
    }
}
