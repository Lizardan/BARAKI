using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Core
{
    public sealed class RuntimeDebugConsole : MonoBehaviour
    {
        private const int WindowId = 572013;
        private const float Margin = 12f;
        private const float MinWindowWidth = 360f;
        private const float MinWindowHeight = 220f;
        private const string LastReportFileName = "last-playtest-report.txt";
        private const string CommandFieldName = "RuntimeDebugConsole.Command";
        private const string UiBlockClass = "runtime-debug-console--blocking";

        private static RuntimeDebugConsole s_instance;

        private readonly RuntimeDebugConsoleLogBuffer _buffer = new();
        private readonly DebugReportSendGate _sendGate = new();
        private readonly List<VisualElement> _blockedUiRoots = new();
        private Rect _windowRect = new(Margin, Margin, 720f, 360f);
        private Vector2 _scroll;
        private bool _isOpen;
        private bool _isSending;
        private bool _focusCommandField;
        private bool _submitCommandRequested;
        private bool _clearLogsRequested;
        private bool _copyLogsRequested;
        private bool _sendLogsRequested;
        private bool _closeRequested;
        private string _status = string.Empty;
        private string _commandLine = string.Empty;
        private IReadOnlyList<RuntimeDebugConsoleLogEntry> _drawnEntries;
        private GUIStyle _logStyle;
        private GUIStyle _toolbarButtonStyle;
        private GUIStyle _statusStyle;

        public static bool IsOpen => s_instance != null && s_instance._isOpen;

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
            if (_isOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            ReleaseUiInputBlockers();
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // Tilde/backquote only opens: while open Esc closes; do not toggle with the same key.
            if (!_isOpen)
            {
                if (keyboard.backquoteKey.wasPressedThisFrame)
                {
                    Open();
                }

                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // Input System owns keyboard; IMGUI often never sees KeyCode.Return.
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                _submitCommandRequested = true;
            }

            // Never mutate IMGUI control tree mid-OnGUI (Layout vs Repaint mismatch).
            if (_closeRequested)
            {
                _closeRequested = false;
                Close();
                return;
            }

            if (_clearLogsRequested)
            {
                _clearLogsRequested = false;
                _buffer.Clear();
                _status = "Очищено";
            }

            if (_copyLogsRequested)
            {
                _copyLogsRequested = false;
                GUIUtility.systemCopyBuffer = _buffer.BuildCopyText();
                _status = "Скопировано";
            }

            if (_sendLogsRequested)
            {
                _sendLogsRequested = false;
                SendLogAsync().Forget();
            }

            if (_submitCommandRequested)
            {
                _submitCommandRequested = false;
                ExecuteCommandLine();
            }
        }

        private void LateUpdate()
        {
            if (!_isOpen)
            {
                return;
            }

            // Scene loads / new UIDocuments while console stays open.
            EnsureUiInputBlockers();
            BlurFocusedUiElements();
        }

        private void Open()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;
            _focusCommandField = true;
            EnsureUiInputBlockers();
            BlurFocusedUiElements();
        }

        private void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _submitCommandRequested = false;
            ReleaseUiInputBlockers();
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
            // Snapshot once per Layout so Layout/Repaint draw the same control count.
            if (Event.current.type == EventType.Layout)
            {
                _drawnEntries = _buffer.GetSnapshot();
            }

            var entries = _drawnEntries ?? Array.Empty<RuntimeDebugConsoleLogEntry>();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Копировать", _toolbarButtonStyle, GUILayout.Width(110f)))
            {
                _copyLogsRequested = true;
            }

            if (GUILayout.Button("Очистить", _toolbarButtonStyle, GUILayout.Width(90f)))
            {
                _clearLogsRequested = true;
            }

            GUI.enabled = !_isSending;
            if (GUILayout.Button("Отправить лог", _toolbarButtonStyle, GUILayout.Width(130f)))
            {
                _sendLogsRequested = true;
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть", _toolbarButtonStyle, GUILayout.Width(80f)))
            {
                _closeRequested = true;
            }

            GUILayout.EndHorizontal();

            // Always draw status row to keep control count stable.
            GUILayout.Label(string.IsNullOrEmpty(_status) ? " " : _status, _statusStyle);

            DrawLogScrollArea(entries);
            DrawCommandRow();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        private void DrawLogScrollArea(IReadOnlyList<RuntimeDebugConsoleLogEntry> entries)
        {
            var viewportHeight = Mathf.Max(120f, _windowRect.height - 118f);
            var viewport = GUILayoutUtility.GetRect(
                0f,
                viewportHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(viewportHeight));

            var contentWidth = Mathf.Max(1f, viewport.width - 18f);
            var contentHeight = 8f;
            if (entries.Count == 0)
            {
                contentHeight += _logStyle.CalcHeight(new GUIContent("Логов пока нет."), contentWidth);
            }
            else
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var suffix = entry.RepeatCount > 1 ? $" x{entry.RepeatCount}" : string.Empty;
                    var line = $"[{entry.Timestamp:HH:mm:ss}] [{entry.Type}] {entry.Message}{suffix}";
                    contentHeight += _logStyle.CalcHeight(new GUIContent(line), contentWidth) + 2f;
                    if (!string.IsNullOrWhiteSpace(entry.StackTrace)
                        && entry.Type is LogType.Error or LogType.Exception or LogType.Assert)
                    {
                        contentHeight += _logStyle.CalcHeight(new GUIContent(entry.StackTrace), contentWidth) + 2f;
                    }
                }
            }

            contentHeight = Mathf.Max(contentHeight, viewport.height);
            var contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            _scroll = GUI.BeginScrollView(viewport, _scroll, contentRect);

            var y = 4f;
            if (entries.Count == 0)
            {
                var empty = "Логов пока нет.";
                var h = _logStyle.CalcHeight(new GUIContent(empty), contentWidth);
                GUI.Label(new Rect(4f, y, contentWidth, h), empty, _logStyle);
            }
            else
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var previousColor = GUI.contentColor;
                    GUI.contentColor = GetColor(entry.Type);
                    var suffix = entry.RepeatCount > 1 ? $" x{entry.RepeatCount}" : string.Empty;
                    var line = $"[{entry.Timestamp:HH:mm:ss}] [{entry.Type}] {entry.Message}{suffix}";
                    var lineHeight = _logStyle.CalcHeight(new GUIContent(line), contentWidth);
                    GUI.Label(new Rect(4f, y, contentWidth, lineHeight), line, _logStyle);
                    y += lineHeight + 2f;

                    if (!string.IsNullOrWhiteSpace(entry.StackTrace)
                        && entry.Type is LogType.Error or LogType.Exception or LogType.Assert)
                    {
                        var stackHeight = _logStyle.CalcHeight(new GUIContent(entry.StackTrace), contentWidth);
                        GUI.Label(new Rect(4f, y, contentWidth, stackHeight), entry.StackTrace, _logStyle);
                        y += stackHeight + 2f;
                    }

                    GUI.contentColor = previousColor;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawCommandRow()
        {
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(CommandFieldName);
            _commandLine = GUILayout.TextField(_commandLine ?? string.Empty);
            if (GUILayout.Button("Выполнить", _toolbarButtonStyle, GUILayout.Width(100f)))
            {
                _submitCommandRequested = true;
            }

            GUILayout.EndHorizontal();

            if (_focusCommandField && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(CommandFieldName);
                _focusCommandField = false;
            }
        }

        private void ExecuteCommandLine()
        {
            var line = _commandLine?.Trim() ?? string.Empty;
            if (line.Length == 0)
            {
                return;
            }

            RuntimeDebugConsoleCommands.TryExecute(line, out var status);
            _status = string.IsNullOrEmpty(status) ? $"OK: {line}" : status;
            PlaytestLog.Info("Cheat", "Cmd", ("line", line), ("ok", status));
            _commandLine = string.Empty;
            _focusCommandField = true;
        }

        private void EnsureUiInputBlockers()
        {
            var documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < documents.Length; i++)
            {
                var root = documents[i] != null ? documents[i].rootVisualElement : null;
                if (root == null || root.ClassListContains(UiBlockClass))
                {
                    continue;
                }

                root.AddToClassList(UiBlockClass);
                root.RegisterCallback<KeyDownEvent>(OnBlockedUiKeyDown, TrickleDown.TrickleDown);
                root.RegisterCallback<NavigationSubmitEvent>(OnBlockedUiSubmit, TrickleDown.TrickleDown);
                _blockedUiRoots.Add(root);
            }
        }

        private void ReleaseUiInputBlockers()
        {
            for (var i = 0; i < _blockedUiRoots.Count; i++)
            {
                var root = _blockedUiRoots[i];
                if (root == null)
                {
                    continue;
                }

                root.UnregisterCallback<KeyDownEvent>(OnBlockedUiKeyDown, TrickleDown.TrickleDown);
                root.UnregisterCallback<NavigationSubmitEvent>(OnBlockedUiSubmit, TrickleDown.TrickleDown);
                root.RemoveFromClassList(UiBlockClass);
            }

            _blockedUiRoots.Clear();
        }

        private void OnBlockedUiKeyDown(KeyDownEvent evt)
        {
            if (!_isOpen)
            {
                return;
            }

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape or KeyCode.Space)
            {
                evt.StopImmediatePropagation();
            }
        }

        private void OnBlockedUiSubmit(NavigationSubmitEvent evt)
        {
            if (!_isOpen)
            {
                return;
            }

            evt.StopImmediatePropagation();
            evt.PreventDefault();
        }

        private static void BlurFocusedUiElements()
        {
            var documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < documents.Length; i++)
            {
                var root = documents[i] != null ? documents[i].rootVisualElement : null;
                var focused = root?.focusController?.focusedElement;
                focused?.Blur();
            }
        }

        private async UniTaskVoid SendLogAsync()
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

            if (!GitHubPlaytestSettings.TryResolveCredentials(out var token, out var repository, out var source))
            {
                _status = source switch
                {
                    "missing-embedded-and-resources" => "GitHub: нет token embed/Settings",
                    "empty-token" => "GitHub: token пустой в Settings",
                    _ => "GitHub не настроен",
                };
                return;
            }

            _isSending = true;
            _status = "Отправка…";

            var netSection = DebugReportContext.TryBuildNetSection();
            var title = DebugPlaytestReportBuilder.BuildIssueTitle(netSection, utcNow);
            var report = DebugPlaytestReportBuilder.BuildReport(eventsText, netSection, utcNow);
            TryWriteLocalCopy(report);

            var (ok, error, htmlUrl) = await GitHubPlaytestIssueSender.CreateIssueAsync(
                token,
                repository,
                title,
                report);

            _isSending = false;
            if (ok)
            {
                _sendGate.MarkSuccess(DateTime.UtcNow);
                _status = string.IsNullOrEmpty(htmlUrl) ? "Отправлено (Issue)" : "Отправлено";
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
