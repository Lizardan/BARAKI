using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    public sealed class RuntimeDebugConsole : MonoBehaviour
    {
        private const int WindowId = 572013;
        private const float Margin = 12f;
        private const float MinWindowWidth = 360f;
        private const float MinWindowHeight = 220f;
        private const string LastReportFileName = "last-playtest-report.txt";

        private static RuntimeDebugConsole s_instance;

        private readonly RuntimeDebugConsoleLogBuffer _buffer = new();
        private readonly DebugReportSendGate _sendGate = new();
        private Rect _windowRect = new(Margin, Margin, 720f, 360f);
        private Vector2 _scroll;
        private bool _isOpen;
        private bool _isSending;
        private string _status = string.Empty;
        private GUIStyle _logStyle;
        private GUIStyle _toolbarButtonStyle;
        private GUIStyle _statusStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (s_instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(RuntimeDebugConsole));
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<RuntimeDebugConsole>();
        }

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame)
            {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen)
            {
                return;
            }

            EnsureStyles();
            ClampWindowToScreen();
            _windowRect = GUI.Window(WindowId, _windowRect, DrawConsoleWindow, "Дебаг консоль");
        }

        private void DrawConsoleWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Копировать", _toolbarButtonStyle, GUILayout.Width(110f)))
            {
                GUIUtility.systemCopyBuffer = _buffer.BuildCopyText();
                _status = "Скопировано";
            }

            if (GUILayout.Button("Очистить", _toolbarButtonStyle, GUILayout.Width(90f)))
            {
                _buffer.Clear();
                _status = "Очищено";
            }

            GUI.enabled = !_isSending;
            if (GUILayout.Button("Отправить лог", _toolbarButtonStyle, GUILayout.Width(130f)))
            {
                SendLogAsync();
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть", _toolbarButtonStyle, GUILayout.Width(80f)))
            {
                _isOpen = false;
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status, _statusStyle);
            }

            var entries = _buffer.GetSnapshot();
            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box);
            if (entries.Count == 0)
            {
                GUILayout.Label("Логов пока нет.", _logStyle);
            }
            else
            {
                DrawEntries(entries);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        private void DrawEntries(IReadOnlyList<RuntimeDebugConsoleLogEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var previousColor = GUI.contentColor;
                GUI.contentColor = GetColor(entry.Type);
                var suffix = entry.RepeatCount > 1 ? $" x{entry.RepeatCount}" : string.Empty;
                GUILayout.Label(
                    $"[{entry.Timestamp:HH:mm:ss}] [{entry.Type}] {entry.Message}{suffix}",
                    _logStyle);

                if (!string.IsNullOrWhiteSpace(entry.StackTrace)
                    && entry.Type is LogType.Error or LogType.Exception or LogType.Assert)
                {
                    GUILayout.Label(entry.StackTrace, _logStyle);
                }

                GUI.contentColor = previousColor;
            }
        }

        private async void SendLogAsync()
        {
            if (_isSending)
            {
                return;
            }

            var eventsText = _buffer.BuildCopyText();
            if (string.IsNullOrWhiteSpace(eventsText))
            {
                _status = "Нет логов";
                return;
            }

            var utcNow = DateTime.UtcNow;
            if (!_sendGate.TryBeginSend(utcNow, out var blockReason))
            {
                _status = blockReason;
                return;
            }

            if (!DiscordWebhookSettings.TryResolveWebhookUrl(out var webhookUrl, out var source))
            {
                _status = source switch
                {
                    "missing-embedded-and-resources" => "Webhook: нет embed/Settings",
                    "empty-url" => "Webhook: URL пустой в Settings",
                    _ => "Webhook не настроен",
                };
                return;
            }

            _isSending = true;
            _status = "Отправка…";

            var netSection = DebugReportContext.TryBuildNetSection();
            var label = DebugPlaytestReportBuilder.BuildDiscordLabel(netSection, utcNow);
            var report = DebugPlaytestReportBuilder.BuildReport(eventsText, netSection, utcNow);
            TryWriteLocalCopy(report);

            var fileName = $"baraki-log-{utcNow:yyyyMMdd-HHmmss}.txt";
            var (ok, error) = await DiscordWebhookSender.SendReportAsync(
                webhookUrl,
                label,
                fileName,
                report);

            _isSending = false;
            if (ok)
            {
                _sendGate.MarkSuccess(DateTime.UtcNow);
                _status = "Отправлено";
            }
            else
            {
                _sendGate.MarkFailure(DateTime.UtcNow);
                _status = string.IsNullOrEmpty(error) ? "Ошибка отправки" : $"Ошибка: {error}";
            }
        }

        private static void TryWriteLocalCopy(string report)
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, LastReportFileName);
                File.WriteAllText(path, report, Encoding.UTF8);
            }
            catch
            {
                // Local copy is best-effort for Cursor handoff.
            }
        }

        private void ClampWindowToScreen()
        {
            var width = Mathf.Clamp(_windowRect.width, MinWindowWidth, Mathf.Max(MinWindowWidth, Screen.width - Margin * 2f));
            var height = Mathf.Clamp(_windowRect.height, MinWindowHeight, Mathf.Max(MinWindowHeight, Screen.height - Margin * 2f));
            _windowRect.width = width;
            _windowRect.height = height;
            _windowRect.x = Mathf.Clamp(_windowRect.x, Margin, Mathf.Max(Margin, Screen.width - width - Margin));
            _windowRect.y = Mathf.Clamp(_windowRect.y, Margin, Mathf.Max(Margin, Screen.height - height - Margin));
        }

        private void EnsureStyles()
        {
            _logStyle ??= new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                richText = false,
                fontSize = 13,
            };
            _toolbarButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
            };
            _statusStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = false,
            };
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!RuntimeDebugConsoleLogFilter.ShouldAccept(condition, type))
            {
                return;
            }

            _buffer.Add(condition, stackTrace, type);
        }

        private static Color GetColor(LogType type) => type switch
        {
            LogType.Error or LogType.Exception or LogType.Assert => new Color(1f, 0.42f, 0.35f),
            LogType.Warning => new Color(1f, 0.82f, 0.38f),
            _ => Color.white,
        };
    }
}
