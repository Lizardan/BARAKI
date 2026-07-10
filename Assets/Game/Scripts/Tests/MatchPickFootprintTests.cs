using Game.Core;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchPickFootprintTests
    {
        [Test]
        public void GetBuildingDiameter_MainLargerThanTower()
        {
            var mainDiameter = MatchPickFootprint.GetBuildingDiameter(GameIds.Buildings.Main);
            var towerDiameter = MatchPickFootprint.GetBuildingDiameter(GameIds.Buildings.TowerNw);

            Assert.Greater(mainDiameter, towerDiameter);
        }

        [Test]
        public void GetBuildingDiameter_AppliesMargin()
        {
            var size = MatchPickFootprint.GetBuildingPickSize(GameIds.Buildings.TowerNw);
            var expected = UnityEngine.Mathf.Max(size.x, size.z) * 1.05f;

            Assert.AreEqual(expected, MatchPickFootprint.GetBuildingDiameter(GameIds.Buildings.TowerNw, 1.05f), 0.001f);
        }

        [Test]
        public void GetModelFootprintDiameter_AppliesMarginToRendererBounds()
        {
            var root = new GameObject("FootprintTestRoot");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(2f, 2f, 3f);

            try
            {
                var diameter = MatchPickFootprint.GetModelFootprintDiameter(root.transform, 1.1f);
                Assert.AreEqual(3.3f, diameter, 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
