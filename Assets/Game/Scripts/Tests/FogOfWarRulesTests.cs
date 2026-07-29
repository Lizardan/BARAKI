using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Fog;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class FogOfWarRulesTests
    {
        const float VisionRadius = 12f;

        [Test]
        public void PermanentBake_N4_LocalBase_IsRevealed_EnemyBase_IsNot()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var zones = FogPermanentMaskBaker.Bake(layout, graph, localPlayerSlot: 0);

            var ownMain = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.Main);
            var enemyMain = layout.Slots[2].GetBuildingWorldPosition(GameIds.Buildings.Main);

            Assert.IsTrue(zones.Contains(ownMain));
            Assert.IsFalse(zones.Contains(enemyMain));
        }

        [Test]
        public void PermanentBake_N4_CenterSpoke_Revealed_UntilArena_NotInsideArena()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var zones = FogPermanentMaskBaker.Bake(layout, graph, localPlayerSlot: 0);

            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Center, out var center));
            var start = center.Path.Start;
            var midSpoke = Vector3.Lerp(start, Vector3.zero, 0.35f);
            midSpoke.y = 0f;

            Assert.IsTrue(
                zones.Contains(midSpoke),
                "Own center spoke before arena entry should be permanently clear.");
            Assert.IsFalse(
                zones.Contains(Vector3.zero),
                "Center arena itself should not be permanent clear.");
        }

        [Test]
        public void PermanentBake_N4_Flank_Revealed_BeforeTurn_NotPastCorner()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var zones = FogPermanentMaskBaker.Bake(layout, graph, localPlayerSlot: 0);

            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Left, out var left));
            var poly = FogPermanentMaskBaker.TruncateFlankToFirstTurn(left.Path);
            Assert.GreaterOrEqual(poly.Count, 2);

            var nearStart = Vector3.Lerp(poly[0], poly[1], 0.5f);
            Assert.IsTrue(zones.Contains(nearStart), "Flank near barracks should be clear.");

            // Point well past the permanent flank tip along the full ring.
            var tip = poly[^1];
            var far = left.Path.GetWaypoint(Mathf.Min(left.Path.WaypointCount - 1, poly.Count + 8));
            far.y = 0f;
            // Ensure we didn't accidentally pick a point still near the tip.
            if (Vector3.Distance(far, tip) < zones.RoadHalfWidth * 2f)
            {
                far = tip + (tip - poly[0]).normalized * (zones.RoadHalfWidth * 4f);
                far.y = 0f;
            }

            Assert.IsFalse(
                zones.Contains(far),
                "Flank past the first corner turn should not be permanent clear.");
        }

        [Test]
        public void DynamicVision_RevealsNearUnit_AndClosesWhenUnitGone()
        {
            var pos = new Vector3(10f, 0f, 10f);
            var units = new List<Vector3> { pos };

            Assert.IsTrue(FogVisionRules.IsDynamicallyRevealed(units, VisionRadius, pos));
            Assert.IsTrue(
                FogVisionRules.IsDynamicallyRevealed(units, VisionRadius, pos + new Vector3(5f, 0f, 0f)));
            Assert.IsFalse(
                FogVisionRules.IsDynamicallyRevealed(units, VisionRadius, pos + new Vector3(40f, 0f, 0f)));

            units.Clear();
            Assert.IsFalse(FogVisionRules.IsDynamicallyRevealed(units, VisionRadius, pos));
        }

        [Test]
        public void IsRevealed_PermanentOverridesEmptyUnits()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var zones = FogPermanentMaskBaker.Bake(layout, graph, localPlayerSlot: 0);
            var ownMain = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.Main);

            Assert.IsTrue(
                FogVisionRules.IsRevealed(zones, new List<Vector3>(), VisionRadius, ownMain, fogDisabled: false));
        }

        [Test]
        public void IsRevealed_Spectator_AlwaysTrue()
        {
            Assert.IsTrue(
                FogVisionRules.IsRevealed(
                    permanent: null,
                    localLivingUnitPositions: null,
                    visionRadius: VisionRadius,
                    worldPosition: new Vector3(99f, 0f, 99f),
                    fogDisabled: true));
        }

        [Test]
        public void CanSelectTarget_BlocksEnemyInFog_AllowsOwnAndRevealedEnemy()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var zones = FogPermanentMaskBaker.Bake(layout, graph, localPlayerSlot: 0);
            var empty = new List<Vector3>();

            var ownMain = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.Main);
            var enemyMain = layout.Slots[2].GetBuildingWorldPosition(GameIds.Buildings.Main);

            Assert.IsTrue(
                FogVisionRules.CanSelectTarget(0, 0, ownMain, zones, empty, VisionRadius, fogDisabled: false));
            Assert.IsFalse(
                FogVisionRules.CanSelectTarget(0, 2, enemyMain, zones, empty, VisionRadius, fogDisabled: false));

            var unitsNearEnemy = new List<Vector3> { enemyMain };
            Assert.IsTrue(
                FogVisionRules.CanSelectTarget(
                    0, 2, enemyMain, zones, unitsNearEnemy, VisionRadius, fogDisabled: false));
        }

        [Test]
        public void TruncateCenter_StopsAtArenaRadius()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            Assert.IsTrue(graph.TryGetLane(0, GameIds.Lanes.Center, out var center));

            var poly = FogPermanentMaskBaker.TruncateCenterToArenaEntry(
                center.Path,
                LaneGraphBuilder.DefaultCenterArenaRadius);

            Assert.GreaterOrEqual(poly.Count, 2);
            var end = poly[^1];
            var endRadius = new Vector2(end.x, end.z).magnitude;
            Assert.AreEqual(
                LaneGraphBuilder.DefaultCenterArenaRadius,
                endRadius,
                0.75f,
                "Center permanent polyline should end near arena entry.");
        }

        [Test]
        public void ShouldShowUnitOnMinimap_HidesEnemyInFog_ShowsOwnAndRevealed()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var graph = LaneGraphBuilder.Build(layout);
            var zones = FogPermanentMaskBaker.Bake(layout, graph, localPlayerSlot: 0);
            var empty = new List<Vector3>();
            var enemyMain = layout.Slots[2].GetBuildingWorldPosition(GameIds.Buildings.Main);
            var ownMain = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.Main);

            Assert.IsTrue(
                FogVisionRules.ShouldShowUnitOnMinimap(0, 0, ownMain, zones, empty, 25f, fogDisabled: false));
            Assert.IsFalse(
                FogVisionRules.ShouldShowUnitOnMinimap(0, 2, enemyMain, zones, empty, 25f, fogDisabled: false));
            Assert.IsTrue(
                FogVisionRules.ShouldShowUnitOnMinimap(
                    0, 2, enemyMain, zones, new List<Vector3> { enemyMain }, 25f, fogDisabled: false));
            Assert.IsTrue(
                FogVisionRules.ShouldShowUnitOnMinimap(0, 2, enemyMain, zones, empty, 25f, fogDisabled: true));
        }
    }
}
