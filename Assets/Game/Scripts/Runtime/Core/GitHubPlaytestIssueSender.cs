using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Core
{
    public static class GitHubPlaytestIssueSender
    {
        /// <summary>GitHub issue body soft limit is 65536; keep margin.</summary>
        public const int MaxIssueBodyChars = 60000;

        public static async Awaitable<(bool Ok, string Error, string HtmlUrl)> CreateIssueAsync(
            string token,
            string repository,
            string title,
            string body)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "GitHub token не задан", null);
            }

            if (string.IsNullOrWhiteSpace(repository) || !repository.Contains("/"))
            {
                return (false, "Репозиторий не задан (owner/name)", null);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = "BARAKI playtest log";
            }

            if (string.IsNullOrEmpty(body))
            {
                return (false, "Пустой отчёт", null);
            }

            try
            {
                var url = $"https://api.github.com/repos/{repository.Trim()}/issues";
                var payload = BuildJsonPayload(title, TruncateBody(body));
                var bytes = Encoding.UTF8.GetBytes(payload);

                using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.SetRequestHeader("Authorization", "Bearer " + token.Trim());
                request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
                request.SetRequestHeader("User-Agent", "BARAKI-Playtest");

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var detail = request.downloadHandler?.text;
                    if (!string.IsNullOrEmpty(detail) && detail.Length > 180)
                    {
                        detail = detail.Substring(0, 180);
                    }

                    return (false, $"{request.responseCode} {request.error} {detail}".Trim(), null);
                }

                var htmlUrl = TryExtractHtmlUrl(request.downloadHandler?.text);
                return (true, string.Empty, htmlUrl);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        private static string TruncateBody(string body)
        {
            if (body.Length <= MaxIssueBodyChars)
            {
                return body;
            }

            return body.Substring(0, MaxIssueBodyChars) + "\n\n... [issue body truncated]\n=== END ===\n";
        }

        private static string BuildJsonPayload(string title, string body)
        {
            var builder = new StringBuilder(body.Length + 128);
            builder.Append("{\"title\":")
                .Append(Quote(title))
                .Append(",\"body\":")
                .Append(Quote(body))
                .Append(",\"labels\":[")
                .Append(Quote(GitHubPlaytestStampRules.IssueLabel))
                .Append("]}");
            return builder.ToString();
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "null";
            }

            var builder = new StringBuilder(value.Length + 16);
            builder.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u")
                                .Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static string TryExtractHtmlUrl(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            const string key = "\"html_url\":\"";
            var index = json.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var start = index + key.Length;
            var end = json.IndexOf('"', start);
            if (end <= start)
            {
                return null;
            }

            return json.Substring(start, end - start);
        }
    }
}
