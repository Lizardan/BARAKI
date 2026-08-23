using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>In-match chat: Enter opens composer; messages fade without background.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchChatController : MonoBehaviour
    {
        const string ComposerHiddenClass = "match-chat__composer--hidden";

        [SerializeField] UIDocument _uiDocument;

        VisualElement _root;
        VisualElement _messages;
        VisualElement _composer;
        TextField _input;
        Button _sendButton;
        readonly List<FadeLine> _lines = new();
        bool _composerOpen;

        struct FadeLine
        {
            public Label Label;
            public float BornAt;
        }

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            _root = _uiDocument.rootVisualElement;
            _messages = _root.Q<VisualElement>("MatchChatMessages");
            _composer = _root.Q<VisualElement>("MatchChatComposer");
            _input = _root.Q<TextField>("MatchChatInput");
            _sendButton = _root.Q<Button>("MatchChatSendButton");
            SetComposerOpen(false);
        }

        void OnEnable()
        {
            if (_sendButton != null)
            {
                _sendButton.clicked += OnSendClicked;
            }

            if (_input != null)
            {
                _input.RegisterCallback<KeyDownEvent>(OnInputKeyDown);
            }

            _root?.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            MatchChatNetworkFacade.MessageReceived += OnMatchMessage;
        }

        void OnDisable()
        {
            if (_sendButton != null)
            {
                _sendButton.clicked -= OnSendClicked;
            }

            if (_input != null)
            {
                _input.UnregisterCallback<KeyDownEvent>(OnInputKeyDown);
            }

            _root?.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            MatchChatNetworkFacade.MessageReceived -= OnMatchMessage;
        }

        void Update()
        {
            if (_lines.Count == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                var line = _lines[i];
                var age = now - line.BornAt;
                if (GameChatRules.ShouldRemoveMatchMessage(age))
                {
                    line.Label.RemoveFromHierarchy();
                    _lines.RemoveAt(i);
                    continue;
                }

                var alpha = GameChatRules.MatchMessageAlpha(age);
                var c = line.Label.style.color.value;
                line.Label.style.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter))
            {
                return;
            }

            if (RuntimeDebugConsole.IsOpen)
            {
                return;
            }

            if (_composerOpen)
            {
                return;
            }

            var pause = _root.Q<VisualElement>("PauseOverlay");
            if (pause != null && !pause.ClassListContains("match-hud__pause--hidden"))
            {
                return;
            }

            evt.StopPropagation();
            SetComposerOpen(true);
            _input?.schedule.Execute(() => _input?.Focus());
        }

        void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.Escape)
            {
                evt.StopPropagation();
                SetComposerOpen(false);
                return;
            }

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                TrySend();
            }
        }

        void OnSendClicked() => TrySend();

        void TrySend()
        {
            var text = _input?.value ?? string.Empty;
            SetComposerOpen(false);
            if (_input != null)
            {
                _input.value = string.Empty;
            }

            if (!NetworkMatchChatRules.TryNormalize(text, out var message))
            {
                return;
            }

            if (!MatchChatNetworkFacade.HasBridge)
            {
                OnMatchMessage("Вы", message);
                return;
            }

            MatchChatNetworkFacade.Send(message);
        }

        void SetComposerOpen(bool open)
        {
            _composerOpen = open;
            _composer?.EnableInClassList(ComposerHiddenClass, !open);
            if (!open)
            {
                _input?.Blur();
            }
        }

        void OnMatchMessage(string displayName, string message)
        {
            if (_messages == null)
            {
                return;
            }

            var label = new Label($"{GameChatRules.FormatDisplayName(displayName)}: {message}")
            {
                pickingMode = PickingMode.Ignore,
            };
            label.AddToClassList("match-chat__line");
            _messages.Add(label);
            _lines.Add(new FadeLine { Label = label, BornAt = Time.unscaledTime });
        }
    }
}
