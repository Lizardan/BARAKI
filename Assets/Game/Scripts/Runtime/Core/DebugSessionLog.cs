using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.Core
{
    public static class DebugSessionLog
    {
        private const string SessionId = "509549";
        private const string RunId = "pre-fix";
        private const string LogPath = "f:/Unity Projects/BARAKI/debug-509549.log";

        public static void Write(
            string location,
            string message,
            string hypothesisId,
            params (string Key, object Value)[] data)
        {
            try
            {
                var builder = new StringBuilder(256);
                builder
                    .Append('{')
                    .Append("\"sessionId\":\"").Append(SessionId).Append("\",")
                    .Append("\"runId\":\"").Append(RunId).Append("\",")
                    .Append("\"hypothesisId\":\"").Append(Escape(hypothesisId)).Append("\",")
                    .Append("\"location\":\"").Append(Escape(location)).Append("\",")
                    .Append("\"message\":\"").Append(Escape(message)).Append("\",")
                    .Append("\"timestamp\":")
                    .Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture))
                    .Append(",\"data\":{");

                for (var i = 0; i < data.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder
                        .Append('"')
                        .Append(Escape(data[i].Key))
                        .Append("\":")
                        .Append(FormatValue(data[i].Value));
                }

                builder.Append("}}");
                File.AppendAllText(LogPath, builder + Environment.NewLine);
            }
            catch
            {
                // Debug instrumentation must never affect gameplay.
            }
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                float f => f.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => "\"" + Escape(value.ToString()) + "\"",
            };
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
