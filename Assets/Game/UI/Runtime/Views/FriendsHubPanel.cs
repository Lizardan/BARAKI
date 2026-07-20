using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    public enum FriendsHubPanelMode
    {
        Full,
        InviteOnly,
    }

    /// <summary>Builds friends list, requests, and invite actions into hub/lobby UI.</summary>
    public sealed class FriendsHubPanel : IDisposable
    {
        const string ActiveTabClass = "mm__friends-tab--active";
        const string OverlayHiddenClass = "ui-overlay--hidden";

        readonly FriendsHubPanelMode _mode;
        readonly VisualElement _friendsListContainer;
        readonly VisualElement _friendsTabContent;
        readonly VisualElement _invitesTabContent;
        readonly Button _friendsTabButton;
        readonly Button _invitesTabButton;
        readonly VisualElement _incomingList;
        readonly Label _friendsCountLabel;
        readonly TextField _friendIdField;
        readonly Button _addFriendButton;
        readonly Label _friendsErrorLabel;
        readonly VisualElement _addFriendSection;
        FriendsHubTab _activeTab = FriendsHubTab.Friends;
        bool _designPreviewActive;
        bool _subscribed;

        public event Action<string> JoinLobbyRequested;

        public FriendsHubTab ActiveTab => _activeTab;

        public FriendsHubPanel(
            FriendsHubPanelMode mode,
            VisualElement friendsListContainer,
            VisualElement friendsTabContent,
            VisualElement invitesTabContent,
            Button friendsTabButton,
            Button invitesTabButton,
            VisualElement incomingList,
            Label friendsCountLabel,
            TextField friendIdField,
            Button addFriendButton,
            Label friendsErrorLabel,
            VisualElement addFriendSection)
        {
            _mode = mode;
            _friendsListContainer = friendsListContainer;
            _friendsTabContent = friendsTabContent;
            _invitesTabContent = invitesTabContent;
            _friendsTabButton = friendsTabButton;
            _invitesTabButton = invitesTabButton;
            _incomingList = incomingList;
            _friendsCountLabel = friendsCountLabel;
            _friendIdField = friendIdField;
            _addFriendButton = addFriendButton;
            _friendsErrorLabel = friendsErrorLabel;
            _addFriendSection = addFriendSection;
        }

        public void Bind()
        {
            if (_subscribed)
            {
                return;
            }

            FriendsHubService.HubChanged += OnHubChanged;
            if (_addFriendButton != null)
            {
                _addFriendButton.clicked += OnAddFriendClicked;
            }

            if (_friendsTabButton != null)
            {
                _friendsTabButton.clicked += OnFriendsTabClicked;
            }

            if (_invitesTabButton != null)
            {
                _invitesTabButton.clicked += OnInvitesTabClicked;
            }

            _subscribed = true;
            ApplyModeVisibility();
            ApplyActiveTab();
            Refresh();
        }

        public void Dispose()
        {
            if (!_subscribed)
            {
                return;
            }

            FriendsHubService.HubChanged -= OnHubChanged;
            if (_addFriendButton != null)
            {
                _addFriendButton.clicked -= OnAddFriendClicked;
            }

            if (_friendsTabButton != null)
            {
                _friendsTabButton.clicked -= OnFriendsTabClicked;
            }

            if (_invitesTabButton != null)
            {
                _invitesTabButton.clicked -= OnInvitesTabClicked;
            }

            _subscribed = false;
        }

        public void SetInteractable(bool interactable)
        {
            _friendIdField?.SetEnabled(interactable);
            _addFriendButton?.SetEnabled(interactable);
            _friendsTabButton?.SetEnabled(interactable);
            _invitesTabButton?.SetEnabled(interactable);
            SetContainerInteractable(_friendsListContainer, interactable);
            SetContainerInteractable(_incomingList, interactable);
        }

        public void SetActiveTab(FriendsHubTab tab)
        {
            _activeTab = tab;
            ApplyActiveTab();
        }

        /// <summary>Fills hub lists with mock rows so design can be reviewed in Edit/Play Mode.</summary>
        public void ApplyDesignPreview(FriendsHubTab tab)
        {
            _designPreviewActive = true;
            _activeTab = tab;
            ApplyModeVisibility();
            ApplyActiveTab();

            if (_friendsListContainer != null)
            {
                _friendsListContainer.Clear();
                if (_friendsCountLabel != null)
                {
                    _friendsCountLabel.text = "5";
                }

                // Offline
                _friendsListContainer.Add(BuildFriendRow(new FriendPresenceInfo(
                    "friend-offline",
                    "Omega#0001",
                    "Offline",
                    false,
                    string.Empty)));
                // In menu
                _friendsListContainer.Add(BuildFriendRow(new FriendPresenceInfo(
                    "friend-menu",
                    "Alpha#1234",
                    FriendsHubRules.StatusInLauncher,
                    true,
                    string.Empty)));
                // Lobby with free slots + green join
                _friendsListContainer.Add(BuildFriendRow(new FriendPresenceInfo(
                    "friend-lobby-open",
                    "Beta#5678",
                    FriendsHubRules.StatusInGame,
                    true,
                    "WXYZ",
                    occupiedSlots: 2,
                    maxSlots: 4)));
                // Lobby almost full
                _friendsListContainer.Add(BuildFriendRow(new FriendPresenceInfo(
                    "friend-lobby-one",
                    "Sigma#7777",
                    FriendsHubRules.StatusInGame,
                    true,
                    "ABCD",
                    occupiedSlots: 3,
                    maxSlots: 4)));
                // Lobby full — no join
                _friendsListContainer.Add(BuildFriendRow(new FriendPresenceInfo(
                    "friend-lobby-full",
                    "Theta#9999",
                    FriendsHubRules.StatusInGame,
                    true,
                    "FULL",
                    occupiedSlots: 4,
                    maxSlots: 4)));
            }

            if (_incomingList != null)
            {
                _incomingList.Clear();
                _incomingList.Add(BuildIncomingRow(new FriendRequestInfo("a1b2c3d4e5f67890", "Gamma#9012")));
                _incomingList.Add(BuildIncomingRow(new FriendRequestInfo("f0e1d2c3b4a59687", "Delta#3456")));
            }

            UpdateInvitesTabLabel(2);
            SetError(string.Empty);
        }

        public void ClearDesignPreview()
        {
            if (!_designPreviewActive)
            {
                return;
            }

            _designPreviewActive = false;
            Refresh();
        }

        public void Refresh()
        {
            if (_designPreviewActive)
            {
                return;
            }

            RefreshFriendsList();
            RefreshIncomingRequests();
            RefreshOutgoingHint();
        }

        void ApplyModeVisibility()
        {
            var inviteOnly = _mode == FriendsHubPanelMode.InviteOnly;
            _addFriendSection?.EnableInClassList(OverlayHiddenClass, inviteOnly);
            _friendsTabButton?.EnableInClassList(OverlayHiddenClass, inviteOnly);
            _invitesTabButton?.EnableInClassList(OverlayHiddenClass, inviteOnly);
            if (_friendsTabButton?.parent != null)
            {
                _friendsTabButton.parent.EnableInClassList(OverlayHiddenClass, inviteOnly);
            }

            if (inviteOnly)
            {
                _friendsTabContent?.EnableInClassList(OverlayHiddenClass, false);
                _invitesTabContent?.EnableInClassList(OverlayHiddenClass, true);
            }
        }

        void OnHubChanged() => Refresh();

        void OnFriendsTabClicked() => SetActiveTab(FriendsHubTab.Friends);

        void OnInvitesTabClicked() => SetActiveTab(FriendsHubTab.Invites);

        void ApplyActiveTab()
        {
            if (_mode == FriendsHubPanelMode.InviteOnly)
            {
                return;
            }

            var invites = FriendsHubRules.IsInvitesTab(_activeTab);
            _friendsTabContent?.EnableInClassList(OverlayHiddenClass, invites);
            _invitesTabContent?.EnableInClassList(OverlayHiddenClass, !invites);
            _friendsTabButton?.EnableInClassList(ActiveTabClass, !invites);
            _invitesTabButton?.EnableInClassList(ActiveTabClass, invites);
        }

        void RefreshFriendsList()
        {
            if (_friendsListContainer == null)
            {
                return;
            }

            _friendsListContainer.Clear();
            var friends = FriendsHubService.GetFriendsSnapshot();
            if (_friendsCountLabel != null)
            {
                _friendsCountLabel.text = friends.Count.ToString();
            }

            if (friends.Count == 0)
            {
                _friendsListContainer.Add(CreateMetaLabel(
                    _mode == FriendsHubPanelMode.InviteOnly
                        ? "Нет друзей для приглашения."
                        : "Нет друзей. Добавьте по имени (Ник#1234)."));
                return;
            }

            foreach (var friend in friends)
            {
                _friendsListContainer.Add(BuildFriendRow(friend));
            }
        }

        VisualElement BuildFriendRow(FriendPresenceInfo friend)
        {
            var row = new VisualElement();
            row.AddToClassList("mm__friend-row");

            var line = FriendsHubRules.FormatFriendLine(
                friend.Name,
                friend.Status,
                friend.IsOnline,
                friend.LobbyCode,
                friend.OccupiedSlots,
                friend.MaxSlots);
            var label = new Label(line);
            label.AddToClassList("mm__friend-row__text");
            row.Add(label);

            var actions = new VisualElement();
            actions.AddToClassList("mm__friend-row__actions");

            if (FriendsHubRules.TryGetJoinableLobbyCode(friend.Status, friend.LobbyCode, out var joinCode))
            {
                if (FriendsHubRules.IsLobbyFull(friend.OccupiedSlots, friend.MaxSlots))
                {
                    var fullLabel = new Label(FriendsHubRules.LobbyFullLabel);
                    fullLabel.AddToClassList("mm__friend-row__full");
                    actions.Add(fullLabel);
                }
                else
                {
                    var joinButton = new Button { text = "ВОЙТИ" };
                    joinButton.AddToClassList("mm__friend-row__btn");
                    joinButton.AddToClassList("mm__friend-row__btn--join");
                    joinButton.clicked += () => JoinLobbyRequested?.Invoke(joinCode);
                    actions.Add(joinButton);
                }
            }
            else if (_mode == FriendsHubPanelMode.InviteOnly && friend.IsOnline)
            {
                var inviteButton = new Button { text = "ПРИГЛАСИТЬ" };
                inviteButton.AddToClassList("mm__friend-row__btn");
                inviteButton.clicked += () => InviteFriendAsync(friend.PlayerId).Forget();
                actions.Add(inviteButton);
            }

            if (actions.childCount > 0)
            {
                row.Add(actions);
            }

            return row;
        }

        void RefreshIncomingRequests()
        {
            if (_incomingList == null)
            {
                return;
            }

            _incomingList.Clear();
            var incoming = FriendsHubService.GetIncomingRequestsSnapshot();
            UpdateInvitesTabLabel(incoming.Count);

            if (incoming.Count == 0)
            {
                _incomingList.Add(CreateMetaLabel(FriendsHubRules.IncomingEmptyText));
                return;
            }

            foreach (var request in incoming)
            {
                _incomingList.Add(BuildIncomingRow(request));
            }
        }

        void UpdateInvitesTabLabel(int pendingCount)
        {
            if (_invitesTabButton != null)
            {
                _invitesTabButton.text = FriendsHubRules.FormatInvitesTabLabel(pendingCount);
            }
        }

        VisualElement BuildIncomingRow(FriendRequestInfo request)
        {
            var row = new VisualElement();
            row.AddToClassList("mm__friend-request-row");

            var label = new Label(FriendsHubRules.FormatIncomingRequestLine(request.Name, request.PlayerId));
            label.AddToClassList("mm__friend-request-row__text");
            row.Add(label);

            var actions = new VisualElement();
            actions.AddToClassList("mm__friend-row__actions");

            var acceptButton = new Button { text = "✓" };
            acceptButton.clicked += () => AcceptRequestAsync(request.PlayerId).Forget();
            acceptButton.AddToClassList("mm__friend-row__btn");
            acceptButton.AddToClassList("mm__friend-row__btn--accept");
            actions.Add(acceptButton);

            var declineButton = new Button { text = "✕" };
            declineButton.AddToClassList("mm__friend-row__btn");
            declineButton.AddToClassList("mm__friend-row__btn--decline");
            declineButton.clicked += () => DeclineRequestAsync(request.PlayerId).Forget();
            actions.Add(declineButton);

            row.Add(actions);
            return row;
        }

        void RefreshOutgoingHint()
        {
            if (_mode != FriendsHubPanelMode.Full || _friendsListContainer == null)
            {
                return;
            }

            var outgoing = FriendsHubService.GetOutgoingRequestsSnapshot();
            if (outgoing.Count == 0)
            {
                return;
            }

            var hint = CreateMetaLabel($"Ожидают подтверждения: {outgoing.Count}");
            hint.AddToClassList("mm__friends-outgoing");
            _friendsListContainer.Insert(0, hint);
        }

        static Label CreateMetaLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("ui-meta");
            label.AddToClassList("mm__friends-list");
            return label;
        }

        void OnAddFriendClicked() => AddFriendAsync().Forget();

        async UniTaskVoid AddFriendAsync()
        {
            try
            {
                var name = _friendIdField?.value;
                if (!FriendsHubRules.IsValidUgsPlayerName(name))
                {
                    SetError("Введите имя в формате Ник#1234.");
                    return;
                }

                await FriendsHubService.SendFriendRequestByNameAsync(name);
                if (_friendIdField != null)
                {
                    _friendIdField.value = string.Empty;
                }

                SetError("Запрос отправлен.");
                Refresh();
            }
            catch (Exception ex)
            {
                SetError(FormatFriendError(ex));
            }
        }

        async UniTaskVoid AcceptRequestAsync(string playerId)
        {
            if (_designPreviewActive)
            {
                return;
            }

            try
            {
                await FriendsHubService.AcceptFriendRequestAsync(playerId);
                SetError(string.Empty);
                Refresh();
            }
            catch (Exception ex)
            {
                SetError(FormatFriendError(ex));
            }
        }

        async UniTaskVoid DeclineRequestAsync(string playerId)
        {
            if (_designPreviewActive)
            {
                return;
            }

            try
            {
                await FriendsHubService.DeclineFriendRequestAsync(playerId);
                SetError(string.Empty);
                Refresh();
            }
            catch (Exception ex)
            {
                SetError(FormatFriendError(ex));
            }
        }

        public async UniTask InviteFriendAsync(string friendPlayerId)
        {
            try
            {
                var lobbyCode = MatchNetworkSession.RoomCode;
                if (string.IsNullOrWhiteSpace(lobbyCode))
                {
                    SetError("Код комнаты ещё не готов.");
                    return;
                }

                await FriendsHubService.InviteFriendToLobbyAsync(friendPlayerId, lobbyCode);
                SetError("Приглашение отправлено.");
            }
            catch (Exception ex)
            {
                SetError(FormatFriendError(ex));
            }
        }

        void SetError(string message)
        {
            if (_friendsErrorLabel != null)
            {
                _friendsErrorLabel.text = message ?? string.Empty;
            }
        }

        static string FormatFriendError(Exception ex)
        {
            var message = ex?.Message ?? string.Empty;
            if (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                || (message.IndexOf("user", StringComparison.OrdinalIgnoreCase) >= 0
                    && message.IndexOf("found", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Игрок с таким именем не найден. Проверьте Ник#1234.";
            }

            if (message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Запрос уже отправлен или вы уже друзья.";
            }

            return string.IsNullOrWhiteSpace(message)
                ? "Не удалось выполнить действие с друзьями."
                : message;
        }

        static void SetContainerInteractable(VisualElement container, bool interactable)
        {
            if (container == null)
            {
                return;
            }

            foreach (var child in container.Children())
            {
                child.SetEnabled(interactable);
            }
        }
    }
}
