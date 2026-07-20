using System;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public static class CombatFormationRules
    {
        public const float UnitScaleFactor = UnitGreyboxVisuals.Scale;
        public const float BarracksFootprintExtent = 4.5f;
        public const float SpawnStaggerStep = 0.9f;
        public static float SpawnRowDepth => SpawnStaggerStep * UnitScaleFactor;
        public const float SpawnDistanceJitter = 0.3f;
        public static float SpawnLateralSpread => 2.4f * UnitScaleFactor;
        /// <summary>
        /// Distance along the lane from barracks center to the first spawn band.
        /// Must clear half the barracks footprint so units appear outside the mesh (exit gate).
        /// </summary>
        public const float BarracksSpawnForwardClearance = BarracksFootprintExtent * 0.5f + 1.25f;
        public static float MinUnitSeparation => 1.7f * UnitScaleFactor;
        public static float MinLaneFollowGap => MinUnitSeparation;
        public static float MaxLateralOffset => 3.5f * UnitScaleFactor;
        public const float SeparationStrength = 0.85f;
        public static float RowLateralStep => 1.1f * UnitScaleFactor;

        public static float SampleSpawnLateralOffset(System.Random random, int unitIndex)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var baseOffset = ((unitIndex % 5) - 2f) * (SpawnLateralSpread * 0.35f);
            var jitter = ((float)random.NextDouble() * 2f - 1f) * SpawnLateralSpread * 0.45f;
            return Math.Clamp(baseOffset + jitter, -SpawnLateralSpread, SpawnLateralSpread);
        }

        public static float SampleSpawnDistanceJitter(System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return ((float)random.NextDouble() * 2f - 1f) * SpawnDistanceJitter;
        }

        public static Vector3 BuildSpawnFormationOffset(LanePath path, System.Random random, int unitIndex)
        {
            var lateral = SampleSpawnLateralOffset(random, unitIndex);
            return ApplyLateralOffset(path, 0f, lateral);
        }

        public static Vector3 BuildRowSpawnFormationOffset(
            LanePath path,
            SquadSpawnSlot slot,
            System.Random random,
            float distanceAlongLane,
            int unitIndex = 0)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var rowLateral = GetRowLateralOffset(slot.IndexInRow, slot.CountInRow);
            var randomLateral = SampleSpawnLateralOffset(random, unitIndex + slot.RowIndex * 17);
            var lateral = ClampLateral(rowLateral + randomLateral);
            return ApplyLateralOffset(path, distanceAlongLane, lateral);
        }

        public static float GetSpawnDistanceForRow(int rowIndex, int rearmostRowIndex, System.Random random)
        {
            var depthFromFront = rearmostRowIndex - rowIndex;
            var jitter = SampleSpawnDistanceJitter(random);
            return BarracksSpawnForwardClearance + Mathf.Max(0f, depthFromFront * SpawnRowDepth + jitter);
        }

        public static float GetRowLateralOffset(int indexInRow, int countInRow)
        {
            if (countInRow <= 1)
            {
                return 0f;
            }

            var center = (countInRow - 1) * 0.5f;
            return (indexInRow - center) * RowLateralStep;
        }

        public static Vector3 GetLaneRight(LanePath path, float distance)
        {
            var forward = path.EvaluateDirectionAtDistance(distance);
            return Vector3.Cross(Vector3.up, forward).normalized;
        }

        public static float GetLateralOffset(LanePath path, float distance, Vector3 worldOffset)
        {
            worldOffset.y = 0f;
            return Vector3.Dot(worldOffset, GetLaneRight(path, distance));
        }

        public static float ClampLateral(float lateral)
        {
            return Mathf.Clamp(lateral, -MaxLateralOffset, MaxLateralOffset);
        }

        public static Vector3 ApplyLateralOffset(LanePath path, float distance, float lateral)
        {
            return GetLaneRight(path, distance) * ClampLateral(lateral);
        }

        public static Vector3 ReprojectFormationOffset(LanePath path, float distance, Vector3 formationOffset)
        {
            var lateral = GetLateralOffset(path, distance, formationOffset);
            return ApplyLateralOffset(path, distance, lateral);
        }

        public static float ClampLateralDelta(float currentLateral, float lateralDelta)
        {
            return ClampLateral(currentLateral + lateralDelta) - currentLateral;
        }

        public static Vector3 ClampFormationOffset(Vector3 formationOffset)
        {
            formationOffset.y = 0f;
            var magnitude = formationOffset.magnitude;
            if (magnitude > MaxLateralOffset)
            {
                formationOffset = formationOffset / magnitude * MaxLateralOffset;
            }

            return formationOffset;
        }
    }
}
