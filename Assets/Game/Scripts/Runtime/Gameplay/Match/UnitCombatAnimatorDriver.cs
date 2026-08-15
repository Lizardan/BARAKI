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
    /// Walk clip rate scales with move speed and visual size so feet match ground travel.
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

        /// <summary>Melee creep baseline (GDD / UNIT_HUMAN_MELEE) — walk clip is authored around this.</summary>
        public const float ReferenceMoveSpeed = 4f;

        public const float MinWalkPlaybackSpeed = 0.2f;
        public const float MaxWalkPlaybackSpeed = 2.5f;

        /// <summary>Animator float used only as Stand↔Walk gate (0 or 1), not clip rate.</summary>
        public static float ResolveSpeed(UnitBehaviorState behaviorState) =>
            behaviorState is UnitBehaviorState.Move or UnitBehaviorState.Chase ? 1f : 0f;

        /// <summary>
        /// Walk clip playback: faster when moving faster, slower when the model is larger
        /// (same world speed + bigger stride ⇒ fewer steps per second).
        /// </summary>
        public static float ResolveWalkPlaybackSpeed(float moveSpeed, float visualScaleVsCreep)
        {
            var speedFactor = Mathf.Max(0f, moveSpeed) / ReferenceMoveSpeed;
            var sizeFactor = 1f / Mathf.Max(0.01f, visualScaleVsCreep);
            return Mathf.Clamp(speedFactor * sizeFactor, MinWalkPlaybackSpeed, MaxWalkPlaybackSpeed);
        }

        /// <summary>
        /// Global <see cref="Animator.speed"/>: scaled only while walking; attack/stand/death stay at 1.
        /// </summary>
        public static float ResolveAnimatorPlaybackSpeed(
            UnitBehaviorState behaviorState,
            float moveSpeed,
            float visualScaleVsCreep,
            bool isDead = false)
        {
            if (isDead || ResolveSpeed(behaviorState) <= 0.1f)
            {
                return 1f;
            }

            return ResolveWalkPlaybackSpeed(moveSpeed, visualScaleVsCreep);
        }

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

        /// <summary>Forces the idle Stand state (used for heroes parked at base).</summary>
        public static void TickStand(Animator animator, UnitCombatAnimatorPlayback playback)
        {
            if (animator == null)
            {
                throw new System.ArgumentNullException(nameof(animator));
            }

            if (playback == null)
            {
                throw new System.ArgumentNullException(nameof(playback));
            }

            animator.speed = 1f;
            animator.SetFloat(SpeedParam, 0f);
            CrossFade(animator, playback, StandState, LocomotionCrossFadeDuration, force: false);
        }

        public static void Tick(
            Animator animator,
            UnitCombatAnimatorPlayback playback,
            UnitBehaviorState behaviorState,
            bool fireAttack,
            bool fireDeath,
            float moveSpeed = ReferenceMoveSpeed,
            float visualScaleVsCreep = 1f)
        {
            if (animator == null)
            {
                throw new System.ArgumentNullException(nameof(animator));
            }

            if (playback == null)
            {
                throw new System.ArgumentNullException(nameof(playback));
            }

            if (fireDeath || playback.IsDead)
            {
                playback.IsDead = true;
            }

            animator.SetFloat(SpeedParam, ResolveSpeed(behaviorState));
            animator.speed = ResolveAnimatorPlaybackSpeed(
                behaviorState,
                moveSpeed,
                visualScaleVsCreep,
                playback.IsDead);

            var desired = ResolveDesiredState(
                behaviorState,
                fireAttack,
                fireDeath,
                playback.IsDead);

            // Attack/death must not inherit a slowed titan walk rate.
            if (desired is AttackState or DeathState)
            {
                animator.speed = 1f;
            }

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
