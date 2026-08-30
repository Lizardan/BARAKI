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
            int? sourceBuildingInstanceId = null,
            string sourceBuildingId = null,
            bool appliesSplashAoe = false)
        {
            ProjectileId = projectileId;
            AttackerUnitId = attackerUnitId;
            TargetUnitId = targetUnitId;
            TargetBuildingInstanceId = targetBuildingInstanceId;
            SourceBuildingInstanceId = sourceBuildingInstanceId;
            SourceBuildingId = sourceBuildingId;
            AttackerOwnerSlot = attackerOwnerSlot;
            AttackerRole = attackerRole;
            AttackerRaceId = attackerRaceId;
            RawDamage = rawDamage;
            FlightDuration = flightDuration;
            StartPosition = startPosition;
            TargetPosition = targetPosition;
            IsParabolic = isParabolic;
            AppliesSplashAoe = appliesSplashAoe;
            if (UnityEngine.Application.isPlaying)
            {
                SpawnRealtime = UnityEngine.Time.time;
            }
        }

        public int ProjectileId { get; }
        public int AttackerUnitId { get; }
        public int TargetUnitId { get; }
        public int? TargetBuildingInstanceId { get; }
        /// <summary>Set when a defensive building fired this shot (bolt in owner slot color).</summary>
        public int? SourceBuildingInstanceId { get; }
        /// <summary>Id of the building that fired this shot; null for unit attacks.</summary>
        public string SourceBuildingId { get; }
        public bool IsBuildingAttack => SourceBuildingInstanceId.HasValue;
        public int AttackerOwnerSlot { get; }
        public UnitRole AttackerRole { get; }
        public string AttackerRaceId { get; }
        public float RawDamage { get; }
        public float FlightDuration { get; }
        public UnityEngine.Vector3 StartPosition { get; }
        public UnityEngine.Vector3 TargetPosition { get; }
        public bool IsParabolic { get; }
        /// <summary>Bonus Super (catapult): splash on impact; host damage only.</summary>
        public bool AppliesSplashAoe { get; }
        public float Elapsed { get; set; }
        /// <summary>-1 when unknown (EditMode). Wall-clock spawn for frame-rate presentation.</summary>
        public float SpawnRealtime { get; set; } = -1f;

        public float Progress => ResolvePresentationProgress();

        public float ResolvePresentationElapsed() =>
            ResolvePresentationElapsed(UnityEngine.Time.time);

        /// <summary>
        /// Presentation elapsed uses wall-clock (Time.time - SpawnRealtime) for smooth ~60 fps visuals.
        /// Damage/impact timing uses <see cref="Elapsed"/> which advances on 30 Hz sim ticks (authoritative).
        /// </summary>
        public float ResolvePresentationElapsed(float now)
        {
            if (SpawnRealtime >= 0f)
            {
                return now - SpawnRealtime;
            }

            // Fallback for EditMode tests where SpawnRealtime is not set
            return Elapsed;
        }

        public float ResolvePresentationProgress() =>
            ResolvePresentationProgress(UnityEngine.Time.time);

        public float ResolvePresentationProgress(float now)
        {
            if (FlightDuration <= 0f)
            {
                return 1f;
            }

            return UnityEngine.Mathf.Clamp01(ResolvePresentationElapsed(now) / FlightDuration);
        }
    }
}
