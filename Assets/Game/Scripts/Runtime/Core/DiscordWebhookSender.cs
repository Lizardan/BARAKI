using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Core
{
    public static class DiscordWebhookSender
    {
        public static async Awaitable<(bool Ok, string Error)> SendReportAsync(
            string webhookUrl,
            string content,
            string fileName,
            string reportText)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                return (false, "Webhook URL не задан");
            }

            if (string.IsNullOrEmpty(reportText))
            {
                return (false, "Пустой отчёт");
            }

            try
            {
                var form = new WWWForm();
                form.AddField("content", TruncateContent(content));
                form.AddBinaryData(
                    "file",
                    Encoding.UTF8.GetBytes(reportText),
                    string.IsNullOrEmpty(fileName) ? "baraki-log.txt" : fileName,
                    "text/plain");

                using var request = UnityWebRequest.Post(webhookUrl, form);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    return (false, $"{request.responseCode} {request.error}");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string TruncateContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return "BARAKI log";
            }

            return content.Length <= 1800 ? content : content.Substring(0, 1800);
        }
    }
}
