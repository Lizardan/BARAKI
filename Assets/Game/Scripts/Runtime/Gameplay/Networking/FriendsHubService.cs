using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    [Serializable]
    public sealed class BarakiPresenceActivity
    {
        public string status = "InLauncher";
        public string lobbyCode = string.Empty;
    }

    public readonly struct FriendPresenceInfo
    {
        public FriendPresenceInfo(string playerId, string name, string status, bool isOnline)
        {
            PlayerId = playerId ?? string.Empty;
            Name = name ?? string.Empty;
            Status = status ?? "Offline";
            IsOnline = isOnline;
        }

        public string PlayerId { get; }
        public string Name { get; }
        public string Status { get; }
        public bool IsOnline { get; }
    }

    /// <summary>UGS Friends list + presence for Main Menu hub.</summary>
    public static class FriendsHubService
    {
        static bool s_initialized;
        static readonly List<FriendPresenceInfo> s_cache = new();

        public static async UniTask InitializeAsync()
        {
            if (s_initialized)
            {
                return;
            }

            await UnityServicesBootstrap.EnsureInitializedAsync();
            await FriendsService.Instance.InitializeAsync();
            await SetPresenceAsync("InLauncher");
            RefreshCache();
            s_initialized = true;
        }

        public static async UniTask SetPresenceAsync(string status, string lobbyCode = null)
        {
            if (!s_initialized)
            {
                await UnityServicesBootstrap.EnsureInitializedAsync();
                await FriendsService.Instance.InitializeAsync();
                s_initialized = true;
            }

            var activity = new BarakiPresenceActivity
            {
                status = status ?? "InLauncher",
                lobbyCode = lobbyCode ?? string.Empty,
            };
            await FriendsService.Instance.SetPresenceAsync(Availability.Online, activity);
        }

        public static async UniTask SendFriendRequestByIdAsync(string playerId)
        {
            await InitializeAsync();
            await FriendsService.Instance.AddFriendAsync(playerId);
            RefreshCache();
        }

        public static IReadOnlyList<FriendPresenceInfo> GetFriendsSnapshot()
        {
            if (s_initialized)
            {
                RefreshCache();
            }

            return s_cache;
        }

        public static async UniTask InviteFriendToLobbyAsync(string friendPlayerId, string lobbyCode)
        {
            await SetPresenceAsync("InGame", lobbyCode);
            Debug.Log($"FriendsHubService: invite lobby={lobbyCode} friend={friendPlayerId}");
        }

        static void RefreshCache()
        {
            s_cache.Clear();
            foreach (var relationship in FriendsService.Instance.Friends)
            {
                var member = relationship.Member;
                if (member == null)
                {
                    continue;
                }

                var availability = member.Presence?.Availability ?? Availability.Offline;
                var status = "Offline";
                var online = availability is Availability.Online or Availability.Away or Availability.Busy;
                if (online)
                {
                    status = "Online";
                    var activity = member.Presence?.GetActivity<BarakiPresenceActivity>();
                    if (activity != null && !string.IsNullOrEmpty(activity.status))
                    {
                        status = activity.status;
                    }
                }

                s_cache.Add(new FriendPresenceInfo(
                    member.Id,
                    member.Profile?.Name ?? member.Id,
                    status,
                    online));
            }
        }
    }
}
