using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SelectionRingMeshBuilderTests
    {
        [Test]
        public void BuildAnnulus_HasInnerHole()
        {
            var mesh = SelectionRingMeshBuilder.BuildAnnulus(2f, 0.2f, 8);

            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
            Assert.Greater(mesh.triangles.Length, 0);

            var minRadius = float.MaxValue;
            var maxRadius = 0f;
            foreach (var vertex in mesh.vertices)
            {
                var radius = new Vector2(vertex.x, vertex.z).magnitude;
                minRadius = Mathf.Min(minRadius, radius);
                maxRadius = Mathf.Max(maxRadius, radius);
            }

            Assert.Less(minRadius, maxRadius);
            Assert.Less(minRadius, 1.9f);
            Assert.AreEqual(2f, maxRadius, 0.001f);
        }
    }
}
