using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CombatAttackVisualBuilderTests
    {
        [Test]
        public void ResolveVisualStyle_BuildingAttack_IsOwnerColoredCube()
        {
            var projectile = new CombatProjectileState(
                projectileId: 1,
                attackerUnitId: 0,
                targetUnitId: 2,
                attackerOwnerSlot: 1,
                attackerRole: UnitRole.Ranged,
                attackerRaceId: Game.Core.GameIds.Races.Human,
                rawDamage: 20f,
                flightDuration: 0.5f,
                startPosition: Vector3.zero,
                targetPosition: Vector3.forward,
                isParabolic: false,
                targetBuildingInstanceId: null,
                sourceBuildingInstanceId: 44);

            CombatAttackVisualBuilder.ResolveVisualStyle(
                projectile,
                out var primitive,
                out var scale,
                out var color);

            Assert.AreEqual(PrimitiveType.Cube, primitive);
            Assert.AreEqual(
                Vector3.one * TowerCombatRules.ProjectileCubeScale,
                scale);
            Assert.AreEqual(MatchPlayerColors.GetSlotColor(1), color);
        }

        [Test]
        public void CreateProjectileVisual_Ranged_BuildsArrowWithShaftAndHead()
        {
            var projectile = new CombatProjectileState(
                projectileId: 7,
                attackerUnitId: 1,
                targetUnitId: 3,
                attackerOwnerSlot: 0,
                attackerRole: UnitRole.Ranged,
                attackerRaceId: Game.Core.GameIds.Races.Human,
                rawDamage: 12f,
                flightDuration: 0.6f,
                startPosition: Vector3.zero,
                targetPosition: Vector3.forward * 6f,
                isParabolic: true);

            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            var shaft = visual.transform.Find("Shaft");
            var head = visual.transform.Find("Head");
            Assert.IsNotNull(shaft, "Arrow should have a Shaft child");
            Assert.IsNotNull(head, "Arrow should have a Head child");
            Assert.IsTrue(visual.GetComponentsInChildren<Collider>().Length == 0, "Arrow children must not have colliders");
            Assert.AreEqual(Vector3.one, visual.transform.localScale);

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_Caster_BuildsFireballWithTrail()
        {
            var projectile = new CombatProjectileState(
                projectileId: 8,
                attackerUnitId: 1,
                targetUnitId: 3,
                attackerOwnerSlot: 2,
                attackerRole: UnitRole.Caster,
                attackerRaceId: Game.Core.GameIds.Races.Human,
                rawDamage: 18f,
                flightDuration: 0.6f,
                startPosition: Vector3.zero,
                targetPosition: Vector3.forward * 6f,
                isParabolic: false);

            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            Assert.IsNotNull(visual.transform.Find("Core"), "Fireball should have a Core child");
            Assert.IsNotNull(visual.GetComponent<TrailRenderer>(), "Fireball should carry a TrailRenderer");
            Assert.IsTrue(visual.GetComponentsInChildren<Collider>().Length == 0, "Fireball must not have colliders");

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_BuildingShot_IsSingleOwnerColoredCube()
        {
            var projectile = new CombatProjectileState(
                projectileId: 9,
                attackerUnitId: 0,
                targetUnitId: 2,
                attackerOwnerSlot: 3,
                attackerRole: UnitRole.Ranged,
                attackerRaceId: Game.Core.GameIds.Races.Human,
                rawDamage: 20f,
                flightDuration: 0.5f,
                startPosition: Vector3.zero,
                targetPosition: Vector3.forward,
                isParabolic: false,
                targetBuildingInstanceId: null,
                sourceBuildingInstanceId: 44);

            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            Assert.IsNull(visual.transform.Find("Shaft"), "Building shot should be a plain cube, not an arrow");
            Assert.IsNull(visual.GetComponent<TrailRenderer>(), "Building shot should have no trail");

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }
    }
}
