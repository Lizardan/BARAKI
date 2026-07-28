using System;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    public static class DebugPlaytestReportBuilder
    {
        public const int MaxReportChars = 400 * 1024;

        public static string BuildIssueTitle(string netSection, DateTime utcNow)
        {
            var name = ExtractField(netSection, "name") ?? "-";
            var role = ExtractField(netSection, "role") ?? "Offline";
            var room = ExtractField(netSection, "room") ?? "-";
            var slot = ExtractField(netSection, "slot") ?? "-";
            var mode = ExtractField(netSection, "mode") ?? "-";
            var phase = ExtractField(netSection, "phase") ?? "Offline";
            var filled = ExtractField(netSection, "filled") ?? "-";
            var elapsed = ExtractField(netSection, "matchElapsed");
            var title =
                $"playtest | {name} | room={room} | mode={mode} | role={role} | slot={slot} | phase={phase} | filled={filled}";
            if (string.Equals(phase, "Match", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(elapsed)
                && elapsed != "-")
            {
                title += $" | elapsed={elapsed}";
            }

            return title + $" | {utcNow:yyyy-MM-dd HH:mm}Z";
        }

        public static string BuildReport(string eventsText, string netSection, DateTime utcNow)
        {
            var builder = new StringBuilder(Math.Max(512, (eventsText?.Length ?? 0) + 512));
            builder.AppendLine("=== META ===");
            builder.Append("reportId=").AppendLine(PlaytestLog.ReportId);
            builder.Append("timeUtc=").AppendLine(utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            builder.Append("version=").Append(Application.version);
            var build = Application.buildGUID;
            if (!string.IsNullOrEmpty(build) && build.Length >= 8)
            {
                builder.Append(" build=").Append(build.Substring(0, 8));
            }

            builder.AppendLine();
            builder.Append("platform=").Append(Application.platform);
            builder.Append(" scene=").AppendLine(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            builder.AppendLine();
            builder.AppendLine("=== NET ===");
            builder.AppendLine(string.IsNullOrWhiteSpace(netSection) ? "role=Offline phase=Offline" : netSection.Trim());
            builder.Append("device=").AppendLine(Sanitize(SystemInfo.deviceModel));
            builder.AppendLine();
            builder.AppendLine("=== EVENTS ===");
            if (string.IsNullOrWhiteSpace(eventsText))
            {
                builder.AppendLine("(empty)");
            }
            else
            {
                builder.AppendLine(eventsText.TrimEnd());
            }

            builder.AppendLine();
            builder.AppendLine("=== END ===");

            var report = builder.ToString();
            if (report.Length <= MaxReportChars)
            {
                return report;
            }

            var keepTail = MaxReportChars - 64;
            return report.Substring(report.Length - keepTail) + "\n... [report truncated]\n=== END ===\n";
        }

        private static string ExtractField(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            var token = key + "=";
            var index = section.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var start = index + token.Length;
            var end = start;
            while (end < section.Length)
            {
                var c = section[end];
                if (c == ' ' || c == '\n' || c == '\r')
                {
                    break;
                }

                end++;
            }

            return section.Substring(start, end - start);
        }

        private static string Sanitize(string value) =>
            string.IsNullOrEmpty(value) ? "-" : value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
