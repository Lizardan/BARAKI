using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BuildingRuinsVisualTests
    {
        [Test]
        public void ApplyRuins_ShowsMeshFoundation_HidesModel()
        {
            var root = new GameObject("BUILDING_BARRACKS_CENTER");
            try
            {
                var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
                foundation.name = BuildingRuinsVisual.FoundationName;
                foundation.transform.SetParent(root.transform, false);
                foundation.SetActive(false);

                var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = BuildingRuinsVisual.ModelName;
                model.transform.SetParent(root.transform, false);

                BuildingRuinsVisual.ApplyRuins(root.transform, GameIds.Buildings.BarracksCenter);

                Assert.IsTrue(foundation.activeSelf);
                Assert.IsFalse(model.activeSelf);
                Assert.IsTrue(root.activeSelf);

                BuildingRuinsVisual.RestoreIntact(root.transform);
                Assert.IsFalse(foundation.activeSelf);
                Assert.IsTrue(model.activeSelf);
                Assert.IsTrue(root.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRuins_RemovesLegacyCylinderPad()
        {
            var root = new GameObject("BUILDING_MAIN");
            try
            {
                var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = BuildingRuinsVisual.FoundationName;
                cylinder.transform.SetParent(root.transform, false);

                var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = BuildingRuinsVisual.ModelName;
                model.transform.SetParent(root.transform, false);

                BuildingRuinsVisual.ApplyRuins(root.transform, GameIds.Buildings.Main);

                Assert.IsFalse(model.activeSelf);
                Assert.IsNull(root.transform.Find(BuildingRuinsVisual.FoundationName));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
