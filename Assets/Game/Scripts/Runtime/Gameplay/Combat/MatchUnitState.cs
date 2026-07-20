using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public sealed class MatchUnitState
    {
        public MatchUnitState(
            int unitId,
            int ownerSlot,
            string laneId,
            UnitRole role,
            UnitCombatStats stats,
            float currentHp,
            Vector3 worldPosition,
            int marchWaypointIndex = 0,
            float? marchMoveSpeed = null,
            float marchSpawnDistance = 0f,
            bool isHero = false,
            int heroSlot = 0)
        {
            UnitId = unitId;
            OwnerSlot = ownerSlot;
            LaneId = laneId;
            Role = role;
            Stats = stats;
            CurrentHp = currentHp;
            CurrentMana = stats.MaxMana > 0f ? stats.MaxMana : 0f;
            WorldPosition = worldPosition;
            MarchWaypointIndex = marchWaypointIndex;
            MarchMoveSpeed = marchMoveSpeed ?? stats.MoveSpeed;
            MarchSpawnDistance = marchSpawnDistance;
            MarchProgressDistance = marchSpawnDistance;
            BehaviorState = UnitBehaviorState.Move;
            FacingDirection = Vector3.forward;
            IsHero = isHero || role == UnitRole.Hero;
            HeroSlot = heroSlot;
            IsParkedAtBase = false;
        }

        public int UnitId { get; }
        public int OwnerSlot { get; }
        public string LaneId { get; set; }
        public UnitRole Role { get; }
        public UnitCombatStats Stats { get; }
        public bool IsHero { get; }
        public int HeroSlot { get; }
        /// <summary>Hired hero waiting behind base — no march/combat AI.</summary>
        public bool IsParkedAtBase { get; set; }
        public float CurrentHp { get; set; }
        public float CurrentMana { get; set; }
        public Vector3 WorldPosition { get; set; }
        public int MarchWaypointIndex { get; set; }
        public float MarchMoveSpeed { get; }
        public float MarchSpawnDistance { get; }
        public float MarchProgressDistance { get; set; }
        public float AttackCooldownRemaining { get; set; }
        /// <summary>Incremented each time this unit starts an attack swing (anim re-trigger).</summary>
        public int AttackSwingSerial { get; set; }
        public UnitBehaviorState BehaviorState { get; set; }
        public int? CurrentTargetId { get; set; }
        public int? CurrentTargetBuildingInstanceId { get; set; }
        public float TargetScanCooldown { get; set; }
        public Vector3 FacingDirection { get; set; }
        /// <summary>
        /// Old open mid path kept after enemy wipe when past mid-halfway.
        /// Null = use the shared lane route.
        /// </summary>
        public LanePath CommittedMarchPath { get; set; }
        /// <summary>
        /// Opponent slot this unit is marching toward (flank remount / mid commit / flank spawn).
        /// Used to retarget when that foe is eliminated mid-march.
        /// </summary>
        public int MarchFocusOpponentSlot { get; set; } = -1;
        public bool IsAlive => CurrentHp > 0f;
    }
}
