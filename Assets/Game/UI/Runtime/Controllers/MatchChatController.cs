using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>In-match chat: Enter opens composer; messages fade without background.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchChatController : MonoBehaviour
    {
        const string ComposerHiddenClass = "match-chat__composer--hidden";
        const string PauseHiddenClass = "match-hud__pause--hidden";
        const string ResultsHiddenClass = "match-hud__results--hidden";
        const string ExtraAbilityHiddenClass = "match-extra-ability--hidden";

        [SerializeField] UIDocument _uiDocument;

        VisualElement _root;
        VisualElement _messages;
        VisualElement _composer;
        VisualElement _pauseOverlay;
        VisualElement _resultsOverlay;
        VisualElement _extraAbilityMenu;
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
            _pauseOverlay = _root.Q<VisualElement>("PauseOverlay");
            _resultsOverlay = _root.Q<VisualElement>("ResultsOverlay");
            _extraAbilityMenu = _root.Q<VisualElement>("ExtraAbilityMenu");
            _input = _root.Q<TextField>("MatchChatInput");
            _sendButton = _root.Q<Button>("MatchChatSendButton");
            if (_input != null)
            {
                _input.multiline = false;
            }

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
                _input.RegisterCallback<NavigationSubmitEvent>(OnInputSubmit);
            }

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
                _input.UnregisterCallback<NavigationSubmitEvent>(OnInputSubmit);
            }

            MatchChatNetworkFacade.MessageReceived -= OnMatchMessage;
        }

        void Update()
        {
            UpdateFadeLines();
            TryHandleEnterHotkey();
        }

        void UpdateFadeLines()
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

        void TryHandleEnterHotkey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                return;
            }

            if (BlocksMatchChatHotkey())
            {
                return;
            }

            if (!_composerOpen)
            {
                SetComposerOpen(true);
                _input?.schedule.Execute(() => _input?.Focus());
                return;
            }

            TrySend();
        }

        bool BlocksMatchChatHotkey()
        {
            if (RuntimeDebugConsole.IsOpen)
            {
                return true;
            }

            if (_pauseOverlay != null && !_pauseOverlay.ClassListContains(PauseHiddenClass))
            {
                return true;
            }

            if (_resultsOverlay != null && !_resultsOverlay.ClassListContains(ResultsHiddenClass))
            {
                return true;
            }

            return _extraAbilityMenu != null
                   && !_extraAbilityMenu.ClassListContains(ExtraAbilityHiddenClass);
        }

        void OnInputSubmit(NavigationSubmitEvent evt)
        {
            evt.StopPropagation();
            TrySend();
        }

        void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.Escape)
            {
                evt.StopPropagation();
                SetComposerOpen(false);
                return;
            }

            if (!GameChatRules.IsComposerSubmit(evt.keyCode, evt.character))
            {
                return;
            }

            evt.StopPropagation();
            evt.PreventDefault();
            TrySend();
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
