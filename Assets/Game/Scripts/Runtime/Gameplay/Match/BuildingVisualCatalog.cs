using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Building prefabs keyed by building id for greybox base population.</summary>
    [CreateAssetMenu(fileName = "BuildingVisualCatalog", menuName = "Game/Building Visual Catalog")]
    public sealed class BuildingVisualCatalog : ScriptableObject
    {
        [SerializeField] private GameObject _main;
        [SerializeField] private GameObject _tower;
        [SerializeField] private GameObject _barracks;

        public bool TryGetPrefab(string buildingId, out GameObject prefab)
        {
            prefab = GetPrefab(buildingId);
            return prefab != null;
        }

        GameObject GetPrefab(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                return null;
            }

            if (buildingId == GameIds.Buildings.Main)
            {
                return _main;
            }

            if (buildingId.StartsWith("BUILDING_TOWER"))
            {
                return _tower;
            }

            if (buildingId.StartsWith("BUILDING_BARRACKS"))
            {
                return _barracks;
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorAssign(GameObject main, GameObject tower, GameObject barracks)
        {
            _main = main;
            _tower = tower;
            _barracks = barracks;
        }
#endif
    }
}
