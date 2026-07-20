using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Tracks the last CrossFade request for a unit Animator.</summary>
    public sealed class UnitCombatAnimatorPlayback
    {
        public string CurrentStateName;
        public bool IsDead;
    }

    /// <summary>
    /// Drives unit combat clips via immediate <see cref="Animator.CrossFade"/> on behavior changes.
    /// Never waits for clip exit time — a new status always blends to the matching state right away.
    /// </summary>
    public static class UnitCombatAnimatorDriver
    {
        public const string SpeedParam = "Speed";
        public const string AttackParam = "Attack";
        public const string DeathParam = "Death";

        public const string StandState = "Stand";
        public const string WalkState = "Walk";
        public const string AttackState = "Attack";
        public const string DeathState = "Death";

        public const float LocomotionCrossFadeDuration = 0.2f;
        public const float AttackCrossFadeDuration = 0.12f;
        public const float DeathCrossFadeDuration = 0.12f;
        public const float DeathVisualSeconds = 3.2f;

        public static float ResolveSpeed(UnitBehaviorState behaviorState) =>
            behaviorState is UnitBehaviorState.Move or UnitBehaviorState.Chase ? 1f : 0f;

        public static string ResolveLocomotionState(UnitBehaviorState behaviorState) =>
            ResolveSpeed(behaviorState) > 0.1f ? WalkState : StandState;

        /// <summary>
        /// Maps combat status to the animator state that should play right now.
        /// </summary>
        public static string ResolveDesiredState(
            UnitBehaviorState behaviorState,
            bool fireAttack,
            bool fireDeath,
            bool isDead)
        {
            if (fireDeath || isDead)
            {
                return DeathState;
            }

            // Attack status (or a new swing) → Attack immediately, never idle-gap first.
            if (fireAttack || behaviorState == UnitBehaviorState.Attack)
            {
                return AttackState;
            }

            return ResolveLocomotionState(behaviorState);
        }

        public static float ResolveCrossFadeDuration(string stateName)
        {
            if (stateName == AttackState)
            {
                return AttackCrossFadeDuration;
            }

            if (stateName == DeathState)
            {
                return DeathCrossFadeDuration;
            }

            return LocomotionCrossFadeDuration;
        }

        public static bool ShouldForceRestartAttack(bool fireAttack, string desiredState) =>
            fireAttack && desiredState == AttackState;

        public static bool IsInStateOrTransitioningTo(Animator animator, string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName(stateName))
            {
                return true;
            }

            return animator.IsInTransition(0)
                && animator.GetNextAnimatorStateInfo(0).IsName(stateName);
        }

        public static void Tick(
            Animator animator,
            UnitCombatAnimatorPlayback playback,
            UnitBehaviorState behaviorState,
            bool fireAttack,
            bool fireDeath)
        {
            if (animator == null)
            {
                throw new System.ArgumentNullException(nameof(animator));
            }

            if (playback == null)
            {
                throw new System.ArgumentNullException(nameof(playback));
            }

            animator.SetFloat(SpeedParam, ResolveSpeed(behaviorState));

            if (fireDeath || playback.IsDead)
            {
                playback.IsDead = true;
            }

            var desired = ResolveDesiredState(
                behaviorState,
                fireAttack,
                fireDeath,
                playback.IsDead);

            var forceRestart = ShouldForceRestartAttack(fireAttack, desired);
            CrossFade(
                animator,
                playback,
                desired,
                ResolveCrossFadeDuration(desired),
                forceRestart);
        }

        static void CrossFade(
            Animator animator,
            UnitCombatAnimatorPlayback playback,
            string stateName,
            float duration,
            bool force)
        {
            if (!force
                && playback.CurrentStateName == stateName
                && IsInStateOrTransitioningTo(animator, stateName))
            {
                return;
            }

            // normalizedTimeOffset 0 = start clip from beginning (required to re-fire Attack in a loop).
            // The 3-arg CrossFade keeps current time when already in the same state — that froze combat anims.
            animator.CrossFade(stateName, duration, 0, force ? 0f : float.NegativeInfinity);
            playback.CurrentStateName = stateName;
        }
    }
}
