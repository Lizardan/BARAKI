using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Изолированная симуляция дуэли 1v1. Поднимает собственный экземпляр
    /// <see cref="MatchCombatSystem"/> с крошечным синтетическим <see cref="LaneGraph"/>
    /// на арене: реальные статы героя, его способности и формулы урона, но ни одного
    /// юнита/здания основного матча — дуэль не может влиять на карту и наоборот.
    /// </summary>
    public sealed class ArenaDuelSim
    {
        public const string LaneAId = "ARENA_A";
        public const string LaneBId = "ARENA_B";

        const float FixedDelta = 1f / 30f;
        const int MaxStepsPerFrame = 6;

        MatchCombatSystem _combat;
        MatchUnitState _a;
        MatchUnitState _b;
        float _accumulator;
        float _elapsed;
        readonly Vector3[] _lastPosition = { Vector3.zero, Vector3.zero };
        readonly float[] _facing = { 0f, 0f };

        public bool IsRunning { get; private set; }

        /// <summary>0 — победил боец A, 1 — боец B, -1 — дуэль идёт.</summary>
        public int WinnerIndex { get; private set; } = -1;

        public MatchUnitState A => _a;
        public MatchUnitState B => _b;
        public float ElapsedSeconds => _elapsed;

        public void Begin(MatchController controller, in ArenaDuel duel, int seed)
        {
            Reset();
            if (controller == null || !duel.IsValid)
            {
                return;
            }

            var center = ArenaRules.Center;
            var west = center + new Vector3(-ArenaRules.SpawnOffset, 0f, 0f);
            var east = center + new Vector3(ArenaRules.SpawnOffset, 0f, 0f);

            _combat = new MatchCombatSystem
            {
                UnitVisualCatalog = controller.UnitVisualCatalog,
                AbilityCatalog = controller.Combat?.AbilityCatalog,
            };
            _combat.Reset(controller.Players, BuildGraph(duel.SlotA, duel.SlotB, west, east), seed);

            _a = Spawn(controller, duel.SlotA, duel.HeroA, LaneAId);
            _b = Spawn(controller, duel.SlotB, duel.HeroB, LaneBId);

            if (_a == null || _b == null)
            {
                Reset();
                return;
            }

            _lastPosition[0] = _a.WorldPosition;
            _lastPosition[1] = _b.WorldPosition;
            _facing[0] = 90f;
            _facing[1] = -90f;
            IsRunning = true;
        }

        MatchUnitState Spawn(MatchController controller, int slot, int heroSlot, string laneId)
        {
            var player = slot >= 0 && slot < controller.Players.Count ? controller.Players[slot] : null;
            if (player == null)
            {
                return null;
            }

            // В дуэли heroSlot: 1..3 — герой, 0 — титан.
            var isTitan = heroSlot <= 0;
            var stats = isTitan
                ? controller.ResolveArenaTitanStats(slot)
                : controller.ResolveArenaHeroStats(slot, heroSlot);
            var level = isTitan
                ? ArenaPairing.TitanLevel(controller, slot)
                : ArenaPairing.HeroLevel(controller, slot, heroSlot);

            return _combat.SpawnUnit(
                slot,
                laneId,
                isTitan ? UnitRole.Titan : UnitRole.Hero,
                stats,
                distanceAlongLane: 0f,
                formationOffset: default,
                isHero: !isTitan,
                heroSlot: isTitan ? 0 : heroSlot,
                level: level);
        }

        static LaneGraph BuildGraph(int slotA, int slotB, Vector3 west, Vector3 east)
        {
            var lanes = new List<LaneSpline>
            {
                new()
                {
                    OwnerSlot = slotA,
                    OpponentSlot = slotB,
                    LaneId = LaneAId,
                    OriginBarracksId = string.Empty,
                    IsCenterLane = false,
                    Path = new LanePath(new[] { west, ArenaRules.Center, east }),
                },
                new()
                {
                    OwnerSlot = slotB,
                    OpponentSlot = slotA,
                    LaneId = LaneBId,
                    OriginBarracksId = string.Empty,
                    IsCenterLane = false,
                    Path = new LanePath(new[] { east, ArenaRules.Center, west }),
                },
            };

            return new LaneGraph
            {
                TopologyId = "ARENA_DUEL",
                PlayerCount = 2,
                CenterArenaRadius = ArenaRules.Radius,
                Lanes = lanes,
            };
        }

        /// <summary>Тик симуляции нескалированным временем (матч в этот момент на паузе).</summary>
        public void Tick(float unscaledDelta)
        {
            if (!IsRunning || _combat == null)
            {
                return;
            }

            _accumulator += Mathf.Clamp(unscaledDelta, 0f, 0.25f);
            var steps = 0;
            while (_accumulator >= FixedDelta && steps < MaxStepsPerFrame)
            {
                _accumulator -= FixedDelta;
                steps++;
                _elapsed += FixedDelta;
                _combat.Tick(FixedDelta);
                if (ResolveOutcome())
                {
                    break;
                }
            }

            if (steps > 0)
            {
                UpdateFacing();
                if (!ResolveOutcome() && _elapsed >= ArenaRules.MaxDuelSeconds)
                {
                    FinishByTimeout();
                }
            }
        }

        void UpdateFacing()
        {
            UpdateOne(0, _a);
            UpdateOne(1, _b);
            return;

            void UpdateOne(int index, MatchUnitState unit)
            {
                if (unit == null)
                {
                    return;
                }

                var delta = unit.WorldPosition - _lastPosition[index];
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0004f)
                {
                    _facing[index] = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                }

                _lastPosition[index] = unit.WorldPosition;
            }
        }

        bool ResolveOutcome()
        {
            if (!IsRunning)
            {
                return true;
            }

            var aAlive = _a != null && _a.IsAlive;
            var bAlive = _b != null && _b.IsAlive;

            if (aAlive && !bAlive)
            {
                WinnerIndex = 0;
                IsRunning = false;
                return true;
            }

            if (bAlive && !aAlive)
            {
                WinnerIndex = 1;
                IsRunning = false;
                return true;
            }

            if (!aAlive && !bAlive)
            {
                // Обоих не стало одновременно — побеждает тот, у кого больше осталось HP.
                WinnerIndex = (_a?.CurrentHp ?? 0f) >= (_b?.CurrentHp ?? 0f) ? 0 : 1;
                IsRunning = false;
                return true;
            }

            return false;
        }

        void FinishByTimeout()
        {
            var aRatio = _a is { IsAlive: true } ? _a.CurrentHp / Mathf.Max(1f, _a.Stats.MaxHp) : -1f;
            var bRatio = _b is { IsAlive: true } ? _b.CurrentHp / Mathf.Max(1f, _b.Stats.MaxHp) : -1f;
            WinnerIndex = aRatio >= bRatio ? 0 : 1;
            IsRunning = false;
        }

        public float FacingOf(int index) => index == 0 ? _facing[0] : _facing[1];

        public void Reset()
        {
            _combat = null;
            _a = null;
            _b = null;
            _accumulator = 0f;
            _elapsed = 0f;
            IsRunning = false;
            WinnerIndex = -1;
        }
    }
}
