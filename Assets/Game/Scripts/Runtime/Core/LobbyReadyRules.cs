using System;

namespace Game.Core
{
    /// <summary>
    /// Pure lobby gate: host may start only when every slot is occupied and ready.
    /// </summary>
    public static class LobbyReadyRules
    {
        public static bool CanHostStart(IReadOnlyLobbySlots lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            if (lobby.PlayerCount < MatchModeRules.MinPlayers ||
                lobby.PlayerCount > MatchModeRules.MaxPlayers)
            {
                return false;
            }

            if (lobby.SlotCount != lobby.PlayerCount)
            {
                return false;
            }

            for (var i = 0; i < lobby.SlotCount; i++)
            {
                var slot = lobby.GetSlot(i);
                if (!slot.IsOccupied || !slot.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        public static int CountOccupied(IReadOnlyLobbySlots lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            var occupied = 0;
            for (var i = 0; i < lobby.SlotCount; i++)
            {
                if (lobby.GetSlot(i).IsOccupied)
                {
                    occupied++;
                }
            }

            return occupied;
        }
    }

    public interface IReadOnlyLobbySlots
    {
        int PlayerCount { get; }
        int SlotCount { get; }
        LobbySlotInfo GetSlot(int index);
    }

    public readonly struct LobbySlotInfo
    {
        public LobbySlotInfo(bool isOccupied, bool isReady, string displayName)
        {
            IsOccupied = isOccupied;
            IsReady = isReady;
            DisplayName = displayName ?? string.Empty;
        }

        public bool IsOccupied { get; }
        public bool IsReady { get; }
        public string DisplayName { get; }
    }
}
