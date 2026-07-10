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
                out _,
                spreadSeed: 2);

            result.y = 0f;
            Assert.Greater(Mathf.Abs(result.x), 0.05f, "Avoidance should push unit sideways around ally");
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
    }
}
