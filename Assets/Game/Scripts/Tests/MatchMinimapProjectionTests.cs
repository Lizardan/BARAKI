using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchMinimapProjectionTests
    {
        [Test]
        public void WorldToNormalized_MapsArenaCorners()
        {
            const float radius = 120f;
            var north = MatchMinimapProjection.WorldToNormalized(new Vector3(0f, 0f, radius), radius);
            var south = MatchMinimapProjection.WorldToNormalized(new Vector3(0f, 0f, -radius), radius);

            Assert.AreEqual(0.5f, north.x, 0.001f);
            Assert.AreEqual(0f, north.y, 0.001f);
            Assert.AreEqual(1f, south.y, 0.001f);
        }

        [Test]
        public void NormalizedToPanel_ScalesToPanelSize()
        {
            var panel = MatchMinimapProjection.NormalizedToPanel(new Vector2(0.5f, 0.5f), 350f, 350f);
            Assert.AreEqual(175f, panel.x, 0.001f);
            Assert.AreEqual(175f, panel.y, 0.001f);
        }

        [Test]
        public void NormalizedToPanel_InsetKeepsCornersInsidePanel()
        {
            var corner = MatchMinimapProjection.NormalizedToPanel(new Vector2(1f, 0f), 350f, 350f);
            Assert.Less(corner.x, 350f);
            Assert.Greater(corner.x, 300f);
            Assert.Less(corner.y, 350f);
            Assert.Greater(corner.y, 0f);
            Assert.AreEqual(336f, corner.x, 0.001f);
            Assert.AreEqual(14f, corner.y, 0.001f);
        }
    }
}
