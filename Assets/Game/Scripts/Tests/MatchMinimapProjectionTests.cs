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

        [Test]
        public void PanelToWorld_CenterMapsToOrigin()
        {
            const float radius = 120f;
            var world = MatchMinimapProjection.PanelToWorld(
                new Vector2(175f, 175f),
                350f,
                350f,
                radius);

            Assert.AreEqual(0f, world.x, 0.001f);
            Assert.AreEqual(0f, world.y, 0.001f);
            Assert.AreEqual(0f, world.z, 0.001f);
        }

        [Test]
        public void PanelToWorld_RoundTripsThroughNormalizedToPanel()
        {
            const float radius = 120f;
            var original = new Vector3(40f, 0f, -25f);
            var normalized = MatchMinimapProjection.WorldToNormalized(original, radius);
            var panel = MatchMinimapProjection.NormalizedToPanel(normalized, 350f, 350f);
            var back = MatchMinimapProjection.PanelToWorld(panel, 350f, 350f, radius);

            Assert.AreEqual(original.x, back.x, 0.05f);
            Assert.AreEqual(original.z, back.z, 0.05f);
        }

        [Test]
        public void PanelToWorld_ArenaCornerMapsNearEdge()
        {
            const float radius = 120f;
            var panel = MatchMinimapProjection.NormalizedToPanel(new Vector2(1f, 0f), 350f, 350f);
            var world = MatchMinimapProjection.PanelToWorld(panel, 350f, 350f, radius);

            Assert.AreEqual(radius, world.x, 0.05f);
            Assert.AreEqual(radius, world.z, 0.05f);
        }

        [Test]
        public void WorldToNormalizedUnclamped_AllowsOutsideArena()
        {
            const float radius = 120f;
            var outside = MatchMinimapProjection.WorldToNormalizedUnclamped(new Vector3(0f, 0f, 240f), radius);
            var clamped = MatchMinimapProjection.WorldToNormalized(new Vector3(0f, 0f, 240f), radius);

            Assert.Less(outside.y, 0f);
            Assert.AreEqual(0f, clamped.y, 0.001f);
        }
    }
}
