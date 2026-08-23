using System;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>Shared launcher/main-menu chat with Global / Friends feed tabs.</summary>
    public sealed class MenuChatPanel : IDisposable
    {
        readonly VisualElement _root;
        readonly string _messageRowClass;
        readonly string _messageMetaClass;
        readonly string _messageNickClass;
        readonly string _messageTimeClass;
        readonly string _messageTextClass;
        readonly string _tabActiveClass;
        readonly Action _onScrollToEnd;

        Button _globalTab;
        Button _friendsTab;
        VisualElement _globalMessages;
        VisualElement _friendsMessages;
        ScrollView _globalScroll;
        ScrollView _friendsScroll;
        TextField _input;
        Button _sendButton;
        Label _subtitle;
        MenuChatChannel _active = MenuChatChannel.Global;
        bool _subscribed;

        public MenuChatPanel(
            VisualElement root,
            string messageRowClass,
            string messageMetaClass,
            string messageNickClass,
            string messageTimeClass,
            string messageTextClass,
            string tabActiveClass = "mm__chat-channel-tab--active")
        {
            _root = root;
            _messageRowClass = messageRowClass;
            _messageMetaClass = messageMetaClass;
            _messageNickClass = messageNickClass;
            _messageTimeClass = messageTimeClass;
            _messageTextClass = messageTextClass;
            _tabActiveClass = tabActiveClass;
        }

        public void Bind()
        {
            if (_root == null)
            {
                return;
            }

            _globalTab = _root.Q<Button>("ChatGlobalTabButton");
            _friendsTab = _root.Q<Button>("ChatFriendsTabButton");
            _globalMessages = _root.Q<VisualElement>("ChatGlobalMessages")
                              ?? _root.Q<VisualElement>("ChatMessages");
            _friendsMessages = _root.Q<VisualElement>("ChatFriendsMessages");
            _globalScroll = _root.Q<ScrollView>("ChatGlobalScroll")
                            ?? _root.Q<ScrollView>("ChatScroll");
            _friendsScroll = _root.Q<ScrollView>("ChatFriendsScroll");
            _input = _root.Q<TextField>("ChatInput");
            _sendButton = _root.Q<Button>("ChatSendButton");
            _subtitle = _root.Q<Label>("ChatSubtitle");

            EnsureFriendsContainer();
            RebuildFromHistory();
            ShowChannel(_active);
            UpdateComposerEnabled();
        }

        public void RegisterCallbacks(bool register)
        {
            if (register)
            {
                if (_globalTab != null) _globalTab.clicked += OnGlobalTab;
                if (_friendsTab != null) _friendsTab.clicked += OnFriendsTab;
                if (_sendButton != null) _sendButton.clicked += OnSend;
                if (_input != null) _input.RegisterCallback<KeyDownEvent>(OnInputKeyDown);
                if (!_subscribed)
                {
                    GameChatService.ChannelMessageReceived += OnChannelMessage;
                    GameChatService.ReadyChanged += OnReadyChanged;
                    _subscribed = true;
                }
            }
            else
            {
                if (_globalTab != null) _globalTab.clicked -= OnGlobalTab;
                if (_friendsTab != null) _friendsTab.clicked -= OnFriendsTab;
                if (_sendButton != null) _sendButton.clicked -= OnSend;
                if (_input != null) _input.UnregisterCallback<KeyDownEvent>(OnInputKeyDown);
                if (_subscribed)
                {
                    GameChatService.ChannelMessageReceived -= OnChannelMessage;
                    GameChatService.ReadyChanged -= OnReadyChanged;
                    _subscribed = false;
                }
            }
        }

        public void Dispose()
        {
            RegisterCallbacks(false);
        }

        public bool IsInputFocused()
        {
            if (_input == null)
            {
                return false;
            }

            var focused = _input.focusController?.focusedElement as VisualElement;
            return focused != null && (_input == focused || _input.Contains(focused));
        }

        void EnsureFriendsContainer()
        {
            if (_friendsMessages != null)
            {
                return;
            }

            // Fallback if UXML not yet updated: reuse global list only.
            _friendsMessages = _globalMessages;
            _friendsScroll = _globalScroll;
        }

        void OnGlobalTab() => ShowChannel(MenuChatChannel.Global);

        void OnFriendsTab() => ShowChannel(MenuChatChannel.FriendsFeed);

        void ShowChannel(MenuChatChannel channel)
        {
            _active = channel;
            var friends = channel == MenuChatChannel.FriendsFeed;
            _globalTab?.EnableInClassList(_tabActiveClass, !friends);
            _friendsTab?.EnableInClassList(_tabActiveClass, friends);

            if (_globalScroll != null && _friendsScroll != null && _globalScroll != _friendsScroll)
            {
                _globalScroll.EnableInClassList("ln-chat-scroll--hidden", friends);
                _globalScroll.EnableInClassList("mm-chat-scroll--hidden", friends);
                _friendsScroll.EnableInClassList("ln-chat-scroll--hidden", !friends);
                _friendsScroll.EnableInClassList("mm-chat-scroll--hidden", !friends);
                _globalScroll.style.display = friends ? DisplayStyle.None : DisplayStyle.Flex;
                _friendsScroll.style.display = friends ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_subtitle != null)
            {
                _subtitle.text = friends
                    ? "Лента друзей"
                    : (GameChatService.IsReady ? "Общий канал" : GameChatRules.ChatUnavailableHint);
            }
        }

        void OnSend() => TrySend();

        void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                TrySend();
            }
        }

        void TrySend()
        {
            var text = _input?.value?.Trim() ?? string.Empty;
            if (!GameChatRules.TrySanitizeMessage(text, out _))
            {
                return;
            }

            if (_input != null)
            {
                _input.value = string.Empty;
            }

            if (!GameChatService.IsReady)
            {
                AppendLocalStub(_active, "Система", GameChatRules.ChatUnavailableHint);
                return;
            }

            GameChatService.SendChannelAsync(_active, text);
        }

        void OnChannelMessage(GameChatMessage message)
        {
            AppendMessage(message);
            ScrollToEnd(message.Channel);
        }

        void OnReadyChanged()
        {
            UpdateComposerEnabled();
            if (_active == MenuChatChannel.Global && _subtitle != null && GameChatService.IsReady)
            {
                _subtitle.text = "Общий канал";
            }
        }

        void UpdateComposerEnabled()
        {
            var enabled = GameChatService.IsReady;
            _sendButton?.SetEnabled(enabled);
            if (_input != null)
            {
                _input.SetEnabled(enabled);
            }
        }

        void RebuildFromHistory()
        {
            RebuildList(MenuChatChannel.Global, _globalMessages);
            if (_friendsMessages != null && _friendsMessages != _globalMessages)
            {
                RebuildList(MenuChatChannel.FriendsFeed, _friendsMessages);
            }
        }

        void RebuildList(MenuChatChannel channel, VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            container.Clear();
            var history = GameChatService.GetChannelHistory(channel);
            for (var i = 0; i < history.Count; i++)
            {
                container.Add(CreateRow(history[i].SenderDisplayName, history[i].ReceivedAt, history[i].Text));
            }

            if (history.Count == 0)
            {
                var hint = channel == MenuChatChannel.FriendsFeed
                    ? "Лента друзей. Сообщения видят ваши друзья онлайн."
                    : "Общий чат. Сообщения видят игроки в лаунчере и меню.";
                container.Add(CreateRow("Система", DateTime.Now, hint));
            }
        }

        void AppendMessage(GameChatMessage message)
        {
            var container = message.Channel == MenuChatChannel.FriendsFeed
                ? _friendsMessages
                : _globalMessages;
            if (container == null)
            {
                return;
            }

            container.Add(CreateRow(message.SenderDisplayName, message.ReceivedAt, message.Text));
        }

        void AppendLocalStub(MenuChatChannel channel, string nick, string text)
        {
            var container = channel == MenuChatChannel.FriendsFeed ? _friendsMessages : _globalMessages;
            container?.Add(CreateRow(nick, DateTime.Now, text));
            ScrollToEnd(channel);
        }

        VisualElement CreateRow(string nick, DateTime time, string text)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(_messageRowClass);

            var meta = new VisualElement { pickingMode = PickingMode.Ignore };
            meta.AddToClassList(_messageMetaClass);

            var nickLabel = new Label(nick) { pickingMode = PickingMode.Ignore };
            nickLabel.AddToClassList(_messageNickClass);
            meta.Add(nickLabel);

            var timeLabel = new Label(GameChatRules.FormatMessageTime(time)) { pickingMode = PickingMode.Ignore };
            timeLabel.AddToClassList(_messageTimeClass);
            meta.Add(timeLabel);
            row.Add(meta);

            var body = new Label(text) { pickingMode = PickingMode.Ignore };
            body.AddToClassList(_messageTextClass);
            row.Add(body);
            return row;
        }

        void ScrollToEnd(MenuChatChannel channel)
        {
            var scroll = channel == MenuChatChannel.FriendsFeed ? _friendsScroll : _globalScroll;
            scroll?.schedule.Execute(() =>
            {
                if (scroll.verticalScroller.highValue > 0f)
                {
                    scroll.verticalScroller.value = scroll.verticalScroller.highValue;
                }
            });
        }
    }
}
