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

        public static readonly float[] MainMaxHpByLevel = { 2000f, 2500f, 3000f };
        public static readonly float[] BarracksMaxHpByLevel = { 800f, 1100f, 1400f, 1600f };
        public const float TowerMaxHp = 600f;

        public static float GetMaxHp(string buildingId) => GetMaxHp(buildingId, level: 1);

        public static float GetMaxHp(string buildingId, int level)
        {
            if (IsMain(buildingId))
            {
                return GetMainMaxHp(level);
            }

            if (IsBarracks(buildingId))
            {
                return GetBarracksMaxHp(level);
            }

            if (IsTower(buildingId))
            {
                return TowerMaxHp;
            }

            return BarracksMaxHpByLevel[0];
        }

        public static float GetMainMaxHp(int mainLevel)
        {
            var index = System.Math.Clamp(mainLevel, 1, MainMaxHpByLevel.Length) - 1;
            return MainMaxHpByLevel[index];
        }

        public static float GetBarracksMaxHp(int barracksLevel)
        {
            var index = System.Math.Clamp(barracksLevel, 1, BarracksMaxHpByLevel.Length) - 1;
            return BarracksMaxHpByLevel[index];
        }

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

        public static float GetSurfaceDistance(float centerDistance, string buildingId) =>
            System.Math.Max(0f, centerDistance - GetEngageRadius(buildingId));
    }
}
