using System;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>DM overlay — graphite Main Menu dialog; transport via Cloudflare GameChatService.</summary>
    public sealed class FriendsDirectChatPanel : IDisposable
    {
        const string HiddenClass = "ui-overlay--hidden";

        readonly VisualElement _host;
        VisualElement _overlay;
        Label _title;
        ScrollView _scroll;
        VisualElement _messages;
        TextField _input;
        Button _sendButton;
        Button _closeButton;
        string _peerId = string.Empty;
        string _peerName = string.Empty;
        bool _bound;

        public FriendsDirectChatPanel(VisualElement host)
        {
            _host = host;
        }

        public void EnsureBuilt()
        {
            if (_overlay != null || _host == null)
            {
                return;
            }

            _overlay = new VisualElement { name = "FriendsDirectChatOverlay" };
            _overlay.AddToClassList("mm__dm-overlay");
            _overlay.AddToClassList(HiddenClass);
            _overlay.pickingMode = PickingMode.Position;

            var panel = new VisualElement();
            panel.AddToClassList("ui-dialog");
            panel.AddToClassList("mm__dm-panel");

            var header = new VisualElement();
            header.AddToClassList("ui-dialog__header");
            _title = new Label("ЛС") { pickingMode = PickingMode.Ignore };
            _title.AddToClassList("ui-dialog__title");
            _closeButton = new Button { text = "✕" };
            _closeButton.AddToClassList("ui-dialog__close");
            header.Add(_title);
            header.Add(_closeButton);
            panel.Add(header);

            var body = new VisualElement();
            body.AddToClassList("ui-dialog__body");
            body.AddToClassList("mm__dm-body");

            var scroll = new ScrollView { name = "FriendsDmScroll", mode = ScrollViewMode.Vertical };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("mm-chat-scroll");
            scroll.AddToClassList("mm__dm-scroll");
            _scroll = scroll;
            _messages = new VisualElement { name = "FriendsDmMessages" };
            _messages.AddToClassList("mm-chat-messages");
            scroll.Add(_messages);
            body.Add(scroll);

            var composer = new VisualElement();
            composer.AddToClassList("mm-chat-composer");
            composer.AddToClassList("mm__dm-composer");
            _input = new TextField { maxLength = GameChatRules.MaxMessageLength };
            _input.AddToClassList("mm-chat-input");
            _input.multiline = false;
            if (_input.textEdition != null)
            {
                _input.textEdition.placeholder = "Написать сообщение…";
            }

            _sendButton = new Button { text = "ОТПРАВИТЬ" };
            _sendButton.AddToClassList("mm-chat-send");
            composer.Add(_input);
            composer.Add(_sendButton);
            body.Add(composer);
            panel.Add(body);

            _overlay.Add(panel);
            _host.Add(_overlay);
        }

        public void Bind()
        {
            EnsureBuilt();
            if (_bound || _overlay == null)
            {
                return;
            }

            _closeButton.clicked += Close;
            _sendButton.clicked += OnSend;
            // TrickleDown: the text-input core swallows Enter/Escape on bubble-up.
            _input.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            _input.RegisterCallback<NavigationSubmitEvent>(OnInputSubmit);
            GameChatService.DirectMessageReceived += OnDirectMessage;
            _bound = true;
        }

        public void Dispose()
        {
            if (!_bound)
            {
                return;
            }

            _closeButton.clicked -= Close;
            _sendButton.clicked -= OnSend;
            _input.UnregisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            _input.UnregisterCallback<NavigationSubmitEvent>(OnInputSubmit);
            GameChatService.DirectMessageReceived -= OnDirectMessage;
            _bound = false;
        }

        public void Open(string playerId, string displayName)
        {
            EnsureBuilt();
            Bind();
            _peerId = GameChatRules.NormalizePlayerId(playerId);
            _peerName = GameChatRules.FormatDisplayName(displayName);
            GameChatService.EnsureDirectPeer(_peerId);
            if (_title != null)
            {
                _title.text = $"ЛС · {_peerName}";
            }

            Rebuild();
            _overlay?.RemoveFromClassList(HiddenClass);
            ScrollToEnd();
            GameChatService.RefreshNow();
            _input?.schedule.Execute(() => _input?.Focus());
        }

        public void Close()
        {
            _overlay?.AddToClassList(HiddenClass);
            _input?.Blur();
        }

        void OnSend() => TrySend();

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
                Close();
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

        void TrySend()
        {
            var text = _input?.value ?? string.Empty;
            if (_input != null)
            {
                _input.value = string.Empty;
            }

            if (!GameChatRules.TrySanitizeMessage(text, out _) || _peerId.Length == 0)
            {
                return;
            }

            GameChatService.SendDirectAsync(_peerId, text);
            GameChatService.RefreshNow();
        }

        void OnDirectMessage(GameChatDirectMessage message)
        {
            if (message.OtherPlayerId != _peerId)
            {
                return;
            }

            Append(message);
        }

        void Rebuild()
        {
            if (_messages == null)
            {
                return;
            }

            _messages.Clear();
            var history = GameChatService.GetDirectHistory(_peerId);
            for (var i = 0; i < history.Count; i++)
            {
                Append(history[i]);
            }

            if (history.Count == 0)
            {
                _messages.Add(CreateLine("Система", DateTime.Now, "Напишите сообщение другу."));
            }

            ScrollToEnd();
        }

        void Append(GameChatDirectMessage message)
        {
            var nick = message.FromSelf ? "Вы" : message.SenderDisplayName;
            _messages?.Add(CreateLine(nick, message.ReceivedAt, message.Text));
            ScrollToEnd();
        }

        void ScrollToEnd()
        {
            ScrollViewToEnd(_scroll);
        }

        static void ScrollViewToEnd(ScrollView scroll)
        {
            if (scroll == null)
            {
                return;
            }

            void Apply()
            {
                var y = scroll.verticalScroller.highValue;
                scroll.scrollOffset = new Vector2(0f, y > 0f ? y : 99999f);
            }

            scroll.schedule.Execute(Apply);
            scroll.schedule.Execute(Apply).StartingIn(32);
        }

        static VisualElement CreateLine(string nick, DateTime time, string text)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("mm-chat-message");

            var meta = new VisualElement { pickingMode = PickingMode.Ignore };
            meta.AddToClassList("mm-chat-message__meta");

            var nickLabel = new Label(nick) { pickingMode = PickingMode.Ignore };
            nickLabel.AddToClassList("mm-chat-message__nick");
            meta.Add(nickLabel);

            var timeLabel = new Label(GameChatRules.FormatMessageTime(time)) { pickingMode = PickingMode.Ignore };
            timeLabel.AddToClassList("mm-chat-message__time");
            meta.Add(timeLabel);
            row.Add(meta);

            var body = new Label(text) { pickingMode = PickingMode.Ignore };
            body.AddToClassList("mm-chat-message__text");
            row.Add(body);
            return row;
        }
    }
}
