using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Dev
{
    /// <summary>Stand / Walk / Attack switcher for the Faceless review lineup.</summary>
    public sealed class FacelessReviewPlayback : MonoBehaviour
    {
        public const int PoseStand = 0;
        public const int PoseWalk = 1;
        public const int PoseAttack = 2;

        static readonly int TeamColorId = Shader.PropertyToID("_TeamColor");

        public static FacelessReviewPlayback Current { get; private set; }

        [SerializeField] int _teamSlot = 1;

        int _pose;

        public int Pose => _pose;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        void Start()
        {
            ApplyTeamColors();
            SetPose(_pose);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                SetPose(PoseStand);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                SetPose(PoseWalk);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                SetPose(PoseAttack);
            }
        }

        public void SetPose(int pose)
        {
            _pose = pose is PoseWalk or PoseAttack ? pose : PoseStand;
            var stateName = _pose == PoseWalk
                ? UnitCombatAnimatorDriver.WalkState
                : _pose == PoseAttack
                    ? UnitCombatAnimatorDriver.AttackState
                    : UnitCombatAnimatorDriver.StandState;
            var duration = _pose == PoseAttack
                ? UnitCombatAnimatorDriver.AttackCrossFadeDuration
                : UnitCombatAnimatorDriver.LocomotionCrossFadeDuration;
            var units = FindObjectsByType<FacelessReviewUnit>(FindObjectsInactive.Exclude);
            for (var i = 0; i < units.Length; i++)
            {
                var animator = units[i].GetComponent<Animator>();
                if (animator == null || !animator.isActiveAndEnabled)
                {
                    continue;
                }

                animator.CrossFade(stateName, duration, 0, float.NegativeInfinity);
            }
        }

        void ApplyTeamColors()
        {
            var color = MatchPlayerColors.GetSlotColor(_teamSlot);
            var units = FindObjectsByType<FacelessReviewUnit>(FindObjectsInactive.Exclude);
            for (var i = 0; i < units.Length; i++)
            {
                var renderer = units[i].GetComponent<SkinnedMeshRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(TeamColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
