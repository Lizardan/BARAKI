using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Server-authoritative per-barracks wave timers. One timer per barracks, not a global tick.
    /// </summary>
    public sealed class BarracksWaveScheduler
    {
        private static readonly string[] s_barracksIds =
        {
            GameIds.Buildings.BarracksLeft,
            GameIds.Buildings.BarracksCenter,
            GameIds.Buildings.BarracksRight,
        };

        private readonly List<BarracksWaveState> _barracks = new();
        private bool _isActive;

        public event Action<BarracksWaveFired> WaveFired;

        public IReadOnlyList<BarracksWaveState> Barracks => _barracks;
        public bool IsActive => _isActive;

        public void Initialize(IReadOnlyList<MatchPlayerState> players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            _barracks.Clear();
            _isActive = false;

            for (var slot = 0; slot < players.Count; slot++)
            {
                var player = players[slot];
                for (var i = 0; i < s_barracksIds.Length; i++)
                {
                    var barracksId = s_barracksIds[i];
                    var laneId = BaseLayoutDefinition.GetLaneForBarracks(barracksId);
                    var laneIndex = BaseLayoutDefinition.GetLaneIndex(barracksId);
                    var interval = BarracksWaveRules.GetWaveIntervalSeconds(1, false, player.RaceId);

                    _barracks.Add(new BarracksWaveState(
                        slot,
                        player.RaceId,
                        barracksId,
                        laneId,
                        laneIndex,
                        interval,
                        interval));
                }
            }
        }

        public void Activate()
        {
            _isActive = true;
        }

        public void Deactivate()
        {
            _isActive = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive || deltaTime <= 0f)
            {
                return;
            }

            for (var i = 0; i < _barracks.Count; i++)
            {
                var barracks = _barracks[i];
                if (!barracks.IsSpawnEnabled)
                {
                    continue;
                }

                barracks.TimeUntilNextWaveSeconds -= deltaTime;
                if (barracks.TimeUntilNextWaveSeconds > 0f)
                {
                    continue;
                }

                FireWave(barracks);
                barracks.TimeUntilNextWaveSeconds += barracks.WaveIntervalSeconds;
            }
        }

        public void SetBarracksLevel(int ownerSlot, string barracksId, int level)
        {
            var barracks = GetBarracks(ownerSlot, barracksId);
            if (barracks == null || barracks.IsRuins)
            {
                return;
            }

            barracks.Level = UnityEngine.Mathf.Clamp(level, 1, 4);
            barracks.RefreshInterval();
        }

        public void SetBarracksRuins(int ownerSlot, string barracksId)
        {
            var barracks = GetBarracks(ownerSlot, barracksId);
            if (barracks == null || barracks.IsRuins)
            {
                return;
            }

            barracks.FrozenSquadLevel = barracks.Level;
            barracks.IsRuins = true;
            barracks.RefreshInterval();
        }

        public void SetPlayerSpawnEnabled(int ownerSlot, bool enabled)
        {
            for (var i = 0; i < _barracks.Count; i++)
            {
                if (_barracks[i].OwnerSlot == ownerSlot)
                {
                    _barracks[i].IsSpawnEnabled = enabled;
                }
            }
        }

        public BarracksWaveState GetBarracks(int ownerSlot, string barracksId)
        {
            for (var i = 0; i < _barracks.Count; i++)
            {
                var barracks = _barracks[i];
                if (barracks.OwnerSlot == ownerSlot && barracks.BarracksId == barracksId)
                {
                    return barracks;
                }
            }

            return null;
        }

        private void FireWave(BarracksWaveState barracks)
        {
            var squadLevel = barracks.EffectiveSquadLevel;
            var payload = new BarracksWaveFired(
                barracks.OwnerSlot,
                barracks.BarracksId,
                barracks.LaneId,
                barracks.OwnerRaceId,
                squadLevel,
                BarracksWaveRules.GetSquadId(squadLevel));

            WaveFired?.Invoke(payload);
        }
    }
}
