using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Building prefabs keyed by building id (and optionally race) for greybox base population.
    /// Races without a set — or with a missing slot — fall back to the default (Human) set.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingVisualCatalog", menuName = "Game/Building Visual Catalog")]
    public sealed class BuildingVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class RaceBuildingSet
        {
            public string RaceId;
            public GameObject Main;
            public GameObject Tower;
            public GameObject Barracks;
        }

        [SerializeField] private GameObject _main;
        [SerializeField] private GameObject _tower;
        [SerializeField] private GameObject _barracks;
        [SerializeField] private List<RaceBuildingSet> _raceSets = new();

        public bool TryGetPrefab(string buildingId, out GameObject prefab) =>
            TryGetPrefab(buildingId, null, out prefab);

        public bool TryGetPrefab(string buildingId, string raceId, out GameObject prefab)
        {
            prefab = GetPrefab(buildingId, raceId);
            return prefab != null;
        }

        GameObject GetPrefab(string buildingId, string raceId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(raceId))
            {
                var set = GetRaceSet(raceId);
                if (set != null && TryGetFromSet(buildingId, set.Main, set.Tower, set.Barracks, out var racePrefab))
                {
                    return racePrefab;
                }
            }

            return TryGetFromSet(buildingId, _main, _tower, _barracks, out var prefab) ? prefab : null;
        }

        static bool TryGetFromSet(string buildingId, GameObject main, GameObject tower, GameObject barracks, out GameObject prefab)
        {
            if (buildingId == GameIds.Buildings.Main)
            {
                prefab = main;
                return prefab != null;
            }

            if (buildingId.StartsWith("BUILDING_TOWER"))
            {
                prefab = tower;
                return prefab != null;
            }

            if (buildingId.StartsWith("BUILDING_BARRACKS"))
            {
                prefab = barracks;
                return prefab != null;
            }

            prefab = null;
            return false;
        }

        RaceBuildingSet GetRaceSet(string raceId)
        {
            for (var i = 0; i < _raceSets.Count; i++)
            {
                if (_raceSets[i] != null && _raceSets[i].RaceId == raceId)
                {
                    return _raceSets[i];
                }
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

        public void EditorAssignRaceSet(string raceId, GameObject main, GameObject tower, GameObject barracks)
        {
            var set = GetRaceSet(raceId);
            if (set == null)
            {
                set = new RaceBuildingSet { RaceId = raceId };
                _raceSets.Add(set);
            }

            set.Main = main;
            set.Tower = tower;
            set.Barracks = barracks;
        }
#endif
    }
}
