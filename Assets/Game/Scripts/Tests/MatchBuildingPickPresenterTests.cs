using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchBuildingPickPresenterTests
    {
        [Test]
        public void RefreshBuildingPicks_CreatesWorldProxiesForAllBuildings()
        {
            MatchPickLayers.InitializeFromName();

            var root = new GameObject("MatchBuildingPickTest");
            try
            {
                var runtime = root.AddComponent<MatchRuntime>();
                root.AddComponent<MatchSelectionBridge>();
                var presenter = root.AddComponent<MatchBuildingPickPresenter>();

                var raceIds = new List<string>
                {
                    GameIds.Races.Human,
                    GameIds.Races.Bug,
                    GameIds.Races.Slot3,
                    GameIds.Races.Slot4,
                };
                runtime.StartMatch(raceIds, localPlayerSlot: 0);
                presenter.RefreshBuildingPicks();

                var pickRoot = root.transform.Find("MatchBuildingPickRoot");
                Assert.IsNotNull(pickRoot, "Pick root should be created.");
                Assert.AreEqual(32, pickRoot.childCount, "N=4 should create 32 building pick proxies.");

                var firstProxy = pickRoot.GetChild(0);
                var pickLayer = LayerMask.NameToLayer(MatchPickLayers.PickableLayerName);
                Assert.GreaterOrEqual(pickLayer, 0);
                Assert.AreEqual(pickLayer, firstProxy.gameObject.layer);

                var collider = firstProxy.GetComponent<Collider>();
                Assert.IsNotNull(collider);
                Assert.IsTrue(collider.isTrigger);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
