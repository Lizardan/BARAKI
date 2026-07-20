using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    [Serializable]
    public sealed class BarakiPresenceActivity
    {
        public string status = FriendsHubRules.StatusInLauncher;
        public string lobbyCode = string.Empty;
        public int occupiedSlots;
        public int maxSlots;
    }

    [Serializable]
    public sealed class BarakiFriendsLobbyInviteMessage
    {
        public string senderName = string.Empty;
        public string lobbyCode = string.Empty;
    }

    public readonly struct FriendPresenceInfo
    {
        public FriendPresenceInfo(
            string playerId,
            string name,
            string status,
            bool isOnline,
            string lobbyCode,
            int occupiedSlots = 0,
            int maxSlots = 0)
        {
            PlayerId = playerId ?? string.Empty;
            Name = name ?? string.Empty;
            Status = status ?? "Offline";
            IsOnline = isOnline;
            LobbyCode = lobbyCode ?? string.Empty;
            OccupiedSlots = occupiedSlots < 0 ? 0 : occupiedSlots;
            MaxSlots = maxSlots < 0 ? 0 : maxSlots;
        }

        public string PlayerId { get; }
        public string Name { get; }
        public string Status { get; }
        public bool IsOnline { get; }
        public string LobbyCode { get; }
        public int OccupiedSlots { get; }
        public int MaxSlots { get; }
    }

    public readonly struct FriendRequestInfo
    {
        public FriendRequestInfo(string playerId, string name)
        {
            PlayerId = playerId ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public string PlayerId { get; }
        public string Name { get; }
    }

    public readonly struct FriendsLobbyInvite
    {
        public FriendsLobbyInvite(string senderPlayerId, string senderName, string lobbyCode)
        {
            SenderPlayerId = senderPlayerId ?? string.Empty;
            SenderName = senderName ?? string.Empty;
            LobbyCode = FriendsHubRules.NormalizeLobbyCode(lobbyCode);
        }

        public string SenderPlayerId { get; }
        public string SenderName { get; }
        public string LobbyCode { get; }
    }

    /// <summary>UGS Friends list, requests, presence, and lobby invites for hub UI.</summary>
    public static class FriendsHubService
    {
        static bool s_initialized;
        static bool s_eventsHooked;
        static readonly List<FriendPresenceInfo> s_friends = new();
        static readonly List<FriendRequestInfo> s_incoming = new();
        static readonly List<FriendRequestInfo> s_outgoing = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetForPlaySession()
        {
            s_initialized = false;
            s_eventsHooked = false;
        }

        public static event Action HubChanged;
        public static event Action<FriendsLobbyInvite> LobbyInviteReceived;

        public static string LocalPlayerId =>
            UnityServicesBootstrap.IsReady ? UnityServicesBootstrap.PlayerId : string.Empty;

        public static string LocalPlayerName =>
            UnityServicesBootstrap.PlayerName;

        public static async UniTask InitializeAsync()
        {
            await UnityServicesBootstrap.EnsureInitializedAsync();
            if (!UnityServicesBootstrap.IsReady)
            {
                return;
            }

            await FriendsService.Instance.InitializeAsync();
            s_initialized = true;
            EnsureEventHooks();
            await EnsureLocalPlayerNameSyncedAsync();
            RefreshAllCaches();
            // Presence is best-effort — do not block hub UI on another ~10s network round-trip.
            SetPresenceAsync(FriendsHubRules.StatusInLauncher).Forget();
        }

        static async UniTask EnsureLocalPlayerNameSyncedAsync()
        {
            var playerName = UnityServicesBootstrap.PlayerName;
            if (!FriendsHubRules.IsValidUgsPlayerName(playerName))
            {
                playerName = await UnityServicesBootstrap.EnsurePlayerNameAsync();
            }

            if (!PlayerProfileService.IsLoaded)
            {
                return;
            }

            if (!FriendsHubRules.ShouldSyncDisplayNameToUgs(playerName, PlayerProfileService.DisplayName))
            {
                return;
            }

            await UnityServicesBootstrap.TrySyncPlayerNameFromDisplayNameAsync(PlayerProfileService.DisplayName);
        }

        public static async UniTask SetPresenceAsync(
            string status,
            string lobbyCode = null,
            int occupiedSlots = 0,
            int maxSlots = 0)
        {
            try
            {
                await UnityServicesBootstrap.EnsureInitializedAsync();
                if (!UnityServicesBootstrap.IsReady
                    || UnityServices.State != ServicesInitializationState.Initialized)
                {
                    return;
                }

                if (!s_initialized)
                {
                    await FriendsService.Instance.InitializeAsync();
                    s_initialized = true;
                    EnsureEventHooks();
                }

                var activity = new BarakiPresenceActivity
                {
                    status = status ?? FriendsHubRules.StatusInLauncher,
                    lobbyCode = lobbyCode ?? string.Empty,
                    occupiedSlots = occupiedSlots < 0 ? 0 : occupiedSlots,
                    maxSlots = maxSlots < 0 ? 0 : maxSlots,
                };
                await FriendsService.Instance.SetPresenceAsync(Availability.Online, activity);
            }
            catch (Exception ex)
            {
                // LocalDev / offline Editor: Friends are optional; never surface via Forget().
                Debug.LogWarning($"FriendsHubService: presence skipped: {ex.Message}");
            }
        }

        public static async UniTask SendFriendRequestByNameAsync(string playerName)
        {
            var normalized = FriendsHubRules.NormalizeUgsPlayerName(playerName);
            if (!FriendsHubRules.IsValidUgsPlayerName(normalized))
            {
                throw new ArgumentException("Укажите имя в формате Ник#1234.", nameof(playerName));
            }

            var localName = await UnityServicesBootstrap.EnsurePlayerNameAsync();
            if (string.Equals(normalized, localName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Нельзя добавить самого себя.");
            }

            await InitializeAsync();
            await FriendsService.Instance.AddFriendByNameAsync(normalized);
            await FriendsService.Instance.ForceRelationshipsRefreshAsync();
            RefreshAllCaches();
        }

        public static async UniTask SendFriendRequestByIdAsync(string playerId)
        {
            var normalized = FriendsHubRules.NormalizePlayerId(playerId);
            if (!FriendsHubRules.IsValidPlayerId(normalized))
            {
                throw new ArgumentException("Player ID is too short.", nameof(playerId));
            }

            if (string.Equals(normalized, LocalPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Нельзя добавить самого себя.");
            }

            await InitializeAsync();
            await FriendsService.Instance.AddFriendAsync(normalized);
            await FriendsService.Instance.ForceRelationshipsRefreshAsync();
            RefreshAllCaches();
        }

        public static async UniTask AcceptFriendRequestAsync(string playerId)
        {
            var normalized = FriendsHubRules.NormalizePlayerId(playerId);
            await InitializeAsync();
            await FriendsService.Instance.AddFriendAsync(normalized);
            await FriendsService.Instance.ForceRelationshipsRefreshAsync();
            RefreshAllCaches();
        }

        public static async UniTask DeclineFriendRequestAsync(string playerId)
        {
            var normalized = FriendsHubRules.NormalizePlayerId(playerId);
            await InitializeAsync();
            await FriendsService.Instance.DeleteIncomingFriendRequestAsync(normalized);
            await FriendsService.Instance.ForceRelationshipsRefreshAsync();
            RefreshAllCaches();
        }

        public static async UniTask InviteFriendToLobbyAsync(string friendPlayerId, string lobbyCode)
        {
            var normalizedFriendId = FriendsHubRules.NormalizePlayerId(friendPlayerId);
            var normalizedLobbyCode = FriendsHubRules.NormalizeLobbyCode(lobbyCode);
            if (!FriendsHubRules.IsValidPlayerId(normalizedFriendId))
            {
                throw new ArgumentException("Friend player ID is invalid.", nameof(friendPlayerId));
            }

            if (normalizedLobbyCode.Length < 4)
            {
                throw new ArgumentException("Lobby code is required.", nameof(lobbyCode));
            }

            await InitializeAsync();
            var occupied = 0;
            var maxSlots = 0;
            if (MatchNetworkSession.HasNetworkLobby)
            {
                maxSlots = MatchNetworkSession.LobbySlotCount;
                for (var i = 0; i < maxSlots; i++)
                {
                    if (MatchNetworkSession.GetLobbySlot(i).IsOccupied)
                    {
                        occupied++;
                    }
                }
            }
            else if (MatchNetworkSession.PlayerCount > 0)
            {
                occupied = 1;
                maxSlots = MatchNetworkSession.PlayerCount;
            }

            await SetPresenceAsync(
                FriendsHubRules.StatusInGame,
                normalizedLobbyCode,
                occupied,
                maxSlots);

            var senderName = UnityServicesBootstrap.PlayerName;
            if (string.IsNullOrWhiteSpace(senderName))
            {
                senderName = PlayerProfileService.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(senderName))
            {
                senderName = "Player";
            }

            var message = new BarakiFriendsLobbyInviteMessage
            {
                senderName = senderName,
                lobbyCode = normalizedLobbyCode,
            };
            await FriendsService.Instance.MessageAsync(normalizedFriendId, message);
        }

        public static IReadOnlyList<FriendPresenceInfo> GetFriendsSnapshot()
        {
            if (IsOperational())
            {
                RefreshFriendsCache();
            }

            return s_friends;
        }

        public static IReadOnlyList<FriendRequestInfo> GetIncomingRequestsSnapshot()
        {
            if (IsOperational())
            {
                RefreshIncomingCache();
            }

            return s_incoming;
        }

        public static IReadOnlyList<FriendRequestInfo> GetOutgoingRequestsSnapshot()
        {
            if (IsOperational())
            {
                RefreshOutgoingCache();
            }

            return s_outgoing;
        }

        static bool IsOperational()
        {
            if (!UnityServicesBootstrap.IsReady)
            {
                s_initialized = false;
                s_eventsHooked = false;
                return false;
            }

            return s_initialized;
        }

        static void EnsureEventHooks()
        {
            if (s_eventsHooked || !UnityServicesBootstrap.IsReady)
            {
                return;
            }

            FriendsService.Instance.MessageReceived += OnMessageReceived;
            FriendsService.Instance.RelationshipAdded += OnRelationshipAdded;
            FriendsService.Instance.RelationshipDeleted += OnRelationshipDeleted;
            FriendsService.Instance.PresenceUpdated += OnPresenceUpdated;
            s_eventsHooked = true;
        }

        static void OnRelationshipAdded(IRelationshipAddedEvent _) => RefreshAllCaches();
        static void OnRelationshipDeleted(IRelationshipDeletedEvent _) => RefreshAllCaches();
        static void OnPresenceUpdated(IPresenceUpdatedEvent _) => RefreshAllCaches();

        static void OnMessageReceived(IMessageReceivedEvent receivedEvent)
        {
            if (receivedEvent == null)
            {
                return;
            }

            var message = receivedEvent.GetAs<BarakiFriendsLobbyInviteMessage>();
            if (message == null || string.IsNullOrWhiteSpace(message.lobbyCode))
            {
                return;
            }

            var invite = new FriendsLobbyInvite(
                receivedEvent.UserId,
                message.senderName,
                message.lobbyCode);
            LobbyInviteReceived?.Invoke(invite);
        }

        static void RefreshAllCaches()
        {
            if (!IsOperational())
            {
                return;
            }

            RefreshFriendsCache();
            RefreshIncomingCache();
            RefreshOutgoingCache();
            HubChanged?.Invoke();
        }

        static void RefreshFriendsCache()
        {
            if (!UnityServicesBootstrap.IsReady)
            {
                s_initialized = false;
                return;
            }

            s_friends.Clear();
            foreach (var relationship in FriendsService.Instance.Friends)
            {
                var member = relationship.Member;
                if (member == null)
                {
                    continue;
                }

                s_friends.Add(BuildPresenceInfo(member));
            }
        }

        static void RefreshIncomingCache()
        {
            if (!UnityServicesBootstrap.IsReady)
            {
                s_initialized = false;
                return;
            }

            s_incoming.Clear();
            foreach (var relationship in FriendsService.Instance.IncomingFriendRequests)
            {
                var member = relationship.Member;
                if (member == null)
                {
                    continue;
                }

                s_incoming.Add(new FriendRequestInfo(
                    member.Id,
                    member.Profile?.Name ?? member.Id));
            }
        }

        static void RefreshOutgoingCache()
        {
            if (!UnityServicesBootstrap.IsReady)
            {
                s_initialized = false;
                return;
            }

            s_outgoing.Clear();
            foreach (var relationship in FriendsService.Instance.OutgoingFriendRequests)
            {
                var member = relationship.Member;
                if (member == null)
                {
                    continue;
                }

                s_outgoing.Add(new FriendRequestInfo(
                    member.Id,
                    member.Profile?.Name ?? member.Id));
            }
        }

        static FriendPresenceInfo BuildPresenceInfo(Member member)
        {
            var availability = member.Presence?.Availability ?? Availability.Offline;
            var online = availability is Availability.Online or Availability.Away or Availability.Busy;
            var status = "Offline";
            var lobbyCode = string.Empty;
            var occupiedSlots = 0;
            var maxSlots = 0;

            if (online)
            {
                status = "Online";
                var activity = member.Presence?.GetActivity<BarakiPresenceActivity>();
                if (activity != null)
                {
                    if (!string.IsNullOrWhiteSpace(activity.status))
                    {
                        status = activity.status;
                    }

                    lobbyCode = activity.lobbyCode ?? string.Empty;
                    occupiedSlots = activity.occupiedSlots;
                    maxSlots = activity.maxSlots;
                }
            }

            return new FriendPresenceInfo(
                member.Id,
                member.Profile?.Name ?? member.Id,
                status,
                online,
                lobbyCode,
                occupiedSlots,
                maxSlots);
        }
    }
}
