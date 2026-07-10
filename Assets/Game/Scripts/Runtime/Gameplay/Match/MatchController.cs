using System;
using System.Collections.Generic;
using Game.Gameplay.Combat;

namespace Game.Gameplay.Match
{
    /// <summary>Server-authoritative match orchestration: phases, arena layout, starting gold.</summary>
    public sealed class MatchController
    {
        private readonly List<MatchPlayerState> _players = new();
        private readonly BarracksWaveScheduler _waveScheduler = new();
        private readonly MatchCombatSystem _combat = new();
        private readonly BuildingRegistry _buildings = new();
        private readonly EliminationService _elimination = new();
        private int? _winnerSlot;

        public event Action<MatchPhase, MatchPhase> PhaseChanged;
        public event Action MatchStarted;
        public event Action<int> MatchEnded;
        public event Action<BarracksWaveFired> WaveFired;
        public event Action<UnitKillEvent> UnitKilled;

        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;
        public float MatchTimeSeconds { get; private set; }
        public MatchArenaLayout Layout { get; private set; }
        public LaneGraph Graph { get; private set; }
        public IReadOnlyList<MatchPlayerState> Players => _players;
        public BarracksWaveScheduler WaveScheduler => _waveScheduler;
        public MatchCombatSystem Combat => _combat;
        public BuildingRegistry Buildings => _buildings;
        public EliminationService Elimination => _elimination;
        public ICombatUnitCatalog CombatCatalog { get; set; }
        public int? WinnerSlot => _winnerSlot;
        public bool IsRunning => Phase is not MatchPhase.Lobby and not MatchPhase.End;

        public void StartMatch(MatchConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (Phase is not MatchPhase.Lobby and not MatchPhase.End)
            {
                throw new InvalidOperationException($"Cannot start match while phase is {Phase}.");
            }

            if (config.PlayerCount < 2 || config.PlayerCount > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(config), "Player count must be 2..8.");
            }

            if (config.RaceIds == null || config.RaceIds.Count != config.PlayerCount)
            {
                throw new ArgumentException("RaceIds count must match PlayerCount.", nameof(config));
            }

            Layout = MatchArenaGenerator.Generate(
                config.PlayerCount,
                config.ArenaRadius,
                config.MainToTowerDistance);
            Graph = LaneGraphBuilder.Build(Layout, config.CenterArenaRadius);

            _players.Clear();
            for (var slot = 0; slot < config.PlayerCount; slot++)
            {
                var raceId = config.GetRaceId(slot);
                var startingGold = MatchRules.GetStartingGold(raceId);
                _players.Add(new MatchPlayerState(slot, raceId, startingGold));
            }

            _waveScheduler.WaveFired -= OnWaveFired;
            _combat.UnitKilled -= OnUnitKilled;
            _buildings.BuildingDestroyed -= OnBuildingDestroyed;
            _waveScheduler.Initialize(_players);
            _waveScheduler.WaveFired += OnWaveFired;
            _buildings.Initialize(Layout);
            _buildings.BuildingDestroyed += OnBuildingDestroyed;
            _combat.Reset(_players, Graph);
            _combat.SetBuildings(_buildings);
            _combat.UnitKilled += OnUnitKilled;
            _waveScheduler.Deactivate();

            MatchTimeSeconds = 0f;
            _winnerSlot = null;
            SetPhase(MatchPhase.Start);
            MatchStarted?.Invoke();
        }

        public void BeginEarlyPhase()
        {
            if (Phase != MatchPhase.Start)
            {
                return;
            }

            SetPhase(MatchPhase.Early);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (Phase == MatchPhase.Lobby || Phase == MatchPhase.End)
            {
                return;
            }

            if (Phase == MatchPhase.Start)
            {
                return;
            }

            MatchTimeSeconds += deltaTime;
            _waveScheduler.Tick(deltaTime);
            _combat.Tick(deltaTime);

            var timePhase = MatchRules.ResolveTimePhase(MatchTimeSeconds);
            if (timePhase != Phase)
            {
                SetPhase(timePhase);
            }
        }

        public void EndMatch(int winnerSlot)
        {
            if (Phase == MatchPhase.Lobby)
            {
                throw new InvalidOperationException("Cannot end a match that has not started.");
            }

            if (Phase == MatchPhase.End)
            {
                return;
            }

            if (winnerSlot < 0 || winnerSlot >= _players.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(winnerSlot));
            }

            _winnerSlot = winnerSlot;
            _waveScheduler.Deactivate();
            SetPhase(MatchPhase.End);
            MatchEnded?.Invoke(winnerSlot);
        }

        private void OnWaveFired(BarracksWaveFired wave)
        {
            if (CombatCatalog != null)
            {
                _combat.HandleWave(wave, CombatCatalog);
            }

            WaveFired?.Invoke(wave);
        }

        private void OnUnitKilled(UnitKillEvent killEvent)
        {
            UnitKilled?.Invoke(killEvent);
        }

        private void OnBuildingDestroyed(BuildingDestroyedEvent destroyed)
        {
            _elimination.HandleBuildingDestroyed(
                destroyed,
                _buildings,
                _players,
                _waveScheduler,
                _combat);

            var winner = _elimination.TryGetLastStandingWinner(_players);
            if (winner.HasValue)
            {
                EndMatch(winner.Value);
            }
        }

        private void SetPhase(MatchPhase nextPhase)
        {
            if (Phase == nextPhase)
            {
                return;
            }

            var previousPhase = Phase;
            Phase = nextPhase;

            if (nextPhase == MatchPhase.Early)
            {
                _waveScheduler.Activate();
            }
            else if (nextPhase == MatchPhase.End)
            {
                _waveScheduler.Deactivate();
            }

            PhaseChanged?.Invoke(previousPhase, nextPhase);
        }
    }
}
