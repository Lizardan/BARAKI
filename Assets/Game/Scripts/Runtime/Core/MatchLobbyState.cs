using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Mutable lobby model for LocalDev and unit tests. N fixed at create.
    /// </summary>
    public sealed class MatchLobbyState : IReadOnlyLobbySlots
    {
        readonly LobbySlot[] _slots;

        public MatchLobbyState(int playerCount, string roomCode, string hostDisplayName)
        {
            if (!MatchModeRules.IsValidPlayerCount(playerCount))
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            if (string.IsNullOrWhiteSpace(roomCode))
            {
                throw new ArgumentException("Room code is required.", nameof(roomCode));
            }

            PlayerCount = playerCount;
            RoomCode = roomCode.Trim().ToUpperInvariant();
            HostSlotIndex = 0;
            _slots = new LobbySlot[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                _slots[i] = new LobbySlot();
            }

            Occupy(0, hostDisplayName ?? "Host", isLocalStandIn: false);
        }

        public int PlayerCount { get; }
        public string RoomCode { get; }
        public int HostSlotIndex { get; }
        public int SlotCount => _slots.Length;
        public bool MatchStarted { get; private set; }
        public int Revision { get; private set; }

        public LobbySlotInfo GetSlot(int index)
        {
            var slot = _slots[index];
            return new LobbySlotInfo(slot.IsOccupied, slot.IsReady, slot.DisplayName);
        }

        public int OccupyNext(string displayName, bool isLocalStandIn = false)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsOccupied)
                {
                    Occupy(i, displayName, isLocalStandIn);
                    return i;
                }
            }

            throw new InvalidOperationException("Lobby is full.");
        }

        public void SetReady(int slotIndex, bool isReady)
        {
            EnsureSlot(slotIndex);
            if (!_slots[slotIndex].IsOccupied)
            {
                throw new InvalidOperationException("Slot is empty.");
            }

            _slots[slotIndex].IsReady = isReady;
            BumpRevision();
        }

        public void FillEmptySlotsWithLocalStandIns()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsOccupied)
                {
                    continue;
                }

                Occupy(i, $"Игрок {i + 1}", isLocalStandIn: true);
                _slots[i].IsReady = true;
            }

            BumpRevision();
        }

        public bool TryMarkMatchStarted()
        {
            if (!LobbyReadyRules.CanHostStart(this) || MatchStarted)
            {
                return false;
            }

            MatchStarted = true;
            BumpRevision();
            return true;
        }

        public MatchSetup ToMatchSetup(int localPlayerSlot)
        {
            return new MatchSetup(PlayerCount, localPlayerSlot);
        }

        void Occupy(int slotIndex, string displayName, bool isLocalStandIn)
        {
            EnsureSlot(slotIndex);
            _slots[slotIndex].IsOccupied = true;
            _slots[slotIndex].DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? $"Игрок {slotIndex + 1}"
                : displayName.Trim();
            _slots[slotIndex].IsLocalStandIn = isLocalStandIn;
            _slots[slotIndex].IsReady = false;
            BumpRevision();
        }

        void BumpRevision() => Revision++;

        void EnsureSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }
        }

        sealed class LobbySlot
        {
            public bool IsOccupied;
            public bool IsReady;
            public bool IsLocalStandIn;
            public string DisplayName = string.Empty;
        }
    }

    /// <summary>Process-wide LocalDev lobby registry (same Editor / first networked backend).</summary>
    public static class LocalMatchRegistry
    {
        static readonly Dictionary<string, MatchLobbyState> s_lobbies =
            new(StringComparer.OrdinalIgnoreCase);

        public static MatchLobbyState Active { get; set; }

        public static int? LocalPlayerSlot { get; set; }

        public static MatchLobbyState Create(int playerCount, string hostDisplayName)
        {
            var code = GenerateRoomCode();
            var lobby = new MatchLobbyState(playerCount, code, hostDisplayName);
            s_lobbies[lobby.RoomCode] = lobby;
            Active = lobby;
            LocalPlayerSlot = 0;
            return lobby;
        }

        public static MatchLobbyState Join(string roomCode, string displayName)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                throw new ArgumentException("Room code is required.", nameof(roomCode));
            }

            var key = roomCode.Trim().ToUpperInvariant();
            if (!s_lobbies.TryGetValue(key, out var lobby))
            {
                throw new InvalidOperationException("Лобби не найдено.");
            }

            if (lobby.MatchStarted)
            {
                throw new InvalidOperationException("Матч уже начат.");
            }

            var slot = lobby.OccupyNext(displayName);
            Active = lobby;
            LocalPlayerSlot = slot;
            return lobby;
        }

        public static void Clear()
        {
            s_lobbies.Clear();
            Active = null;
            LocalPlayerSlot = null;
        }

        static string GenerateRoomCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[4];
            var rng = new Random();
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[rng.Next(alphabet.Length)];
            }

            var code = new string(chars);
            return s_lobbies.ContainsKey(code) ? GenerateRoomCode() : code;
        }
    }
}
