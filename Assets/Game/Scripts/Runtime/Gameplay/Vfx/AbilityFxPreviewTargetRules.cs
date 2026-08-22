using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;

namespace Game.Gameplay.Vfx
{
    /// <summary>Dummy on the right of BARAKI Studio preview (what the ability hits).</summary>
    public readonly struct AbilityFxPreviewTarget
    {
        public AbilityFxPreviewTarget(string displayName, bool isBuilding, string buildingId = "")
        {
            DisplayName = displayName;
            IsBuilding = isBuilding;
            BuildingId = buildingId ?? "";
        }

        public string DisplayName { get; }
        public bool IsBuilding { get; }
        public string BuildingId { get; }
    }

    /// <summary>Maps ability ids to the example target spawned opposite the caster.</summary>
    public static class AbilityFxPreviewTargetRules
    {
        /// <summary>Delay before the barracks collapses so the beam is visible on the intact building.</summary>
        public const float BuildingSmiteRuinDelaySeconds = 0.25f;

        /// <summary>How long Studio keeps burning ruins before looping the preview.</summary>
        public const float BuildingSmiteRuinsHoldSeconds = 2f;

        public static AbilityFxPreviewTarget Resolve(int abilityId) => abilityId switch
        {
            AbilityIds.MainBuildingSmite =>
                new("Барак", isBuilding: true, GameIds.Buildings.Barracks),
            AbilityIds.MainUnitSmite =>
                new("Мечник", isBuilding: false),
            _ => new("Цель", isBuilding: false),
        };

        /// <summary>Divine Blessing has no caster model — one target in the middle of the preview.</summary>
        public static bool UsesSoloTarget(int abilityId) =>
            abilityId is AbilityIds.MainBuildingSmite or AbilityIds.MainUnitSmite;

        /// <summary>Кара зданий: collapse to burning ruins like the match presenter.</summary>
        public static bool PreviewBuildingCollapse(int abilityId) =>
            abilityId == AbilityIds.MainBuildingSmite;
    }
}
