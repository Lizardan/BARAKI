using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Compact tagged logs that pass the playtest console filter.</summary>
    public static class PlaytestLog
    {
        public const string Marker = "[P]";

        public static readonly string ReportId = Guid.NewGuid().ToString("N").Substring(0, 8);

        public static void Info(string category, string action, params (string Key, object Value)[] data) =>
            Debug.Log(Format(category, action, data));

        public static void Warn(string category, string action, params (string Key, object Value)[] data) =>
            Debug.LogWarning(Format(category, action, data));

        public static string Format(string category, string action, params (string Key, object Value)[] data)
        {
            var builder = new StringBuilder(96);
            builder.Append(Marker).Append(' ').Append(category).Append('.').Append(action);
            if (data == null || data.Length == 0)
            {
                return builder.ToString();
            }

            for (var i = 0; i < data.Length; i++)
            {
                builder.Append(' ').Append(data[i].Key).Append('=').Append(FormatValue(data[i].Value));
            }

            return builder.ToString();
        }

        private static string FormatValue(object value) =>
            value switch
            {
                null => "null",
                bool b => b ? "1" : "0",
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                float f => f.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => Sanitize(value.ToString()),
            };

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace('\r', ' ').Replace('\n', ' ').Replace(' ', '_');
        }
    }
}
