using System;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    public readonly struct PendingMatchReconnectState
    {
        public PendingMatchReconnectState(
            string roomCode,
            string lobbyId,
            int slot,
            int playerCount,
            string sessionToken,
            long savedUtcTicks)
        {
            RoomCode = roomCode ?? string.Empty;
            LobbyId = lobbyId ?? string.Empty;
            Slot = slot;
            PlayerCount = playerCount;
            SessionToken = sessionToken ?? string.Empty;
            SavedUtcTicks = savedUtcTicks;
        }

        public string RoomCode { get; }
        public string LobbyId { get; }
        public int Slot { get; }
        public int PlayerCount { get; }
        public string SessionToken { get; }
        public long SavedUtcTicks { get; }

        public bool HasValue =>
            !string.IsNullOrWhiteSpace(RoomCode)
            && Slot >= 0
            && !string.IsNullOrWhiteSpace(SessionToken);
    }

    /// <summary>PlayerPrefs-backed reconnect token so a closed client can return to the match.</summary>
    public static class PendingMatchReconnectStore
    {
        const string KeyRoom = "baraki.reconnect.room";
        const string KeyLobbyId = "baraki.reconnect.lobbyId";
        const string KeySlot = "baraki.reconnect.slot";
        const string KeyPlayerCount = "baraki.reconnect.playerCount";
        const string KeyToken = "baraki.reconnect.token";
        const string KeySavedUtcTicks = "baraki.reconnect.savedUtcTicks";

        public static void Save(PendingMatchReconnectState state)
        {
            if (!state.HasValue)
            {
                Clear();
                return;
            }

            PlayerPrefs.SetString(KeyRoom, state.RoomCode);
            PlayerPrefs.SetString(KeyLobbyId, state.LobbyId);
            PlayerPrefs.SetInt(KeySlot, state.Slot);
            PlayerPrefs.SetInt(KeyPlayerCount, state.PlayerCount);
            PlayerPrefs.SetString(KeyToken, state.SessionToken);
            PlayerPrefs.SetString(KeySavedUtcTicks, state.SavedUtcTicks.ToString());
            PlayerPrefs.Save();
        }

        public static bool TryLoad(out PendingMatchReconnectState state)
        {
            state = default;
            if (!PlayerPrefs.HasKey(KeyToken) || !PlayerPrefs.HasKey(KeyRoom))
            {
                return false;
            }

            var savedTicks = 0L;
            long.TryParse(PlayerPrefs.GetString(KeySavedUtcTicks, "0"), out savedTicks);
            state = new PendingMatchReconnectState(
                PlayerPrefs.GetString(KeyRoom, string.Empty),
                PlayerPrefs.GetString(KeyLobbyId, string.Empty),
                PlayerPrefs.GetInt(KeySlot, -1),
                PlayerPrefs.GetInt(KeyPlayerCount, 0),
                PlayerPrefs.GetString(KeyToken, string.Empty),
                savedTicks);
            return state.HasValue;
        }

        public static bool TryLoadActive(out PendingMatchReconnectState state)
        {
            if (!TryLoad(out state))
            {
                return false;
            }

            if (!PendingMatchReconnectRules.CanShowReturnButton(
                    true,
                    state.SavedUtcTicks,
                    DateTime.UtcNow.Ticks))
            {
                Clear();
                state = default;
                return false;
            }

            return true;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyRoom);
            PlayerPrefs.DeleteKey(KeyLobbyId);
            PlayerPrefs.DeleteKey(KeySlot);
            PlayerPrefs.DeleteKey(KeyPlayerCount);
            PlayerPrefs.DeleteKey(KeyToken);
            PlayerPrefs.DeleteKey(KeySavedUtcTicks);
            PlayerPrefs.Save();
        }
    }
}
