using System.Collections.Generic;
using System.Reflection;
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
        public void RefreshBuildingPicks_WithoutVisuals_CreatesFallbackProxies()
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
                    GameIds.Races.Human,
                    GameIds.Races.Slot3,
                    GameIds.Races.Slot4,
                };
                runtime.StartMatch(raceIds, localPlayerSlot: 0);
                presenter.RefreshBuildingPicks();

                var pickRoot = root.transform.Find("MatchBuildingPickRoot");
                Assert.IsNotNull(pickRoot, "Pick root should be created.");
                Assert.AreEqual(32, pickRoot.childCount, "N=4 without visuals should create 32 fallback proxies.");

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

        [Test]
        public void RefreshBuildingPicks_WithVisualMesh_AttachesMeshCollider()
        {
            MatchPickLayers.InitializeFromName();

            var root = new GameObject("MatchBuildingPickMeshTest");
            var greyboxGo = new GameObject("ArenaGreybox");
            greyboxGo.SetActive(false);
            try
            {
                var runtime = root.AddComponent<MatchRuntime>();
                var bridge = root.AddComponent<MatchSelectionBridge>();
                var presenter = root.AddComponent<MatchBuildingPickPresenter>();

                runtime.StartMatch(
                    new List<string>
                    {
                        GameIds.Races.Human,
                        GameIds.Races.Human,
                        GameIds.Races.Slot3,
                        GameIds.Races.Slot4,
                    },
                    localPlayerSlot: 0);

                var building = runtime.Controller.Buildings.Buildings[0];

                var greybox = greyboxGo.AddComponent<MatchArenaGreybox>();
                SetPrivateField(greybox, "_buildOnAwake", false);

                var visualRoot = new GameObject("GreyboxVisual");
                visualRoot.transform.SetParent(greyboxGo.transform, false);
                var slotRoot = new GameObject($"Player_{building.OwnerSlot}");
                slotRoot.transform.SetParent(visualRoot.transform, false);
                var buildingVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                buildingVisual.name = building.BuildingId;
                buildingVisual.transform.SetParent(slotRoot.transform, false);
                Object.DestroyImmediate(buildingVisual.GetComponent<Collider>());

                greyboxGo.SetActive(true);

                presenter.RefreshBuildingPicks();

                var meshFilter = buildingVisual.GetComponent<MeshFilter>();
                Assert.IsNotNull(meshFilter);
                var meshCollider = buildingVisual.GetComponent<MeshCollider>();
                Assert.IsNotNull(meshCollider, "Visual mesh should receive a MeshCollider pick.");
                Assert.IsFalse(meshCollider.isTrigger);
                Assert.AreEqual(meshFilter.sharedMesh, meshCollider.sharedMesh);

                var pickLayer = LayerMask.NameToLayer(MatchPickLayers.PickableLayerName);
                Assert.AreEqual(pickLayer, buildingVisual.layer);
                Assert.IsTrue(bridge.Registry.TryResolve(meshCollider, out var target));
                Assert.IsTrue(target.IsBuilding);
                Assert.AreEqual(building.InstanceId, target.EntityId);

                var pickRoot = root.transform.Find("MatchBuildingPickRoot");
                Assert.IsNotNull(pickRoot);
                Assert.AreEqual(
                    0,
                    CountOwnedFallbackForBuilding(pickRoot, building),
                    "Mesh pick should not create a fallback proxy for that building.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(greyboxGo);
            }
        }

        [Test]
        public void EnsureMeshPickCollider_UsesSharedMeshAndPickableLayer()
        {
            MatchPickLayers.InitializeFromName();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                var collider = MatchPickColliderUtility.EnsureMeshPickCollider(cube, mesh);
                Assert.IsNotNull(collider);
                Assert.IsInstanceOf<MeshCollider>(collider);
                Assert.AreEqual(mesh, ((MeshCollider)collider).sharedMesh);
                Assert.IsFalse(collider.isTrigger);
                Assert.AreEqual(LayerMask.NameToLayer(MatchPickLayers.PickableLayerName), cube.layer);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        static int CountOwnedFallbackForBuilding(Transform pickRoot, BuildingState building)
        {
            var expected = $"Pick_{building.BuildingId}_{building.InstanceId}";
            var count = 0;
            for (var i = 0; i < pickRoot.childCount; i++)
            {
                if (pickRoot.GetChild(i).name == expected)
                {
                    count++;
                }
            }

            return count;
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }
    }
}
