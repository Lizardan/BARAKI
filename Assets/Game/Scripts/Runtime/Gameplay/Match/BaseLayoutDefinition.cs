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
        /// TT building meshes are authored with the door on local +X; yaw maps door → transform forward (+Z).
        /// Same pack quirk as <see cref="UnitGreyboxVisuals.AnimatedHumanModelYawDegrees"/>.
        /// </summary>
        public const float BuildingModelYawDegrees = -90f;

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
        /// Desired door/face direction in base local space.
        /// Main and center barracks face the central road (+Z); side barracks face creep exit (±X).
        /// </summary>
        public static Vector3 GetLocalFacingDirection(string buildingId) => buildingId switch
        {
            GameIds.Buildings.BarracksLeft => Vector3.left,
            GameIds.Buildings.BarracksRight => Vector3.right,
            _ => Vector3.forward,
        };

        /// <summary>Local rotation so the building door faces <see cref="GetLocalFacingDirection"/>.</summary>
        public static Quaternion GetLocalRotation(string buildingId)
        {
            var face = GetLocalFacingDirection(buildingId);
            if (face.sqrMagnitude < 0.0001f)
            {
                face = Vector3.forward;
            }

            return Quaternion.LookRotation(face.normalized, Vector3.up)
                   * Quaternion.Euler(0f, BuildingModelYawDegrees, 0f);
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
