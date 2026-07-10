using Game.Gameplay.Data;
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
            float marchSpawnDistance = 0f)
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
        }

        public int UnitId { get; }
        public int OwnerSlot { get; }
        public string LaneId { get; }
        public UnitRole Role { get; }
        public UnitCombatStats Stats { get; }
        public float CurrentHp { get; set; }
        public float CurrentMana { get; set; }
        public Vector3 WorldPosition { get; set; }
        public int MarchWaypointIndex { get; set; }
        public float MarchMoveSpeed { get; }
        public float MarchSpawnDistance { get; }
        public float MarchProgressDistance { get; set; }
        public float AttackCooldownRemaining { get; set; }
        public UnitBehaviorState BehaviorState { get; set; }
        public int? CurrentTargetId { get; set; }
        public int? CurrentTargetBuildingInstanceId { get; set; }
        public float TargetScanCooldown { get; set; }
        public Vector3 FacingDirection { get; set; }
        public bool IsAlive => CurrentHp > 0f;
    }
}
