using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Networking;

namespace Game.Gameplay.Match
{
    /// <summary>Server-authoritative match orchestration: phases, arena layout, starting gold.</summary>
    public sealed class MatchController
    {
        private readonly List<MatchPlayerState> _players = new();
        private readonly List<HeroRosterState> _heroRosters = new();
        private readonly BarracksWaveScheduler _waveScheduler = new();
        private readonly MatchCombatSystem _combat = new();
        private readonly BuildingRegistry _buildings = new();
        private readonly EliminationService _elimination = new();
        private readonly MatchResearchQueue _research = new();
        private readonly TowerDefenseSystem _towers = new();
        private int? _winnerSlot;
        private bool _clientEndedRaised;

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
        public MatchResearchQueue Research => _research;
        public TowerDefenseSystem Towers => _towers;
        public ICombatUnitCatalog CombatCatalog { get; set; }
        public int? WinnerSlot => _winnerSlot;
        public bool IsRunning => Phase is not MatchPhase.Lobby and not MatchPhase.End;

        public HeroRosterState GetHeroRoster(int ownerSlot) =>
            ownerSlot >= 0 && ownerSlot < _heroRosters.Count ? _heroRosters[ownerSlot] : null;

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
            _heroRosters.Clear();
            for (var slot = 0; slot < config.PlayerCount; slot++)
            {
                var raceId = config.GetRaceId(slot);
                var startingGold = MatchRules.GetStartingGold(raceId);
                _players.Add(new MatchPlayerState(slot, raceId, startingGold));
                _heroRosters.Add(new HeroRosterState());
            }

            _waveScheduler.WaveFired -= OnWaveFired;
            _combat.UnitKilled -= OnUnitKilled;
            _buildings.BuildingDestroyed -= OnBuildingDestroyed;
            _elimination.PlayerEliminated -= OnPlayerEliminated;
            _waveScheduler.Initialize(_players);
            _waveScheduler.WaveFired += OnWaveFired;
            _buildings.Initialize(Layout);
            _buildings.BuildingDestroyed += OnBuildingDestroyed;
            _elimination.PlayerEliminated += OnPlayerEliminated;
            _combat.Reset(_players, Graph);
            _combat.SetBuildings(_buildings);
            _combat.UnitKilled += OnUnitKilled;
            _waveScheduler.Deactivate();
            _research.Clear();
            _towers.Reset();

            MatchTimeSeconds = 0f;
            _winnerSlot = null;
            _clientEndedRaised = false;
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
            TickResearch(deltaTime);
            TickPassiveGold(deltaTime);
            TickHeroRosters(deltaTime);
            TickBarracksCallCharges(deltaTime);
            _waveScheduler.Tick(deltaTime);
            // Towers first so building shots advance in the same combat tick.
            _towers.Tick(deltaTime, _buildings, _combat, _players);
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

        public bool TryGetResearch(int buildingInstanceId, out BuildingResearchState research) =>
            _research.TryGet(buildingInstanceId, out research);

        /// <summary>Editor/debug: complete all queued research for an owner immediately.</summary>
        public void DebugCompleteResearchForOwner(int ownerSlot)
        {
            if (ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return;
            }

            for (var safety = 0; safety < 64; safety++)
            {
                var found = false;
                foreach (var buildingInstanceId in _research.BuildingInstanceIds)
                {
                    if (_research.TryGetActive(buildingInstanceId, out var head)
                        && head.OwnerSlot == ownerSlot)
                    {
                        head.RemainingSeconds = 0f;
                        found = true;
                    }
                }

                if (!found)
                {
                    break;
                }

                // MatchResearchQueue.Tick ignores non-positive delta; use a tiny step to flush.
                TickResearch(0.001f);
            }
        }

        /// <summary>Editor/debug: bump Passive Gold level within Main-level cap.</summary>
        public bool DebugBumpPassiveGold(int ownerSlot)
        {
            if (ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return false;
            }

            var player = _players[ownerSlot];
            if (player.IsEliminated)
            {
                return false;
            }

            var cap = MatchEconomyRules.GetPassiveGoldCap(player.MainLevel);
            if (player.PassiveGoldLevel >= cap)
            {
                return false;
            }

            player.PassiveGoldLevel = Math.Min(MatchEconomyRules.MaxPassiveGoldLevel, player.PassiveGoldLevel + 1);
            return true;
        }

        public bool TryStartResearch(int ownerSlot, int buildingInstanceId, string upgradeId)
        {
            if (!IsRunning || Phase == MatchPhase.Start)
            {
                return false;
            }

            if (ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return false;
            }

            var player = _players[ownerSlot];
            if (player.IsEliminated)
            {
                return false;
            }

            var building = _buildings.GetByInstanceId(buildingInstanceId);
            if (building == null || !building.IsIntact || building.OwnerSlot != ownerSlot)
            {
                return false;
            }

            if (!_research.HasSpace(buildingInstanceId))
            {
                return false;
            }

            if (upgradeId == GameIds.Upgrades.BarracksLevel)
            {
                return TryStartBarracksLevelResearch(player, building);
            }

            if (upgradeId == GameIds.Upgrades.MainPassiveGold)
            {
                return TryStartPassiveGoldResearch(player, building);
            }

            if (HeroRules.TryParseHireUpgradeId(upgradeId, out var heroSlot))
            {
                return TryStartHeroHireResearch(player, building, heroSlot);
            }

            return false;
        }

        public bool TryGetResearchQueue(
            int buildingInstanceId,
            out IReadOnlyList<BuildingResearchState> queue) =>
            _research.TryGetQueue(buildingInstanceId, out queue);

        public void ApplyAuthoritativeSnapshot(MatchSnapshot snapshot)
        {
            if (snapshot?.Players == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.Players.Length; i++)
            {
                var p = snapshot.Players[i];
                if (p.Slot < 0 || p.Slot >= _players.Count)
                {
                    continue;
                }

                _players[p.Slot].Gold = p.Gold;
                _players[p.Slot].IsEliminated = p.IsEliminated;
                _players[p.Slot].PassiveGoldLevel = Math.Max(0, p.PassiveGoldLevel);
                if (p.MainLevel > 0)
                {
                    _players[p.Slot].MainLevel = p.MainLevel;
                }
            }

            ApplyAuthoritativeBuildings(snapshot.Buildings);
            ApplyAuthoritativeBarracks(snapshot.Barracks);
            ApplyAuthoritativeResearch(snapshot.Research);
            ApplyAuthoritativeCenterLanes(snapshot.CenterLanes);
            _combat.ApplyAuthoritativeUnits(snapshot.Units, CombatCatalog);

            MatchTimeSeconds = snapshot.MatchTimeSeconds;

            if (Enum.IsDefined(typeof(MatchPhase), snapshot.Phase)
                && snapshot.Phase != (int)MatchPhase.End
                && Phase != MatchPhase.End)
            {
                // Clients do not run sim; only mirror phase label/time without activating systems.
                Phase = (MatchPhase)snapshot.Phase;
            }

            if (snapshot.Phase == (int)MatchPhase.End && snapshot.WinnerSlot >= 0)
            {
                if (Phase != MatchPhase.End)
                {
                    _winnerSlot = snapshot.WinnerSlot;
                    _waveScheduler.Deactivate();
                    Phase = MatchPhase.End;
                }

                if (!_clientEndedRaised)
                {
                    _clientEndedRaised = true;
                    MatchEnded?.Invoke(snapshot.WinnerSlot);
                }
            }
        }

        void ApplyAuthoritativeBuildings(MatchBuildingSnapshot[] buildings)
        {
            if (buildings == null)
            {
                return;
            }

            for (var i = 0; i < buildings.Length; i++)
            {
                var snap = buildings[i];
                var building = snap.InstanceId > 0
                    ? _buildings.GetByInstanceId(snap.InstanceId)
                    : FindBuilding(snap.OwnerSlot, snap.BuildingId);
                if (building == null)
                {
                    continue;
                }

                var hp = snap.IsRuins ? 0f : snap.Health;
                building.SetAuthoritativeHp(hp);
            }
        }

        BuildingState FindBuilding(int ownerSlot, string buildingId)
        {
            foreach (var building in _buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.BuildingId == buildingId)
                {
                    return building;
                }
            }

            return null;
        }

        void ApplyAuthoritativeBarracks(MatchBarracksSnapshot[] barracks)
        {
            if (barracks == null)
            {
                return;
            }

            for (var i = 0; i < barracks.Length; i++)
            {
                var snap = barracks[i];
                var state = _waveScheduler.GetBarracks(snap.OwnerSlot, snap.BarracksId);
                if (state == null)
                {
                    continue;
                }

                state.Level = Math.Max(1, snap.Level);
                state.FrozenSquadLevel = Math.Max(1, snap.FrozenSquadLevel);
                state.IsRuins = snap.IsRuins;
                state.RefreshInterval();
                if (snap.CallCurrent != null
                    && snap.CallMax != null
                    && snap.CallNextRegen != null
                    && snap.CallCurrent.Length >= BarracksCallChargeState.CallableRoleCount
                    && HasAnyCallMax(snap.CallMax))
                {
                    state.CallCharges.ApplySnapshot(snap.CallCurrent, snap.CallMax, snap.CallNextRegen);
                }
            }
        }

        void ApplyAuthoritativeResearch(MatchResearchSnapshot[] research)
        {
            if (research == null || research.Length == 0)
            {
                _research.Clear();
                return;
            }

            var items = new List<BuildingResearchState>(research.Length);
            for (var i = 0; i < research.Length; i++)
            {
                var snap = research[i];
                var item = new BuildingResearchState(
                    snap.BuildingInstanceId,
                    snap.OwnerSlot,
                    snap.BuildingId,
                    snap.UpgradeId,
                    snap.CostPaid,
                    snap.DurationSeconds);
                item.RemainingSeconds = Math.Max(0f, snap.RemainingSeconds);
                items.Add(item);
            }

            _research.ReplaceAll(items);
        }

        void ApplyAuthoritativeCenterLanes(MatchCenterLaneSnapshot[] centerLanes)
        {
            if (centerLanes == null || Graph == null || Layout == null)
            {
                return;
            }

            for (var i = 0; i < centerLanes.Length; i++)
            {
                var snap = centerLanes[i];
                if (!Graph.TryGetLane(snap.OwnerSlot, GameIds.Lanes.Center, out var lane))
                {
                    continue;
                }

                if (lane.OpponentSlot == snap.OpponentSlot
                    || snap.OpponentSlot < 0
                    || snap.OpponentSlot >= Layout.Slots.Count
                    || snap.OwnerSlot < 0
                    || snap.OwnerSlot >= Layout.Slots.Count)
                {
                    continue;
                }

                lane.OpponentSlot = snap.OpponentSlot;
                lane.Path = LaneGraphBuilder.BuildCenterPath(
                    Layout.Slots[snap.OwnerSlot],
                    Layout.Slots[snap.OpponentSlot],
                    Graph.CenterArenaRadius);
                _combat.ReplaceLaneRoute(snap.OwnerSlot, GameIds.Lanes.Center, lane.Path);
            }
        }

        public bool TryEliminateForDisconnect(int ownerSlot)
        {
            if (!IsRunning || ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return false;
            }

            if (_players[ownerSlot].IsEliminated)
            {
                return false;
            }

            _elimination.EliminateForDisconnect(ownerSlot, _players, _waveScheduler, _combat);
            // Center retarget runs via PlayerEliminated before winner check.
            var winner = _elimination.TryGetLastStandingWinner(_players);
            if (winner.HasValue)
            {
                EndMatch(winner.Value);
            }

            return true;
        }

        /// <summary>Starts hero-hire research on the owner's Main (25s), same queue as Passive Gold.</summary>
        public bool TryHireHero(int ownerSlot, int heroSlot)
        {
            if (ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return false;
            }

            BuildingState main = null;
            foreach (var building in _buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot
                    && building.BuildingId == GameIds.Buildings.Main
                    && building.IsIntact)
                {
                    main = building;
                    break;
                }
            }

            if (main == null)
            {
                return false;
            }

            return TryStartResearch(ownerSlot, main.InstanceId, HeroRules.BuildHireUpgradeId(heroSlot));
        }

        public bool TryDeployHero(int ownerSlot, int buildingInstanceId, int heroSlot)
        {
            if (!IsRunning || Phase == MatchPhase.Start)
            {
                return false;
            }

            if (ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return false;
            }

            var player = _players[ownerSlot];
            if (player.IsEliminated)
            {
                return false;
            }

            var building = _buildings.GetByInstanceId(buildingInstanceId);
            if (building == null
                || !building.IsIntact
                || building.OwnerSlot != ownerSlot
                || !BuildingRules.IsBarracks(building.BuildingId))
            {
                return false;
            }

            var roster = _heroRosters[ownerSlot];
            var slotState = roster.Get(heroSlot);
            if (!HeroRules.CanDeploy(
                    slotState.State,
                    slotState.DeathCooldownRemaining,
                    player.Gold,
                    barracksIntact: true))
            {
                return false;
            }

            DespawnParkedHero(ownerSlot, heroSlot);

            var stats = ResolveHeroStats(player.RaceId, heroSlot);
            var laneId = BuildingRules.GetLaneBinding(building.BuildingId);
            var unit = _combat.SpawnUnit(
                ownerSlot,
                laneId,
                UnitRole.Hero,
                stats,
                isHero: true,
                heroSlot: heroSlot);

            player.Gold -= HeroRules.DeployGold;
            slotState.State = HeroLifecycleState.Deployed;
            slotState.DeployedUnitId = unit.UnitId;
            return true;
        }

        public bool TrySetTowerTarget(int ownerSlot, int towerInstanceId, int unitId)
        {
            if (!IsRunning || ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return false;
            }

            var building = _buildings.GetByInstanceId(towerInstanceId);
            if (building == null
                || !building.IsIntact
                || building.OwnerSlot != ownerSlot
                || !BuildingRules.IsDefensiveBuilding(building.BuildingId))
            {
                return false;
            }

            var unit = _combat.GetUnit(unitId);
            if (!TowerCombatRules.CanTargetUnit(ownerSlot, unit)
                || !TowerCombatRules.IsInRange(building.WorldPosition, unit.WorldPosition))
            {
                return false;
            }

            return _towers.TrySetManualTarget(towerInstanceId, unitId);
        }

        public bool TryManualCallUnit(int ownerSlot, int barracksBuildingInstanceId, UnitRole role)
        {
            if (!IsRunning || Phase == MatchPhase.Start)
            {
                return false;
            }

            if (ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return false;
            }

            var player = _players[ownerSlot];
            if (player.IsEliminated)
            {
                return false;
            }

            var building = _buildings.GetByInstanceId(barracksBuildingInstanceId);
            if (building == null
                || !building.IsIntact
                || building.OwnerSlot != ownerSlot
                || !BuildingRules.IsBarracks(building.BuildingId))
            {
                return false;
            }

            var barracks = _waveScheduler.GetBarracks(ownerSlot, building.BuildingId);
            if (barracks == null || barracks.IsRuins)
            {
                return false;
            }

            EnsureBarracksCallCharges(barracks);
            var cost = BarracksManualCallRules.GetGoldCost(role);
            var charges = barracks.CallCharges.GetCharges(role);
            if (!BarracksManualCallRules.CanCall(
                    player.Gold >= cost,
                    charges,
                    barracksIntact: true,
                    notEliminated: true))
            {
                return false;
            }

            if (!barracks.CallCharges.TrySpend(role))
            {
                return false;
            }

            if (!MatchEconomyRules.TrySpendGold(player.Gold, cost, out var remaining))
            {
                return false;
            }

            player.Gold = remaining;
            var stats = ResolveUnitStats(player.RaceId, role);
            // Same forward clearance band as auto-wave creeps (not barracks center / inside mesh).
            var spawnDistance = CombatFormationRules.BarracksSpawnForwardClearance;
            _combat.SpawnUnit(
                ownerSlot,
                barracks.LaneId,
                role,
                stats,
                distanceAlongLane: spawnDistance);
            return true;
        }

        bool TryStartBarracksLevelResearch(MatchPlayerState player, BuildingState building)
        {
            if (!IsBarracksBuildingId(building.BuildingId))
            {
                return false;
            }

            var barracks = _waveScheduler.GetBarracks(player.SlotIndex, building.BuildingId);
            if (barracks == null || barracks.IsRuins)
            {
                return false;
            }

            var queued = _research.CountUpgrade(building.InstanceId, GameIds.Upgrades.BarracksLevel);
            var projectedLevel = barracks.Level + queued;
            if (!MatchEconomyRules.TryGetBarracksLevelUpgrade(projectedLevel, out var cost, out var duration))
            {
                return false;
            }

            if (!MatchEconomyRules.TrySpendGold(player.Gold, cost, out var remaining))
            {
                return false;
            }

            var research = new BuildingResearchState(
                building.InstanceId,
                player.SlotIndex,
                building.BuildingId,
                GameIds.Upgrades.BarracksLevel,
                cost,
                duration);

            if (!_research.TryEnqueue(research))
            {
                return false;
            }

            player.Gold = remaining;
            return true;
        }

        bool TryStartPassiveGoldResearch(MatchPlayerState player, BuildingState building)
        {
            if (building.BuildingId != GameIds.Buildings.Main)
            {
                return false;
            }

            var queued = _research.CountUpgrade(building.InstanceId, GameIds.Upgrades.MainPassiveGold);
            var projectedLevel = player.PassiveGoldLevel + queued;
            if (!MatchEconomyRules.CanPurchasePassiveGold(projectedLevel, player.MainLevel))
            {
                return false;
            }

            if (!MatchEconomyRules.TrySpendGold(
                    player.Gold,
                    MatchEconomyRules.PassiveGoldUpgradeCost,
                    out var remaining))
            {
                return false;
            }

            var research = new BuildingResearchState(
                building.InstanceId,
                player.SlotIndex,
                building.BuildingId,
                GameIds.Upgrades.MainPassiveGold,
                MatchEconomyRules.PassiveGoldUpgradeCost,
                MatchEconomyRules.PassiveGoldUpgradeSeconds);

            if (!_research.TryEnqueue(research))
            {
                return false;
            }

            player.Gold = remaining;
            return true;
        }

        void TickResearch(float deltaTime)
        {
            var completed = _research.Tick(deltaTime);
            for (var i = 0; i < completed.Count; i++)
            {
                ApplyCompletedResearch(completed[i]);
            }
        }

        void ApplyCompletedResearch(BuildingResearchState research)
        {
            if (research.UpgradeId == GameIds.Upgrades.BarracksLevel)
            {
                var barracks = _waveScheduler.GetBarracks(research.OwnerSlot, research.BuildingId);
                if (barracks == null || barracks.IsRuins)
                {
                    return;
                }

                _waveScheduler.SetBarracksLevel(
                    research.OwnerSlot,
                    research.BuildingId,
                    barracks.Level + 1);
                EnsureBarracksCallCharges(barracks);
                barracks.CallCharges.OnLevelUp(ResolveSquadCounts(barracks.EffectiveSquadLevel));
                return;
            }

            if (research.UpgradeId == GameIds.Upgrades.MainPassiveGold)
            {
                if (research.OwnerSlot < 0 || research.OwnerSlot >= _players.Count)
                {
                    return;
                }

                var player = _players[research.OwnerSlot];
                var cap = MatchEconomyRules.GetPassiveGoldCap(player.MainLevel);
                if (player.PassiveGoldLevel >= cap)
                {
                    return;
                }

                player.PassiveGoldLevel = Math.Min(
                    MatchEconomyRules.MaxPassiveGoldLevel,
                    player.PassiveGoldLevel + 1);
                return;
            }

            if (HeroRules.TryParseHireUpgradeId(research.UpgradeId, out var heroSlot)
                && research.OwnerSlot >= 0
                && research.OwnerSlot < _heroRosters.Count)
            {
                var slotState = _heroRosters[research.OwnerSlot].Get(heroSlot);
                if (slotState.State == HeroLifecycleState.None)
                {
                    slotState.State = HeroLifecycleState.IdleAtBase;
                    SpawnParkedHero(research.OwnerSlot, heroSlot);
                }
            }
        }

        bool TryStartHeroHireResearch(MatchPlayerState player, BuildingState building, int heroSlot)
        {
            if (building.BuildingId != GameIds.Buildings.Main)
            {
                return false;
            }

            var hireId = HeroRules.BuildHireUpgradeId(heroSlot);
            if (_research.CountUpgrade(building.InstanceId, hireId) > 0)
            {
                return false;
            }

            var roster = _heroRosters[player.SlotIndex];
            var slotState = roster.Get(heroSlot);
            if (!HeroRules.CanHire(slotState.State, heroSlot, player.MainLevel, player.Gold))
            {
                return false;
            }

            if (!MatchEconomyRules.TrySpendGold(player.Gold, HeroRules.HireGold, out var remaining))
            {
                return false;
            }

            var research = new BuildingResearchState(
                building.InstanceId,
                player.SlotIndex,
                building.BuildingId,
                hireId,
                HeroRules.HireGold,
                HeroRules.HireResearchSeconds);

            if (!_research.TryEnqueue(research))
            {
                return false;
            }

            player.Gold = remaining;
            return true;
        }

        void TickPassiveGold(float deltaTime)
        {
            for (var i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                if (player.IsEliminated || player.PassiveGoldLevel <= 0)
                {
                    continue;
                }

                player.PassiveGoldTickRemainingSeconds -= deltaTime;
                while (player.PassiveGoldTickRemainingSeconds <= 0f)
                {
                    player.Gold += MatchEconomyRules.GetPassiveGoldPerTick(player.PassiveGoldLevel);
                    player.PassiveGoldTickRemainingSeconds +=
                        MatchEconomyRules.PassiveGoldTickIntervalSeconds;
                }
            }
        }

        static bool IsBarracksBuildingId(string buildingId) =>
            buildingId is GameIds.Buildings.BarracksLeft
                or GameIds.Buildings.BarracksCenter
                or GameIds.Buildings.BarracksRight;

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
            HandleHeroDeath(killEvent);
            UnitKilled?.Invoke(killEvent);
        }

        void HandleHeroDeath(UnitKillEvent killEvent)
        {
            for (var slot = 0; slot < _heroRosters.Count; slot++)
            {
                var roster = _heroRosters[slot];
                for (var heroSlot = 1; heroSlot <= HeroRules.MaxHeroSlots; heroSlot++)
                {
                    var state = roster.Get(heroSlot);
                    if (state.DeployedUnitId != killEvent.VictimUnitId)
                    {
                        continue;
                    }

                    state.State = HeroLifecycleState.Dead;
                    state.DeployedUnitId = null;
                    state.DeathCooldownRemaining = HeroRules.DeathCooldownSeconds;
                    return;
                }
            }
        }

        void TickHeroRosters(float deltaTime)
        {
            for (var i = 0; i < _heroRosters.Count; i++)
            {
                _heroRosters[i].Tick(deltaTime);
            }
        }

        void TickBarracksCallCharges(float deltaTime)
        {
            for (var i = 0; i < _waveScheduler.Barracks.Count; i++)
            {
                var barracks = _waveScheduler.Barracks[i];
                EnsureBarracksCallCharges(barracks);
                barracks.CallCharges.Tick(deltaTime);
            }
        }

        void EnsureBarracksCallCharges(BarracksWaveState barracks)
        {
            if (barracks == null)
            {
                return;
            }

            if (barracks.CallCharges.IsInitialized)
            {
                return;
            }

            barracks.CallCharges.Initialize(ResolveSquadCounts(barracks.EffectiveSquadLevel));
        }

        static bool HasAnyCallMax(int[] callMax)
        {
            if (callMax == null)
            {
                return false;
            }

            for (var i = 0; i < callMax.Length; i++)
            {
                if (callMax[i] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        ISquadCounts ResolveSquadCounts(int barracksLevel)
        {
            var squad = CombatCatalog?.GetSquad(barracksLevel);
            if (squad != null)
            {
                return squad;
            }

            return BarracksManualCallRules.GetDefaultSquadCounts(barracksLevel);
        }

        UnitCombatStats ResolveUnitStats(string raceId, UnitRole role)
        {
            var race = CombatCatalog?.GetRace(raceId);
            var def = race?.GetUnit(role);
            if (def != null)
            {
                return new UnitCombatStats(
                    role,
                    def.MaxHp,
                    def.Armor,
                    def.DamageMin,
                    def.DamageMax,
                    def.AttackSpeed,
                    def.AttackRange,
                    def.MoveSpeed,
                    def.GoldBounty);
            }

            return new UnitCombatStats(role, 200f, 1f, 10f, 14f, 1f, 1.5f, 3.5f, 20);
        }

        void SpawnParkedHero(int ownerSlot, int heroSlot)
        {
            if (Layout == null || ownerSlot < 0 || ownerSlot >= _players.Count)
            {
                return;
            }

            var player = _players[ownerSlot];
            var stats = ResolveHeroStats(player.RaceId, heroSlot);
            var park = HeroParkRules.GetParkWorldPosition(
                Layout,
                ownerSlot,
                heroSlot,
                Layout.MainToTowerDistance);
            var unit = _combat.SpawnUnit(
                ownerSlot,
                GameIds.Lanes.Center,
                UnitRole.Hero,
                stats,
                isHero: true,
                heroSlot: heroSlot);
            unit.WorldPosition = park;
            unit.IsParkedAtBase = true;
            unit.BehaviorState = UnitBehaviorState.Move;
            var roster = _heroRosters[ownerSlot].Get(heroSlot);
            roster.DeployedUnitId = unit.UnitId;
        }

        void DespawnParkedHero(int ownerSlot, int heroSlot)
        {
            if (ownerSlot < 0 || ownerSlot >= _heroRosters.Count)
            {
                return;
            }

            var slotState = _heroRosters[ownerSlot].Get(heroSlot);
            if (!slotState.DeployedUnitId.HasValue)
            {
                return;
            }

            var unit = _combat.GetUnit(slotState.DeployedUnitId.Value);
            if (unit != null && unit.IsParkedAtBase)
            {
                _combat.DespawnUnit(unit.UnitId);
            }

            slotState.DeployedUnitId = null;
        }

        UnitCombatStats ResolveHeroStats(string raceId, int heroSlot)
        {
            var race = CombatCatalog?.GetRace(raceId);
            var hero = race?.GetHeroBySlot(heroSlot);
            if (hero != null)
            {
                return new UnitCombatStats(
                    UnitRole.Hero,
                    hero.MaxHp,
                    hero.Armor,
                    hero.DamageMin,
                    hero.DamageMax,
                    hero.AttackSpeed,
                    hero.AttackRange,
                    hero.MoveSpeed,
                    hero.GoldBounty);
            }

            return new UnitCombatStats(
                UnitRole.Hero,
                600f,
                4f,
                35f,
                45f,
                1f,
                1.5f,
                4f,
                80);
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

        private void OnPlayerEliminated(int eliminatedSlot)
        {
            if (_elimination.TryGetLastStandingWinner(_players).HasValue)
            {
                return;
            }

            CenterLaneRetarget.Apply(eliminatedSlot, _players, Layout, Graph, _combat);
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
