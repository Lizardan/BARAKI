using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BuildingVisualCatalogRaceTests
    {
        BuildingVisualCatalog _catalog;
        GameObject _humanMain;
        GameObject _humanTower;
        GameObject _humanBarracks;
        GameObject _facelessMain;
        GameObject _facelessTower;
        GameObject _facelessBarracks;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<BuildingVisualCatalog>();
            _humanMain = new GameObject("Human_Main");
            _humanTower = new GameObject("Human_Tower");
            _humanBarracks = new GameObject("Human_Barracks");
            _facelessMain = new GameObject("Faceless_Main");
            _facelessTower = new GameObject("Faceless_Tower");
            _facelessBarracks = new GameObject("Faceless_Barracks");
            _catalog.EditorAssign(_humanMain, _humanTower, _humanBarracks);
            _catalog.EditorAssignRaceSet(GameIds.Races.Faceless, _facelessMain, _facelessTower, _facelessBarracks);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_humanMain);
            Object.DestroyImmediate(_humanTower);
            Object.DestroyImmediate(_humanBarracks);
            Object.DestroyImmediate(_facelessMain);
            Object.DestroyImmediate(_facelessTower);
            Object.DestroyImmediate(_facelessBarracks);
        }

        [Test]
        public void TryGetPrefab_RaceSet_ReturnsRacePrefabs()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.Main, GameIds.Races.Faceless, out var main));
            Assert.AreEqual(_facelessMain, main);
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.TowerSe, GameIds.Races.Faceless, out var tower));
            Assert.AreEqual(_facelessTower, tower);
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.BarracksLeft, GameIds.Races.Faceless, out var barracks));
            Assert.AreEqual(_facelessBarracks, barracks);
        }

        [Test]
        public void TryGetPrefab_NullRace_FallsBackToDefaultSet()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.Main, null, out var main));
            Assert.AreEqual(_humanMain, main);
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.TowerNw, null, out var tower));
            Assert.AreEqual(_humanTower, tower);
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.BarracksCenter, null, out var barracks));
            Assert.AreEqual(_humanBarracks, barracks);
        }

        [Test]
        public void TryGetPrefab_UnknownRace_FallsBackToDefaultSet()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.Main, "RACE_UNKNOWN", out var main));
            Assert.AreEqual(_humanMain, main);
        }

        [Test]
        public void TryGetPrefab_RaceSetMissingSlot_FallsBackToDefaultSlot()
        {
            _catalog.EditorAssignRaceSet(GameIds.Races.Faceless, _facelessMain, _facelessTower, null);
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.BarracksRight, GameIds.Races.Faceless, out var barracks));
            Assert.AreEqual(_humanBarracks, barracks);
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Buildings.Main, GameIds.Races.Faceless, out var main));
            Assert.AreEqual(_facelessMain, main);
        }

        [Test]
        public void TryGetPrefab_UnknownBuildingId_ReturnsFalse()
        {
            Assert.IsFalse(_catalog.TryGetPrefab("BUILDING_UNKNOWN", GameIds.Races.Faceless, out _));
            Assert.IsFalse(_catalog.TryGetPrefab("BUILDING_UNKNOWN", null, out _));
        }

        [Test]
        public void Populate_RaceIds_UsesRacePrefabPerSlot()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var graph = LaneGraphBuilder.Build(layout, LaneGraphBuilder.DefaultCenterArenaRadius);
            var catalog = ScriptableObject.CreateInstance<BuildingVisualCatalog>();
            catalog.EditorAssign(_humanMain, _humanTower, _humanBarracks);
            catalog.EditorAssignRaceSet(GameIds.Races.Faceless, _facelessMain, _facelessTower, _facelessBarracks);
            AddMarker(_facelessMain, "FacelessMarker");
            AddMarker(_humanMain, "HumanMarker");
            var raceIds = new[] { GameIds.Races.Faceless, GameIds.Races.Human };
            var root = new GameObject("GreyboxRoot");
            try
            {
                MatchArenaGreyboxBuilder.Populate(root.transform, layout, graph, raceIds, catalog);

                var slot0 = root.transform.Find($"Bases/Player_0/{GameIds.Buildings.Main}");
                var slot1 = root.transform.Find($"Bases/Player_1/{GameIds.Buildings.Main}");
                Assert.IsNotNull(slot0, "Player_0 main missing");
                Assert.IsNotNull(slot1, "Player_1 main missing");
                Assert.IsNotNull(slot0.Find("FacelessMarker"), "Player_0 must use Faceless prefab");
                Assert.IsNotNull(slot1.Find("HumanMarker"), "Player_1 must use Human prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(catalog);
            }
        }

        static void AddMarker(GameObject prefab, string markerName)
        {
            var marker = new GameObject(markerName);
            marker.transform.SetParent(prefab.transform, false);
        }
    }
}
