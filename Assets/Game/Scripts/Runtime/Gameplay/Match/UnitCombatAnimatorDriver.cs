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
    /// Attack clip rate scales with attack interval so one clip equals one swing (Haste Aura included).
    /// </summary>
    public static class UnitCombatAnimatorDriver
    {
        public const string SpeedParam = "Speed";
        public const string AttackParam = "Attack";
        public const string DeathParam = "Death";
        public const string AttackVariantParam = "AttackVariant";
        public const string CastVariantParam = "CastVariant";

        public const string StandState = "Stand";
        public const string WalkState = "Walk";
        public const string AttackState = "Attack";
        public const string CastState = "Cast";
        public const string DeathState = "Death";

        public const float LocomotionCrossFadeDuration = 0.2f;
        public const float AttackCrossFadeDuration = 0.12f;
        public const float CastCrossFadeDuration = 0.12f;
        public const float DeathCrossFadeDuration = 0.12f;
        public const float DeathVisualSeconds = 3.2f;

        /// <summary>Melee creep baseline (GDD / UNIT_HUMAN_MELEE) — walk clip is authored around this.</summary>
        public const float ReferenceMoveSpeed = 4f;

        /// <summary>
        /// Fallback when role is unknown. Prefer <see cref="AbilityAnimRules.ResolveAttackClipSeconds"/>.
        /// </summary>
        public const float ReferenceAttackClipLength = AbilityAnimRules.CavalryOrMachineAttackClipSeconds;

        public const float MinWalkPlaybackSpeed = 0.2f;
        public const float MaxWalkPlaybackSpeed = 2.5f;
        public const float MinAttackPlaybackSpeed = 0.35f;
        public const float MaxAttackPlaybackSpeed = 3f;

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
        /// Attack clip playback so one authored clip spans one attack interval
        /// (Haste Aura shortens interval ⇒ faster swing).
        /// </summary>
        public static float ResolveAttackPlaybackSpeed(
            float attackIntervalSeconds,
            float attackClipLength = ReferenceAttackClipLength)
        {
            var interval = Mathf.Max(0.05f, attackIntervalSeconds);
            var length = Mathf.Max(0.05f, attackClipLength);
            return Mathf.Clamp(length / interval, MinAttackPlaybackSpeed, MaxAttackPlaybackSpeed);
        }

        /// <summary>Picks a discrete BlendTree child index for Attack/Cast pools.</summary>
        public static float ResolveVariant(int clipCount, int sample)
        {
            if (clipCount <= 1)
            {
                return 0f;
            }

            var index = sample % clipCount;
            if (index < 0)
            {
                index += clipCount;
            }

            return index;
        }

        public static float ResolveRandomVariant(int clipCount) =>
            ResolveVariant(clipCount, Random.Range(0, Mathf.Max(1, clipCount)));

        /// <summary>
        /// Global <see cref="Animator.speed"/>: scaled while walking; attack uses interval;
        /// cast/stand/death stay at 1.
        /// </summary>
        public static float ResolveAnimatorPlaybackSpeed(
            UnitBehaviorState behaviorState,
            float moveSpeed,
            float visualScaleVsCreep,
            bool isDead = false,
            float attackIntervalSeconds = 1f,
            float attackClipLength = ReferenceAttackClipLength)
        {
            if (isDead)
            {
                return 1f;
            }

            if (behaviorState == UnitBehaviorState.Attack)
            {
                return ResolveAttackPlaybackSpeed(attackIntervalSeconds, attackClipLength);
            }

            if (behaviorState is UnitBehaviorState.Cast or UnitBehaviorState.Frozen)
            {
                return 1f;
            }

            if (ResolveSpeed(behaviorState) <= 0.1f)
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
            bool isDead,
            string stateOverride = null)
        {
            if (fireDeath || isDead)
            {
                return DeathState;
            }

            if (!string.IsNullOrEmpty(stateOverride))
            {
                return stateOverride;
            }

            if (behaviorState == UnitBehaviorState.Cast)
            {
                return CastState;
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

            if (stateName == CastState)
            {
                return CastCrossFadeDuration;
            }

            if (stateName == DeathState)
            {
                return DeathCrossFadeDuration;
            }

            return LocomotionCrossFadeDuration;
        }

        public static bool ShouldForceRestartAttack(bool fireAttack, string desiredState) =>
            fireAttack && desiredState == AttackState;

        public static bool HasParameter(Animator animator, string parameterName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

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
            float visualScaleVsCreep = 1f,
            float attackIntervalSeconds = 1f,
            float attackClipLength = ReferenceAttackClipLength,
            bool enteringCast = false,
            float? attackVariantOverride = null,
            float? castVariantOverride = null,
            string stateOverride = null)
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

            var desired = ResolveDesiredState(
                behaviorState,
                fireAttack,
                fireDeath,
                playback.IsDead,
                stateOverride);

            animator.speed = ResolveAnimatorPlaybackSpeed(
                behaviorState,
                moveSpeed,
                visualScaleVsCreep,
                playback.IsDead,
                attackIntervalSeconds,
                attackClipLength);

            // Death/cast must not inherit a slowed titan walk or haste attack rate.
            if (desired is DeathState or CastState or StandState)
            {
                animator.speed = 1f;
            }
            else if (desired == AttackState)
            {
                animator.speed = ResolveAttackPlaybackSpeed(attackIntervalSeconds, attackClipLength);
            }
            else if (!string.IsNullOrEmpty(stateOverride))
            {
                animator.speed = 1f;
            }

            if (fireAttack && HasParameter(animator, AttackVariantParam))
            {
                var variant = attackVariantOverride
                    ?? ResolveRandomVariant(2);
                animator.SetFloat(AttackVariantParam, variant);
            }

            if (enteringCast && HasParameter(animator, CastVariantParam))
            {
                var variant = castVariantOverride
                    ?? ResolveRandomVariant(2);
                animator.SetFloat(CastVariantParam, variant);
            }

            var forceRestart = ShouldForceRestartAttack(fireAttack, desired)
                || (enteringCast && desired == CastState)
                || (!string.IsNullOrEmpty(stateOverride)
                    && (fireAttack || enteringCast));
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
