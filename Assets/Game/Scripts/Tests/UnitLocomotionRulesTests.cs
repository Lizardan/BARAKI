using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitLocomotionRulesTests
    {
        [Test]
        public void MoveTowards_AllyAvoidance_SeparatesUnits()
        {
            var stats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 1f,
                attackRange: 1.5f,
                moveSpeed: 4f,
                goldBounty: 1);

            var ally = new MatchUnitState(1, 0, Game.Core.GameIds.Lanes.Center, UnitRole.Melee, stats, 100f, new Vector3(0f, 0f, 1.2f));
            var allies = new List<MatchUnitState> { ally };
            var start = new Vector3(0f, 0f, 0f);
            var destination = new Vector3(0f, 0f, 4f);

            var result = UnitLocomotionRules.MoveTowards(
                start,
                destination,
                maxStep: 0.5f,
                allies,
                out var facing,
                spreadSeed: 2);

            result.y = 0f;
            Assert.Greater(Mathf.Abs(result.x), 0.05f, "Avoidance should push unit sideways around ally");

            var movement = result - start;
            movement.y = 0f;
            Assert.Greater(movement.sqrMagnitude, 0.0001f);
            Assert.Greater(
                Vector3.Dot(facing.normalized, movement.normalized),
                0.99f,
                "Facing should match actual movement, not destination direction.");
            Assert.Less(
                Vector3.Dot(facing.normalized, Vector3.forward),
                0.98f,
                "Avoidance sidestep should not keep pure destination facing.");
        }

        [Test]
        public void MoveTowards_ClampedStep_FacingMatchesActualStep()
        {
            var start = new Vector3(0f, 0f, 0f);
            var destination = new Vector3(0f, 0f, 0.35f);

            var result = UnitLocomotionRules.MoveTowards(
                start,
                destination,
                maxStep: 1f,
                allies: null,
                out var facing);

            var movement = result - start;
            movement.y = 0f;
            Assert.AreEqual(destination, result);
            Assert.Greater(Vector3.Dot(facing.normalized, movement.normalized), 0.99f);
        }

        [Test]
        public void TryGetFacingFromDisplacement_ReturnsNormalizedHorizontalDelta()
        {
            Assert.IsTrue(UnitLocomotionRules.TryGetFacingFromDisplacement(
                new Vector3(1f, 2f, 3f),
                new Vector3(4f, 9f, 7f),
                out var facing));
            Assert.AreEqual(0f, facing.y, 0.0001f);
            Assert.AreEqual(1f, facing.magnitude, 0.0001f);
            Assert.Greater(Vector3.Dot(facing, new Vector3(3f, 0f, 4f).normalized), 0.99f);

            Assert.IsFalse(UnitLocomotionRules.TryGetFacingFromDisplacement(
                Vector3.zero,
                new Vector3(0f, 5f, 0f),
                out _));
        }

        [Test]
        public void GetRouteLookaheadDestination_AdvancesAlongPath()
        {
            var path = new Game.Gameplay.Match.LanePath(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(20f, 0f, 0f),
            });
            var route = LaneRoute.FromPath(path);
            var start = new Vector3(0f, 0f, 0f);

            var destination = UnitLocomotionRules.GetRouteLookaheadDestination(
                route,
                start,
                maxStep: 1f);

            Assert.Greater(route.ProjectDistance(destination), 2f);
        }

        [Test]
        public void LimitDisplacement_CapsStepLength()
        {
            var from = new Vector3(0f, 0f, 0f);
            var to = new Vector3(10f, 0f, 0f);
            var limited = UnitLocomotionRules.LimitDisplacement(from, to, maxStep: 2f);
            Assert.AreEqual(2f, Vector3.Distance(from, limited), 0.001f);
            Assert.AreEqual(2f, limited.x, 0.001f);
        }

        [Test]
        public void ClampToWalkable_OutsideRoad_ClampsToRoadHalfWidth()
        {
            var path = new Game.Gameplay.Match.LanePath(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(40f, 0f, 0f),
            });
            var route = LaneRoute.FromPath(path);
            var outside = new Vector3(20f, 0f, UnitLocomotionRules.RoadHalfWidth + 5f);

            var clamped = UnitLocomotionRules.ClampToWalkable(
                route,
                outside,
                progressDistance: 20f,
                centerArenaRadius: 0f);

            Assert.AreEqual(UnitLocomotionRules.RoadHalfWidth, Mathf.Abs(clamped.z), 0.05f);
        }

        [Test]
        public void ClampToWalkable_InsideCenterArena_AllowsFreePosition()
        {
            var path = new Game.Gameplay.Match.LanePath(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(40f, 0f, 0f),
            });
            var route = LaneRoute.FromPath(path);
            var inArena = new Vector3(5f, 0f, 8f);

            var clamped = UnitLocomotionRules.ClampToWalkable(
                route,
                inArena,
                progressDistance: 5f,
                centerArenaRadius: 20f);

            Assert.AreEqual(inArena, clamped);
        }

        [Test]
        public void ApplyWalkableLimit_FarOutsideRoad_DoesNotTeleport()
        {
            var path = new Game.Gameplay.Match.LanePath(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(40f, 0f, 0f),
            });
            var route = LaneRoute.FromPath(path);
            var previous = new Vector3(20f, 0f, UnitLocomotionRules.RoadHalfWidth + 8f);
            var proposed = previous;
            const float maxStep = 0.5f;

            var hard = UnitLocomotionRules.ClampToWalkable(route, proposed, 20f, centerArenaRadius: 0f);
            Assert.Greater(Vector3.Distance(previous, hard), maxStep * 2f, "Hard clamp would teleport.");

            var soft = UnitLocomotionRules.ApplyWalkableLimit(
                route,
                previous,
                proposed,
                maxStep,
                20f,
                centerArenaRadius: 0f);
            Assert.LessOrEqual(Vector3.Distance(previous, soft), maxStep + 0.001f);
            Assert.Less(Mathf.Abs(soft.z), Mathf.Abs(previous.z));
        }
    }
}
