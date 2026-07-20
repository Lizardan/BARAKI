using Game.Core;
using Game.Gameplay.Match.Selection;

namespace Game.Gameplay.Match
{
    /// <summary>GDD building HP/armor and lane building engage rules.</summary>
    public static class BuildingRules
    {
        public static readonly string[] EliminationBuildingIds =
        {
            GameIds.Buildings.Main,
            GameIds.Buildings.BarracksLeft,
            GameIds.Buildings.BarracksCenter,
            GameIds.Buildings.BarracksRight,
            GameIds.Buildings.TowerNw,
            GameIds.Buildings.TowerNe,
            GameIds.Buildings.TowerSw,
            GameIds.Buildings.TowerSe,
        };

        public static float GetMaxHp(string buildingId) => buildingId switch
        {
            GameIds.Buildings.Main => 2000f,
            GameIds.Buildings.BarracksLeft
                or GameIds.Buildings.BarracksCenter
                or GameIds.Buildings.BarracksRight => 800f,
            GameIds.Buildings.TowerNw
                or GameIds.Buildings.TowerNe
                or GameIds.Buildings.TowerSw
                or GameIds.Buildings.TowerSe => 600f,
            _ => 800f,
        };

        public static float GetArmor(string buildingId) => buildingId switch
        {
            GameIds.Buildings.Main => 5f,
            GameIds.Buildings.BarracksLeft
                or GameIds.Buildings.BarracksCenter
                or GameIds.Buildings.BarracksRight => 2f,
            GameIds.Buildings.TowerNw
                or GameIds.Buildings.TowerNe
                or GameIds.Buildings.TowerSw
                or GameIds.Buildings.TowerSe => 3f,
            _ => 2f,
        };

        public static bool IsBarracks(string buildingId) =>
            buildingId is GameIds.Buildings.BarracksLeft
                or GameIds.Buildings.BarracksCenter
                or GameIds.Buildings.BarracksRight;

        public static bool IsTower(string buildingId) =>
            buildingId is GameIds.Buildings.Tower
                or GameIds.Buildings.TowerNw
                or GameIds.Buildings.TowerNe
                or GameIds.Buildings.TowerSw
                or GameIds.Buildings.TowerSe;

        public static bool IsMain(string buildingId) =>
            buildingId == GameIds.Buildings.Main;

        /// <summary>Main, barracks, and towers auto-fire in playtest defense radius.</summary>
        public static bool IsDefensiveBuilding(string buildingId) =>
            IsMain(buildingId) || IsBarracks(buildingId) || IsTower(buildingId);

        public static string GetLaneBinding(string buildingId) => buildingId switch
        {
            GameIds.Buildings.BarracksLeft => GameIds.Lanes.Left,
            GameIds.Buildings.BarracksCenter => GameIds.Lanes.Center,
            GameIds.Buildings.BarracksRight => GameIds.Lanes.Right,
            _ => string.Empty,
        };

        /// <summary>Half of pick footprint diameter — used for attack/aggro distance to buildings.</summary>
        public static float GetEngageRadius(string buildingId) =>
            MatchPickFootprint.GetBuildingDiameter(buildingId) * 0.5f;

        /// <summary>
        /// Any intact building not owned by the attacker is a valid target.
        /// No role-specific building priority; aggro radius limits engagement.
        /// </summary>
        public static bool CanLaneAttackBuilding(
            int attackerOwnerSlot,
            string attackerLaneId,
            BuildingState building,
            LaneGraph graph)
        {
            _ = attackerLaneId;
            _ = graph;
            return building != null
                   && building.IsIntact
                   && building.OwnerSlot != attackerOwnerSlot;
        }

        /// <summary>Legacy name — same as lane attack eligibility without graph (tests / simple checks).</summary>
        public static bool CanSiegeTarget(string unitLaneId, string buildingId)
        {
            // Buildings are approachable from any lane once near them; keep a soft hint for Main/towers.
            if (IsBarracks(buildingId))
            {
                return true;
            }

            return unitLaneId == GameIds.Lanes.Center
                   || string.IsNullOrEmpty(BuildingRules.GetLaneBinding(buildingId));
        }

        public static float GetSurfaceDistance(float centerDistance, string buildingId) =>
            System.Math.Max(0f, centerDistance - GetEngageRadius(buildingId));
    }
}
