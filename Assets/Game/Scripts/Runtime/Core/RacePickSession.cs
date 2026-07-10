using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Pre-match race selection. Match starts only when every slot has a race id.
    /// Offline MVP: local player picks; unpicked slots are filled with random playable races.
    /// </summary>
    public sealed class RacePickSession
    {
        private readonly string[] _raceIds;

        public RacePickSession(int playerCount, int localPlayerSlot)
        {
            if (playerCount < 2 || playerCount > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be 2..8.");
            }

            if (localPlayerSlot < 0 || localPlayerSlot >= playerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerSlot));
            }

            PlayerCount = playerCount;
            LocalPlayerSlot = localPlayerSlot;
            _raceIds = new string[playerCount];
        }

        public int PlayerCount { get; }
        public int LocalPlayerSlot { get; }

        public bool HasLocalPick => _raceIds[LocalPlayerSlot] != null;

        public bool IsComplete
        {
            get
            {
                for (var i = 0; i < _raceIds.Length; i++)
                {
                    if (_raceIds[i] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public string GetRaceId(int slot)
        {
            if (slot < 0 || slot >= PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return _raceIds[slot];
        }

        public void SetLocalPick(string raceId)
        {
            if (!RacePickRules.IsPlayable(raceId))
            {
                throw new ArgumentException($"Race is not playable: {raceId}", nameof(raceId));
            }

            _raceIds[LocalPlayerSlot] = raceId;
        }

        public void SetPick(int slot, string raceId)
        {
            if (slot < 0 || slot >= PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            if (!RacePickRules.IsPlayable(raceId))
            {
                throw new ArgumentException($"Race is not playable: {raceId}", nameof(raceId));
            }

            _raceIds[slot] = raceId;
        }

        public void FillRandomForUnpickedSlots(Random random = null)
        {
            random ??= new Random();

            for (var slot = 0; slot < PlayerCount; slot++)
            {
                if (_raceIds[slot] != null)
                {
                    continue;
                }

                _raceIds[slot] = RacePickRules.PickRandomRace(random);
            }
        }

        public void ConfirmOfflinePick(Random random = null)
        {
            if (!HasLocalPick)
            {
                throw new InvalidOperationException("Local player must pick a race first.");
            }

            FillRandomForUnpickedSlots(random);
        }

        public string[] ToRaceIdsArray()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("Not all players have picked a race.");
            }

            var copy = new string[PlayerCount];
            Array.Copy(_raceIds, copy, PlayerCount);
            return copy;
        }
    }
}
