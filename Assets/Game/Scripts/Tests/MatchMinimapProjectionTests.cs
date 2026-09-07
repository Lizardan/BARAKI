using Game.Core;
using Game.Gameplay.Match;
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
        public void NormalizedToPanel_CornerMapsToPanelBoundary()
        {
            var corner = MatchMinimapProjection.NormalizedToPanel(new Vector2(1f, 0f), 350f, 350f);
            Assert.AreEqual(350f, corner.x, 0.001f);
            Assert.AreEqual(0f, corner.y, 0.001f);
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

        [Test]
        public void WorldToNormalized_WithYaw90_MapsNegXToBottom()
        {
            const float radius = 120f;
            var west = MatchMinimapProjection.WorldToNormalized(
                new Vector3(-radius, 0f, 0f),
                radius,
                viewYawDegrees: 90f);

            Assert.AreEqual(0.5f, west.x, 0.001f);
            Assert.AreEqual(1f, west.y, 0.001f);
        }

        [Test]
        public void PanelToWorld_WithYaw_RoundTrips()
        {
            const float radius = 120f;
            const float yaw = -90f;
            var original = new Vector3(40f, 0f, -25f);
            var normalized = MatchMinimapProjection.WorldToNormalized(original, radius, yaw);
            var panel = MatchMinimapProjection.NormalizedToPanel(normalized, 350f, 350f);
            var back = MatchMinimapProjection.PanelToWorld(panel, 350f, 350f, radius, yaw);

            Assert.AreEqual(original.x, back.x, 0.05f);
            Assert.AreEqual(original.z, back.z, 0.05f);
        }

        [Test]
        public void BlipRotateDegrees_MatchesBaseYawWhenViewIsZero()
        {
            var layout = MatchArenaGenerator.Generate(3);
            var yaw = MatchMinimapProjection.YawDegrees(layout.Slots[1].BaseRotation);
            Assert.AreEqual(yaw, MatchMinimapProjection.BlipRotateDegrees(yaw, 0f), 0.01f);
            Assert.AreEqual(yaw - 90f, MatchMinimapProjection.BlipRotateDegrees(yaw, 90f), 0.01f);
        }

        [Test]
        public void MapHalfExtent_KeepsEveryBasePadOnMap_ForAllModesAndYaw()
        {
            for (var players = 2; players <= MatchModeRules.MaxPlayers; players++)
            {
                var layout = MatchArenaGenerator.Generate(players);
                var graph = LaneGraphBuilder.Build(layout);
                var topology = MatchMinimapTopologyBuilder.Build(layout, graph);
                var halfExtent = MatchMinimapProjection.MapHalfExtent(layout.ArenaRadius);

                for (var yaw = 0f; yaw < 360f; yaw += 15f)
                {
                    foreach (var rect in topology.FilledRects)
                    {
                        for (var cornerIndex = 0; cornerIndex < 4; cornerIndex++)
                        {
                            var corner = GetRectCorner(rect, cornerIndex);
                            var projected = MatchMinimapProjection.WorldToNormalized(
                                new Vector3(corner.x, 0f, corner.y),
                                halfExtent,
                                yaw);
                            Assert.IsTrue(
                                projected.x >= -0.0001f && projected.x <= 1.0001f,
                                $"P{players} slot {rect.OwnerSlot} corner {cornerIndex} yaw {yaw}: x {projected.x} outside map");
                            Assert.IsTrue(
                                projected.y >= -0.0001f && projected.y <= 1.0001f,
                                $"P{players} slot {rect.OwnerSlot} corner {cornerIndex} yaw {yaw}: y {projected.y} outside map");
                        }
                    }
                }
            }
        }

        static Vector2 GetRectCorner(MatchMinimapRect rect, int index) =>
            rect.GetWorldCorner(index);
    }
}
