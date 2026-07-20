using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    public sealed class CombatProjectileState
    {
        public CombatProjectileState(
            int projectileId,
            int attackerUnitId,
            int targetUnitId,
            int attackerOwnerSlot,
            UnitRole attackerRole,
            string attackerRaceId,
            float rawDamage,
            float flightDuration,
            UnityEngine.Vector3 startPosition,
            UnityEngine.Vector3 targetPosition,
            bool isParabolic,
            int? targetBuildingInstanceId = null,
            int? sourceBuildingInstanceId = null)
        {
            ProjectileId = projectileId;
            AttackerUnitId = attackerUnitId;
            TargetUnitId = targetUnitId;
            TargetBuildingInstanceId = targetBuildingInstanceId;
            SourceBuildingInstanceId = sourceBuildingInstanceId;
            AttackerOwnerSlot = attackerOwnerSlot;
            AttackerRole = attackerRole;
            AttackerRaceId = attackerRaceId;
            RawDamage = rawDamage;
            FlightDuration = flightDuration;
            StartPosition = startPosition;
            TargetPosition = targetPosition;
            IsParabolic = isParabolic;
        }

        public int ProjectileId { get; }
        public int AttackerUnitId { get; }
        public int TargetUnitId { get; }
        public int? TargetBuildingInstanceId { get; }
        /// <summary>Set when a defensive building fired this shot (cube in owner slot color).</summary>
        public int? SourceBuildingInstanceId { get; }
        public bool IsBuildingAttack => SourceBuildingInstanceId.HasValue;
        public int AttackerOwnerSlot { get; }
        public UnitRole AttackerRole { get; }
        public string AttackerRaceId { get; }
        public float RawDamage { get; }
        public float FlightDuration { get; }
        public UnityEngine.Vector3 StartPosition { get; }
        public UnityEngine.Vector3 TargetPosition { get; }
        public bool IsParabolic { get; }
        public float Elapsed { get; set; }

        public float Progress => FlightDuration > 0f ? Elapsed / FlightDuration : 1f;
    }
}
