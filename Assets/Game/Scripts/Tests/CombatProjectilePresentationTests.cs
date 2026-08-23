using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CombatProjectilePresentationTests
    {
        [Test]
        public void ResolvePresentationProgress_UsesSpawnClockAheadOfSimElapsed()
        {
            var projectile = new CombatProjectileState(
                projectileId: 1,
                attackerUnitId: 1,
                targetUnitId: 2,
                attackerOwnerSlot: 0,
                attackerRole: UnitRole.Ranged,
                attackerRaceId: GameIds.Races.Human,
                rawDamage: 5f,
                flightDuration: 1f,
                startPosition: Vector3.zero,
                targetPosition: Vector3.forward * 10f,
                isParabolic: false);
            projectile.Elapsed = 0f;
            projectile.SpawnRealtime = 10f;

            Assert.Greater(projectile.ResolvePresentationElapsed(10.05f), projectile.Elapsed);
            Assert.Greater(projectile.ResolvePresentationProgress(10.05f), 0f);
            Assert.Less(projectile.ResolvePresentationProgress(10.05f), 1f);
        }
    }
}
