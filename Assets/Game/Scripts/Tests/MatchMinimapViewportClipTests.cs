using System.Collections.Generic;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchMinimapViewportClipTests
    {
        [Test]
        public void Clip_FullyInside_KeepsQuad()
        {
            var output = new List<Vector2>();
            var scratch = new List<Vector2>();
            var clipped = MatchMinimapViewportClip.TryClipToUnitSquare(
                new Vector2(0.2f, 0.2f),
                new Vector2(0.8f, 0.2f),
                new Vector2(0.8f, 0.8f),
                new Vector2(0.2f, 0.8f),
                output,
                scratch);

            Assert.IsTrue(clipped);
            Assert.AreEqual(4, output.Count);
            AssertInsideUnitSquare(output);
        }

        [Test]
        public void Clip_FullyOutside_ReturnsEmpty()
        {
            var output = new List<Vector2>();
            var scratch = new List<Vector2>();
            var clipped = MatchMinimapViewportClip.TryClipToUnitSquare(
                new Vector2(1.2f, 0.2f),
                new Vector2(1.5f, 0.2f),
                new Vector2(1.5f, 0.8f),
                new Vector2(1.2f, 0.8f),
                output,
                scratch);

            Assert.IsFalse(clipped);
            Assert.Less(output.Count, 3);
        }

        [Test]
        public void Clip_StraddlingRightEdge_ClosesAlongMapBorder()
        {
            var output = new List<Vector2>();
            var scratch = new List<Vector2>();
            var clipped = MatchMinimapViewportClip.TryClipToUnitSquare(
                new Vector2(0.5f, 0.3f),
                new Vector2(1.5f, 0.3f),
                new Vector2(1.5f, 0.7f),
                new Vector2(0.5f, 0.7f),
                output,
                scratch);

            Assert.IsTrue(clipped);
            AssertInsideUnitSquare(output);
            Assert.IsTrue(HasVertexOnEdge(output, x: 1f));
        }

        [Test]
        public void Clip_FarEdgePastNorth_ClosesAlongTopBorder()
        {
            var output = new List<Vector2>();
            var scratch = new List<Vector2>();
            var clipped = MatchMinimapViewportClip.TryClipToUnitSquare(
                new Vector2(0.4f, 0.6f),
                new Vector2(0.6f, 0.6f),
                new Vector2(0.8f, -0.4f),
                new Vector2(0.2f, -0.4f),
                output,
                scratch);

            Assert.IsTrue(clipped);
            AssertInsideUnitSquare(output);
            Assert.IsTrue(HasVertexOnEdge(output, y: 0f));
            Assert.GreaterOrEqual(CountOnEdge(output, y: 0f), 2);
        }

        static void AssertInsideUnitSquare(List<Vector2> polygon)
        {
            foreach (var point in polygon)
            {
                Assert.GreaterOrEqual(point.x, -0.0001f);
                Assert.LessOrEqual(point.x, 1.0001f);
                Assert.GreaterOrEqual(point.y, -0.0001f);
                Assert.LessOrEqual(point.y, 1.0001f);
            }
        }

        static bool HasVertexOnEdge(List<Vector2> polygon, float? x = null, float? y = null)
        {
            return CountOnEdge(polygon, x, y) > 0;
        }

        static int CountOnEdge(List<Vector2> polygon, float? x = null, float? y = null)
        {
            var count = 0;
            foreach (var point in polygon)
            {
                var onX = !x.HasValue || Mathf.Abs(point.x - x.Value) < 0.001f;
                var onY = !y.HasValue || Mathf.Abs(point.y - y.Value) < 0.001f;
                if (onX && onY)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
