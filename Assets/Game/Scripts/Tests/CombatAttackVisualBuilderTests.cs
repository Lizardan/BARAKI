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
    }
}
