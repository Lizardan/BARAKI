using System.Collections.Generic;
using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CombatMovementRulesTests
    {
        [Test]
        public void ResolveCollisionSlide_SlidesPastObstacle()
        {
            var from = Vector3.zero;
            var step = new Vector3(3f, 0f, 0f);
            var obstacles = new List<Vector3> { new Vector3(2f, 0f, 0f) };

            var resolved = CombatMovementRules.ResolveCollisionSlide(
                from,
                step,
                CombatFormationRules.MinUnitSeparation,
                obstacles);

            var finalPosition = from + resolved;
            finalPosition.y = 0f;
            var obstacle = obstacles[0];
            obstacle.y = 0f;
            var separation = Vector3.Distance(finalPosition, obstacle);

            Assert.GreaterOrEqual(separation, CombatFormationRules.MinUnitSeparation * 0.95f);
            Assert.Greater(resolved.magnitude, 0.01f);
        }

        [Test]
        public void ApplyRepulsion_DeflectsStepAwayFromBlocker()
        {
            var desired = new Vector3(1f, 0f, 0f);
            var myPosition = Vector3.zero;
            var blockers = new List<Vector3> { new Vector3(0.5f, 0f, 0f) };

            var steered = CombatMovementRules.ApplyRepulsion(
                desired,
                myPosition,
                blockers,
                CombatFormationRules.MinUnitSeparation);

            Assert.Less(steered.x, desired.x);
            Assert.Greater(steered.magnitude, 0.01f);
        }

        [Test]
        public void ClampStepToAvoidOverlap_ShortensStepBeforeOverlap()
        {
            var separation = CombatFormationRules.MinUnitSeparation;
            var from = new Vector3(-(separation + 1.25f), 0f, 0f);
            var step = new Vector3(1.5f, 0f, 0f);
            var obstacles = new List<Vector3> { Vector3.zero };

            var clamped = CombatMovementRules.ClampStepToAvoidOverlap(
                from,
                step,
                obstacles,
                separation);

            Assert.Less(clamped.magnitude, step.magnitude);
            Assert.GreaterOrEqual(
                Vector3.Distance(from + clamped, obstacles[0]),
                separation * 0.99f);
        }
    }
}
