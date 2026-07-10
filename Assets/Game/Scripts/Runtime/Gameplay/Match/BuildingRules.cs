using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>GDD building HP/armor and siege lane targeting.</summary>
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

        public static string GetLaneBinding(string buildingId) => buildingId switch
        {
            GameIds.Buildings.BarracksLeft => GameIds.Lanes.Left,
            GameIds.Buildings.BarracksCenter => GameIds.Lanes.Center,
            GameIds.Buildings.BarracksRight => GameIds.Lanes.Right,
            _ => string.Empty,
        };

        public static bool CanSiegeTarget(string unitLaneId, string buildingId)
        {
            var binding = GetLaneBinding(buildingId);
            if (!string.IsNullOrEmpty(binding))
            {
                return binding == unitLaneId;
            }

            return unitLaneId == GameIds.Lanes.Center;
        }
    }
}
