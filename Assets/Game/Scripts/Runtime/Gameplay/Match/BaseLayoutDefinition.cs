using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Per-player building offsets in base local space (+Z = toward map center, −Z = map edge / rear).
    /// See <c>Buildings.md</c> BASE_LAYOUT.
    /// </summary>
    public static class BaseLayoutDefinition
    {
        public const int BuildingsPerBase = 8;
        public const int LanesPerPlayer = 3;

        /// <summary>MAIN→barracks distance as a multiple of main→tower distance (legacy 2f; 1.5f = 25% closer).</summary>
        public const float BarracksOffsetFactor = 1.5f;

        /// <summary>
        /// Extra yaw on top of <see cref="Quaternion.LookRotation"/> toward
        /// <see cref="GetLocalFacingDirection"/>. TT prefab forward (+Z) is the visual face;
        /// 0 keeps LookRotation aim = face (door/+X sits on the model's right).
        /// </summary>
        public const float BuildingModelYawDegrees = 0f;

        public static IReadOnlyDictionary<string, Vector3> GetLocalOffsets(float mainToTowerDistance)
        {
            var d = mainToTowerDistance;
            var barracks = d * BarracksOffsetFactor;
            return new Dictionary<string, Vector3>
            {
                [GameIds.Buildings.Main] = Vector3.zero,
                [GameIds.Buildings.TowerNw] = new(-d, 0f, d),
                [GameIds.Buildings.TowerNe] = new(d, 0f, d),
                [GameIds.Buildings.TowerSw] = new(-d, 0f, -d),
                [GameIds.Buildings.TowerSe] = new(d, 0f, -d),
                [GameIds.Buildings.BarracksCenter] = new(0f, 0f, barracks),
                [GameIds.Buildings.BarracksLeft] = new(-barracks, 0f, 0f),
                [GameIds.Buildings.BarracksRight] = new(barracks, 0f, 0f),
            };
        }

        /// <summary>
        /// Desired visual face direction in base local space.
        /// Main and center barracks face the central road (+Z);
        /// side barracks and same-side towers face creep exit (±X).
        /// </summary>
        public static Vector3 GetLocalFacingDirection(string buildingId) => buildingId switch
        {
            GameIds.Buildings.BarracksLeft
                or GameIds.Buildings.TowerNw
                or GameIds.Buildings.TowerSw => Vector3.left,
            GameIds.Buildings.BarracksRight
                or GameIds.Buildings.TowerNe
                or GameIds.Buildings.TowerSe => Vector3.right,
            _ => Vector3.forward,
        };

        /// <summary>Local rotation so prefab forward faces <see cref="GetLocalFacingDirection"/>.</summary>
        public static Quaternion GetLocalRotation(string buildingId)
        {
            var face = GetLocalFacingDirection(buildingId);
            if (face.sqrMagnitude < 0.0001f)
            {
                face = Vector3.forward;
            }

            var rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            if (Mathf.Abs(BuildingModelYawDegrees) < 0.01f)
            {
                return rotation;
            }

            return rotation * Quaternion.Euler(0f, BuildingModelYawDegrees, 0f);
        }

        public static string GetLaneForBarracks(string barracksId) => barracksId switch
        {
            GameIds.Buildings.BarracksLeft => GameIds.Lanes.Left,
            GameIds.Buildings.BarracksCenter => GameIds.Lanes.Center,
            GameIds.Buildings.BarracksRight => GameIds.Lanes.Right,
            _ => string.Empty,
        };

        /// <summary>Flank spline terminates on the opponent barracks along the shared ring arc.</summary>
        public static string GetFlankDestinationBarracks(string laneId) => laneId switch
        {
            GameIds.Lanes.Left => GameIds.Buildings.BarracksRight,
            GameIds.Lanes.Right => GameIds.Buildings.BarracksLeft,
            _ => string.Empty,
        };

        public static string GetFlankOriginBarracks(string laneId) => laneId switch
        {
            GameIds.Lanes.Left => GameIds.Buildings.BarracksLeft,
            GameIds.Lanes.Right => GameIds.Buildings.BarracksRight,
            _ => string.Empty,
        };

        public static int GetLaneIndex(string barracksId) => barracksId switch
        {
            GameIds.Buildings.BarracksLeft => 0,
            GameIds.Buildings.BarracksCenter => 1,
            GameIds.Buildings.BarracksRight => 2,
            _ => -1,
        };
    }
}
