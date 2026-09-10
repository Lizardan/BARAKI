using System.Collections.Generic;
using Game.Gameplay.Cameras;
using Game.Gameplay.Combat;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Ведущий арены. Работает и на хосте (авторитетный автомат + симуляция дуэлей),
    /// и на клиентах (камера, визуал, UI читают реплицированное состояние).
    /// Тикает нескалированным временем: во время арены матч стоит на паузе.
    /// </summary>
    public sealed class ArenaDirector : MonoBehaviour
    {
        public static ArenaDirector Current { get; private set; }

        ArenaNetState _state = ArenaNetState.Empty;
        readonly ArenaDuelSim _sim = new();
        readonly List<int> _participants = new();
        readonly List<ArenaDuel> _duels = new();
        ArenaDuel _currentDuel;

        ArenaDuelPresenter _presenter;

        int _arenaIndex = 1;
        float _nextArenaMatchTime = ArenaRules.IntervalSeconds;
        uint _usedHeroMask;
        int _duelCursor;

        bool _cameraTaken;
        Vector3 _savedCameraPosition;
        float _savedYawDegrees;

        bool IsHost => ArenaNetworkBridge.IsAuthority;

        void Awake()
        {
            Current = this;
            ArenaStage.EnsureBuilt();

            _presenter = GetComponent<ArenaDuelPresenter>();
            if (_presenter == null)
            {
                _presenter = gameObject.AddComponent<ArenaDuelPresenter>();
            }
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            if (MatchPauseGate.IsArenaPaused)
            {
                MatchPauseGate.SetArenaPaused(false);
            }

            ReleaseCamera();
        }

        void Update()
        {
            var runtime = MatchRuntime.Current;
            if (runtime == null || !runtime.IsMatchStarted)
            {
                return;
            }

            var controller = runtime.Controller;
            if (controller == null || controller.Phase == MatchPhase.Lobby)
            {
                return;
            }

            if (controller.Phase == MatchPhase.End)
            {
                AbortArena();
                return;
            }

            var unscaledDelta = Time.unscaledDeltaTime;

            if (IsHost)
            {
                TickAuthority(controller, unscaledDelta);
            }

            TickLocal(unscaledDelta);
        }

        #region Авторитетный автомат (хост)

        void TickAuthority(MatchController controller, float unscaledDelta)
        {
            switch ((ArenaPhase)_state.Phase)
            {
                case ArenaPhase.Idle:
                    TickIdle(controller);
                    break;
                case ArenaPhase.Pick:
                    TickPick(controller, unscaledDelta);
                    break;
                case ArenaPhase.Intro:
                    TickIntro(controller, unscaledDelta);
                    break;
                case ArenaPhase.Duel:
                    TickDuel(controller, unscaledDelta);
                    break;
                case ArenaPhase.Result:
                    TickResult(controller, unscaledDelta);
                    break;
            }

            ArenaNetworkBridge.Publish(_state);
        }

        void TickIdle(MatchController controller)
        {
            // Индекс 0 означает «арен больше нет» — HUD прячет отсчёт.
            _state.Index = (byte)(_arenaIndex > ArenaRules.TotalArenas ? 0 : _arenaIndex);

            if (_arenaIndex > ArenaRules.TotalArenas)
            {
                _state.Timer = 0f;
                return;
            }

            _state.Timer = Mathf.Max(0f, _nextArenaMatchTime - controller.MatchTimeSeconds);
            if (_state.Timer > 0f)
            {
                return;
            }

            StartArena(controller);
        }

        void StartArena(MatchController controller)
        {
            _participants.Clear();
            _participants.AddRange(
                ArenaPairing.CollectParticipants(controller, _arenaIndex, _usedHeroMask));

            if (_participants.Count < 2)
            {
                BurnArena(controller);
                return;
            }

            _duels.Clear();
            _duelCursor = 0;

            _state = ArenaNetState.Empty;
            _state.Phase = (byte)ArenaPhase.Pick;
            _state.Index = (byte)_arenaIndex;
            _state.RewardGold = ArenaRules.RewardGold(_arenaIndex);
            _state.Timer = ArenaRules.PickSeconds;
            _state.UsedHeroMask = _usedHeroMask;
            _state.Winner = -1;
            _state.ChallengerSlot = ArenaPairing.FindChallenger(_participants);
            _state.DuelCount = _participants.Count / 2 + (_state.ChallengerSlot >= 0 ? 1 : 0);

            foreach (var slot in _participants)
            {
                _state.Participants = ArenaRules.SetMask(_state.Participants, slot, true);
            }

            MatchPauseGate.SetArenaPaused(true);
        }

        /// <summary>Арена «сгорает»: участников меньше двух, отсчёт уходит к следующей.</summary>
        void BurnArena(MatchController controller)
        {
            _arenaIndex++;
            _nextArenaMatchTime = controller.MatchTimeSeconds + ArenaRules.IntervalSeconds;
            _state = ArenaNetState.Empty;
            _state.UsedHeroMask = _usedHeroMask;
            _state.Index = (byte)(_arenaIndex > ArenaRules.TotalArenas ? 0 : _arenaIndex);
            _state.Timer = _arenaIndex > ArenaRules.TotalArenas
                ? 0f
                : Mathf.Max(0f, _nextArenaMatchTime - controller.MatchTimeSeconds);
        }

        void TickPick(MatchController controller, float unscaledDelta)
        {
            _state.Timer -= unscaledDelta;

            if (_state.Timer > 0f && !AllSubmitted())
            {
                return;
            }

            ResolveMissingPicks(controller);
            BuildDuels();
            _duelCursor = 0;
            StartNextDuel(controller);
        }

        bool AllSubmitted()
        {
            // Нечётный игрок обязан выбрать ещё и соперника, иначе фаза не закрывается.
            if (_state.ChallengerSlot >= 0 && _state.ChallengerTarget < 0)
            {
                return false;
            }

            for (var slot = 0; slot < ArenaRules.MaxSlots; slot++)
            {
                if (ArenaRules.HasMask(_state.Participants, slot)
                    && !ArenaRules.HasMask(_state.Submitted, slot))
                {
                    return false;
                }
            }

            return true;
        }

        void ResolveMissingPicks(MatchController controller)
        {
            foreach (var slot in _participants)
            {
                if (ArenaRules.HasMask(_state.Submitted, slot))
                {
                    continue;
                }

                var auto = ArenaPairing.FirstAvailablePick(
                    controller, slot, _state.Index, _usedHeroMask);
                _state.Picks = ArenaRules.WritePick(_state.Picks, slot, auto);
                _state.Submitted = ArenaRules.SetMask(_state.Submitted, slot, true);
            }

            if (_state.ChallengerSlot >= 0 && _state.ChallengerTarget < 0)
            {
                _state.ChallengerTarget = FirstOpponentFor(_state.ChallengerSlot);
            }
        }

        int FirstOpponentFor(int slot)
        {
            foreach (var other in _participants)
            {
                if (other != slot)
                {
                    return other;
                }
            }

            return -1;
        }

        int PickOf(int slot)
        {
            var raw = ArenaRules.ReadPick(_state.Picks, slot);
            return raw == ArenaRules.PickValueTitan ? 0 : raw;
        }

        void BuildDuels()
        {
            _duels.Clear();
            ArenaPairing.BuildDuels(_participants, PickOf, _duels);

            if (_state.ChallengerSlot >= 0)
            {
                var target = _state.ChallengerTarget;
                if (target < 0 || target == _state.ChallengerSlot
                               || !ArenaRules.HasMask(_state.Participants, target))
                {
                    target = FirstOpponentFor(_state.ChallengerSlot);
                    _state.ChallengerTarget = target;
                }

                if (target >= 0)
                {
                    _duels.Add(ArenaPairing.BuildChallengerDuel(
                        _state.ChallengerSlot,
                        PickOf(_state.ChallengerSlot),
                        target,
                        PickOf(target)));
                }
            }

            _state.DuelCount = _duels.Count;
        }

        void StartNextDuel(MatchController controller)
        {
            if (_duelCursor >= _duels.Count)
            {
                FinishArena(controller);
                return;
            }

            _currentDuel = _duels[_duelCursor];
            _state.PairASlot = _currentDuel.SlotA;
            _state.PairAHero = _currentDuel.HeroA;
            _state.PairBSlot = _currentDuel.SlotB;
            _state.PairBHero = _currentDuel.HeroB;
            _state.Winner = -1;
            _state.DuelOrdinal = _duelCursor + 1;
            _state.A = ArenaFighterNet.Empty;
            _state.B = ArenaFighterNet.Empty;
            _state.Phase = (byte)ArenaPhase.Intro;
            _state.Timer = ArenaRules.IntroSeconds;

            // Герой считается использованным с момента выхода на арену, независимо от исхода.
            MarkUsedHero(_currentDuel.SlotA, _currentDuel.HeroA);
            MarkUsedHero(_currentDuel.SlotB, _currentDuel.HeroB);
        }

        void MarkUsedHero(int slot, int heroSlot)
        {
            if (slot < 0 || heroSlot <= 0)
            {
                return;
            }

            _usedHeroMask = ArenaRules.MarkUsedHero(_usedHeroMask, slot, heroSlot);
            _state.UsedHeroMask = _usedHeroMask;
        }

        void TickIntro(MatchController controller, float unscaledDelta)
        {
            _state.Timer -= unscaledDelta;
            if (_state.Timer > 0f)
            {
                return;
            }

            _sim.Begin(controller, _currentDuel, _state.Index * 1000 + _duelCursor);
            if (!_sim.IsRunning)
            {
                // Бой не удалось собрать — пропускаем дуэль без награды.
                _duelCursor++;
                StartNextDuel(controller);
                return;
            }

            _state.Phase = (byte)ArenaPhase.Duel;
            _state.Timer = 0f;
            PublishFighters();
        }

        void TickDuel(MatchController controller, float unscaledDelta)
        {
            _sim.Tick(unscaledDelta);
            PublishFighters();
            _state.Timer = _sim.ElapsedSeconds;

            if (_sim.WinnerIndex < 0)
            {
                return;
            }

            var winnerSlot = _sim.WinnerIndex == 0 ? _currentDuel.SlotA : _currentDuel.SlotB;
            controller.GrantArenaGold(winnerSlot, _state.RewardGold);

            _state.Winner = _sim.WinnerIndex;
            _state.Fought = ArenaRules.SetMask(_state.Fought, _currentDuel.SlotA, true);
            _state.Fought = ArenaRules.SetMask(_state.Fought, _currentDuel.SlotB, true);
            _state.Phase = (byte)ArenaPhase.Result;
            _state.Timer = ArenaRules.ResultSeconds;

            // Золото лежит в игроке матча — нужен внеочередной снапшот, пока матч на паузе.
            MatchNetworkAuthority.Instance?.PublishSnapshotNow();
        }

        void TickResult(MatchController controller, float unscaledDelta)
        {
            _state.Timer -= unscaledDelta;
            if (_state.Timer > 0f)
            {
                return;
            }

            _duelCursor++;
            StartNextDuel(controller);
        }

        void FinishArena(MatchController controller)
        {
            _sim.Reset();
            _duels.Clear();
            _duelCursor = 0;
            _arenaIndex++;
            _nextArenaMatchTime = controller.MatchTimeSeconds + ArenaRules.IntervalSeconds;

            MatchPauseGate.SetArenaPaused(false);

            _state = ArenaNetState.Empty;
            _state.UsedHeroMask = _usedHeroMask;
            _state.Index = (byte)(_arenaIndex > ArenaRules.TotalArenas ? 0 : _arenaIndex);
            _state.Timer = _arenaIndex > ArenaRules.TotalArenas
                ? 0f
                : Mathf.Max(0f, _nextArenaMatchTime - controller.MatchTimeSeconds);
        }

        void AbortArena()
        {
            if ((ArenaPhase)_state.Phase == ArenaPhase.Idle)
            {
                return;
            }

            _sim.Reset();
            _duels.Clear();
            _duelCursor = 0;
            _state = ArenaNetState.Empty;
            _state.UsedHeroMask = _usedHeroMask;
            MatchPauseGate.SetArenaPaused(false);
            ArenaNetworkBridge.Publish(_state);
        }

        /// <summary>
        /// Полный сброс перед новым матчем (лобби/реванш). Индексы, маска использованных
        /// героев и позиция камеры возвращаются в исходное состояние.
        /// </summary>
        public void ResetForNewMatch()
        {
            _sim.Reset();
            _duels.Clear();
            _participants.Clear();
            _duelCursor = 0;
            _arenaIndex = 1;
            _nextArenaMatchTime = ArenaRules.IntervalSeconds;
            _usedHeroMask = 0u;
            _state = ArenaNetState.Empty;

            if (_cameraTaken)
            {
                ReleaseCamera();
            }
        }

        /// <summary>Выбор игрока. Вызывается на хосте (напрямую или из ServerRpc).</summary>
        public void SubmitPick(int slot, int heroSlot, int challengerTarget)
        {
            if ((ArenaPhase)_state.Phase != ArenaPhase.Pick)
            {
                return;
            }

            if (!ArenaRules.HasMask(_state.Participants, slot))
            {
                return;
            }

            var controller = MatchRuntime.Current?.Controller;
            if (controller == null)
            {
                return;
            }

            var raw = heroSlot <= 0 ? ArenaRules.PickValueTitan : heroSlot;
            if (raw != ArenaRules.PickValueTitan)
            {
                if (ArenaRules.IsTitanArena(_state.Index)
                    || !ArenaPairing.IsHeroHired(controller, slot, raw)
                    || ArenaRules.HasUsedHero(_usedHeroMask, slot, raw))
                {
                    return;
                }
            }
            else if (!ArenaPairing.IsTitanHired(controller, slot))
            {
                return;
            }

            _state.Picks = ArenaRules.WritePick(_state.Picks, slot, raw);
            _state.Submitted = ArenaRules.SetMask(_state.Submitted, slot, true);

            if (slot == _state.ChallengerSlot
                && challengerTarget >= 0
                && challengerTarget != slot
                && ArenaRules.HasMask(_state.Participants, challengerTarget))
            {
                _state.ChallengerTarget = challengerTarget;
            }

            ArenaNetworkBridge.Publish(_state);
        }

        void PublishFighters()
        {
            _state.A = MakeFighter(_sim.A, 0);
            _state.B = MakeFighter(_sim.B, 1);
        }

        ArenaFighterNet MakeFighter(MatchUnitState unit, int index)
        {
            if (unit == null)
            {
                return ArenaFighterNet.Empty;
            }

            return new ArenaFighterNet
            {
                Slot = unit.OwnerSlot,
                HeroSlot = unit.IsHero ? unit.HeroSlot : 0,
                Hp = unit.CurrentHp,
                MaxHp = unit.Stats.MaxHp,
                X = unit.WorldPosition.x,
                Z = unit.WorldPosition.z,
                FacingDegrees = _sim.FacingOf(index),
                Behavior = (byte)unit.BehaviorState,
                AttackSwing = (byte)(unit.AttackSwingSerial & 0xFF),
                IsDead = unit.IsAlive ? (byte)0 : (byte)1,
            };
        }

        #endregion

        #region Локальная часть (все пиры)

        void TickLocal(float unscaledDelta)
        {
            var state = IsHost ? _state : ArenaNetworkBridge.Current;
            var active = state.Phase != (byte)ArenaPhase.Idle;

            if (active && !_cameraTaken)
            {
                TakeCamera();
            }
            else if (!active && _cameraTaken)
            {
                ReleaseCamera();
            }

            _presenter?.Apply(in state, unscaledDelta);
        }

        void TakeCamera()
        {
            var pan = GameplayCameraPanController.Current;
            if (pan != null)
            {
                _savedCameraPosition = pan.PanPosition;
                _savedYawDegrees = pan.TargetYawDegrees;
                pan.SetPanBoundsEnabled(false);
                pan.SetPanInputLocked(true);
                pan.SetPanPosition(ArenaRules.Center);
            }

            _cameraTaken = true;
        }

        void ReleaseCamera()
        {
            var pan = GameplayCameraPanController.Current;
            if (pan != null)
            {
                pan.SetPanPosition(_savedCameraPosition);
                pan.SetYawDegrees(_savedYawDegrees);
                pan.SetPanBoundsEnabled(true);
                pan.SetPanInputLocked(false);
            }

            _cameraTaken = false;
        }

        #endregion
    }
}
