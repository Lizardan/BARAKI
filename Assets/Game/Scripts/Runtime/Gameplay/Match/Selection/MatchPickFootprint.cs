using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchPickFootprint
    {
        public const float PickSizeMargin = 1.15f;
        public const float RingMargin = 1.1f;
        public const float DefaultUnitDiameter = 1.8f;

        public static Vector3 GetBuildingPickSize(string buildingId) => buildingId switch
        {
            GameIds.Buildings.Main => new Vector3(8.4f, 9.6f, 8.2f),
            GameIds.Buildings.TowerNw or GameIds.Buildings.TowerNe or GameIds.Buildings.TowerSw or GameIds.Buildings.TowerSe
                => new Vector3(4f, 8.3f, 4f),
            GameIds.Buildings.BarracksCenter
                or GameIds.Buildings.BarracksLeft
                or GameIds.Buildings.BarracksRight => new Vector3(6.6f, 5.4f, 6.6f),
            _ => Vector3.one * 2f,
        };

        public static float GetBuildingDiameter(string buildingId, float margin = RingMargin)
        {
            var size = GetBuildingPickSize(buildingId);
            return Mathf.Max(size.x, size.z) * margin;
        }

        public static float GetModelFootprintDiameter(Transform modelRoot, float margin = RingMargin)
        {
            if (modelRoot == null)
            {
                return DefaultUnitDiameter * margin;
            }

            var renderers = modelRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return DefaultUnitDiameter * margin;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Max(bounds.size.x, bounds.size.z) * margin;
        }
    }
}
