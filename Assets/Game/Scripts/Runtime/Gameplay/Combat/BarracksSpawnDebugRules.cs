using System;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Spawn point / random area used by editor debug overlay near barracks.</summary>
    public readonly struct BarracksSpawnDebugRegion
    {
        public BarracksSpawnDebugRegion(
            Vector3 spawnPoint,
            Vector3 areaCenter,
            Vector3 forward,
            Vector3 right,
            float halfLengthAlongLane,
            float halfWidthLateral)
        {
            SpawnPoint = spawnPoint;
            AreaCenter = areaCenter;
            Forward = forward;
            Right = right;
            HalfLengthAlongLane = halfLengthAlongLane;
            HalfWidthLateral = halfWidthLateral;
        }

        /// <summary>Nominal spawn on the lane spine (no lateral offset, no jitter).</summary>
        public Vector3 SpawnPoint { get; }

        /// <summary>Center of the random/row spawn band along the lane.</summary>
        public Vector3 AreaCenter { get; }

        public Vector3 Forward { get; }
        public Vector3 Right { get; }

        /// <summary>Half-extent along march direction covering distance jitter + row depth.</summary>
        public float HalfLengthAlongLane { get; }

        /// <summary>Half-extent across the lane covering lateral random spread.</summary>
        public float HalfWidthLateral { get; }

        public void GetAreaCorners(out Vector3 forwardLeft, out Vector3 forwardRight, out Vector3 backRight, out Vector3 backLeft)
        {
            var along = Forward * HalfLengthAlongLane;
            var across = Right * HalfWidthLateral;
            forwardLeft = AreaCenter + along - across;
            forwardRight = AreaCenter + along + across;
            backRight = AreaCenter - along + across;
            backLeft = AreaCenter - along - across;
        }
    }

    public static class BarracksSpawnDebugRules
    {
        /// <summary>Worst-case role rows in a squad plan (melee…super).</summary>
        public const int MaxSquadRowIndex = 5;

        public static BarracksSpawnDebugRegion BuildRegion(LanePath path, int maxRowIndex = MaxSquadRowIndex)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            maxRowIndex = Math.Max(0, maxRowIndex);
            var nominalDistance = CombatFormationRules.BarracksSpawnForwardClearance;
            var minDistance = Mathf.Max(0f, nominalDistance - CombatFormationRules.SpawnDistanceJitter);
            var maxDistance = nominalDistance
                              + maxRowIndex * CombatFormationRules.SpawnRowDepth
                              + CombatFormationRules.SpawnDistanceJitter;
            var midDistance = (minDistance + maxDistance) * 0.5f;
            var halfLength = (maxDistance - minDistance) * 0.5f;

            var spawnPoint = Flat(path.EvaluateDistance(nominalDistance));
            var areaCenter = Flat(path.EvaluateDistance(midDistance));
            var forward = FlatDirection(path.EvaluateDirectionAtDistance(midDistance), Vector3.forward);
            var right = FlatDirection(CombatFormationRules.GetLaneRight(path, midDistance), Vector3.right);

            return new BarracksSpawnDebugRegion(
                spawnPoint,
                areaCenter,
                forward,
                right,
                halfLength,
                CombatFormationRules.MaxLateralOffset);
        }

        static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        static Vector3 FlatDirection(Vector3 value, Vector3 fallback)
        {
            value.y = 0f;
            if (value.sqrMagnitude < 0.0001f)
            {
                return fallback;
            }

            return value.normalized;
        }
    }
}
