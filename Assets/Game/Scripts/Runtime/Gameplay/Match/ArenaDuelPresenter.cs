using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Визуал дуэли: два бойца на арене. Спавнит префабы героя/титана из
    /// <see cref="UnitVisualCatalog"/> и ведёт их по реплицированному состоянию
    /// (позиции, поворот, анимации). Никакой логики боя — только отрисовка.
    /// </summary>
    public sealed class ArenaDuelPresenter : MonoBehaviour
    {
        sealed class Fighter
        {
            public GameObject Root;
            public Animator Animator;
            public UnitCombatAnimatorPlayback Playback = new();
            public float MoveSpeed = UnitCombatAnimatorDriver.ReferenceMoveSpeed;
            public float AttackIntervalSeconds = 1f;
            public Vector3 Position;
            public float FacingDegrees;
            public byte LastSwing;
            public bool DeathFired;
            public UnitBehaviorState LastBehavior = UnitBehaviorState.Move;
        }

        readonly Fighter[] _fighters = { new(), new() };
        GameObject _root;
        int _pairKey;

        public void Apply(in ArenaNetState state, float unscaledDelta)
        {
            if (state.Phase == (byte)ArenaPhase.Idle)
            {
                Clear();
                return;
            }

            var key = ComputePairKey(state);
            if (key != _pairKey)
            {
                _pairKey = key;
                Rebuild(state);
            }

            UpdateFighter(0, in state.A, unscaledDelta);
            UpdateFighter(1, in state.B, unscaledDelta);
        }

        static int ComputePairKey(in ArenaNetState state)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + state.Index;
                hash = hash * 31 + state.DuelOrdinal;
                hash = hash * 31 + state.PairASlot;
                hash = hash * 31 + state.PairAHero;
                hash = hash * 31 + state.PairBSlot;
                hash = hash * 31 + state.PairBHero;
                return hash;
            }
        }

        void Rebuild(in ArenaNetState state)
        {
            Clear();

            EnsureRoot();
            var controller = MatchRuntime.Current?.Controller;

            Spawn(0, state.PairASlot, state.PairAHero, controller, -1f);
            Spawn(1, state.PairBSlot, state.PairBHero, controller, 1f);
        }

        void Spawn(int index, int slot, int heroSlot, MatchController controller, float side)
        {
            if (slot < 0 || controller == null)
            {
                return;
            }

            if (slot >= controller.Players.Count)
            {
                return;
            }

            var player = controller.Players[slot];
            var isTitan = heroSlot <= 0;
            var catalog = controller.UnitVisualCatalog;
            if (catalog == null)
            {
                return;
            }

            var bonusSlot = isTitan
                ? BonusKitRules.EffectiveBonusSlotForTitan(
                    player.RaceId, player.BonusPickSlot, player.BonusPickSlot2)
                : BonusKitRules.EffectiveBonusSlotForHero(
                    player.RaceId, player.BonusPickSlot, player.BonusPickSlot2, heroSlot);

            if (!catalog.TryGetPrefab(
                    player.RaceId,
                    isTitan ? UnitRole.Titan : UnitRole.Hero,
                    isTitan ? 0 : heroSlot,
                    bonusSlot,
                    out var prefab)
                || prefab == null)
            {
                return;
            }

            var fighter = _fighters[index];
            fighter.Root = Instantiate(prefab, _root.transform, false);
            fighter.Root.name = $"ArenaFighter_{index}_slot{slot}";
            fighter.Root.transform.position = ArenaRules.Center
                                              + new Vector3(ArenaRules.SpawnOffset * side, 0f, 0f);
            fighter.Root.transform.rotation = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f);

            fighter.Animator = fighter.Root.GetComponentInChildren<Animator>();
            if (fighter.Animator != null)
            {
                fighter.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            fighter.Playback = new UnitCombatAnimatorPlayback();
            fighter.LastSwing = 0;
            fighter.DeathFired = false;
            fighter.LastBehavior = UnitBehaviorState.Move;
            fighter.Position = fighter.Root.transform.position;
            fighter.FacingDegrees = fighter.Root.transform.eulerAngles.y;

            var stats = isTitan
                ? controller.ResolveArenaTitanStats(slot)
                : controller.ResolveArenaHeroStats(slot, heroSlot);
            fighter.MoveSpeed = stats.MoveSpeed > 0f
                ? stats.MoveSpeed
                : UnitCombatAnimatorDriver.ReferenceMoveSpeed;
            fighter.AttackIntervalSeconds = stats.AttackSpeed > 0f
                ? 1f / stats.AttackSpeed
                : 1f;
        }

        void UpdateFighter(int index, in ArenaFighterNet data, float unscaledDelta)
        {
            var fighter = _fighters[index];
            if (fighter?.Root == null)
            {
                return;
            }

            if (data.IsPresent)
            {
                var target = new Vector3(data.X, ArenaRules.Center.y, data.Z);
                var blend = 1f - Mathf.Exp(-18f * Mathf.Max(0f, unscaledDelta));
                fighter.Position = Vector3.Lerp(fighter.Position, target, blend);
                fighter.FacingDegrees = Mathf.LerpAngle(
                    fighter.FacingDegrees,
                    data.FacingDegrees,
                    blend);
            }

            fighter.Root.transform.position = fighter.Position;
            fighter.Root.transform.rotation = Quaternion.Euler(0f, fighter.FacingDegrees, 0f);

            DriveAnimator(fighter, in data);
        }

        void DriveAnimator(Fighter fighter, in ArenaFighterNet data)
        {
            if (fighter.Animator == null)
            {
                return;
            }

            var behavior = (UnitBehaviorState)data.Behavior;
            var fireAttack = data.IsPresent
                             && data.AttackSwing != fighter.LastSwing
                             && behavior == UnitBehaviorState.Attack;
            if (data.IsPresent)
            {
                fighter.LastSwing = data.AttackSwing;
            }

            var fireDeath = data.IsDead != 0 && !fighter.DeathFired;
            if (fireDeath)
            {
                fighter.DeathFired = true;
            }

            var enteringCast = behavior == UnitBehaviorState.Cast
                               && fighter.LastBehavior != UnitBehaviorState.Cast;
            fighter.LastBehavior = behavior;

            UnitCombatAnimatorDriver.Tick(
                fighter.Animator,
                fighter.Playback,
                behavior,
                fireAttack,
                fireDeath,
                moveSpeed: fighter.MoveSpeed,
                visualScaleVsCreep: 1f,
                attackIntervalSeconds: fighter.AttackIntervalSeconds,
                enteringCast: enteringCast);
        }

        void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("ArenaDuelVisuals");
            _root.transform.SetParent(transform, false);
        }

        void Clear()
        {
            foreach (var fighter in _fighters)
            {
                if (fighter?.Root != null)
                {
                    Destroy(fighter.Root);
                }

                fighter.Root = null;
                fighter.Animator = null;
                fighter.Playback = new UnitCombatAnimatorPlayback();
                fighter.DeathFired = false;
                fighter.LastSwing = 0;
                fighter.LastBehavior = UnitBehaviorState.Move;
            }

            if (_root != null)
            {
                Destroy(_root);
                _root = null;
            }
        }

        void OnDestroy() => Clear();
    }
}
