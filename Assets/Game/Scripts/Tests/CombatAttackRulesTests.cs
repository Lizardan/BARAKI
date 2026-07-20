using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CombatAttackRulesTests
    {
        [Test]
        public void UsesParabolicArc_OnlyForRanged()
        {
            Assert.IsTrue(CombatAttackRules.UsesParabolicArc(UnitRole.Ranged));
            Assert.IsFalse(CombatAttackRules.UsesParabolicArc(UnitRole.Caster));
            Assert.IsFalse(CombatAttackRules.UsesParabolicArc(UnitRole.Melee));
        }

        [Test]
        public void UsesMeleeStrike_IncludesSiegeAndSuper()
        {
            Assert.IsTrue(CombatAttackRules.UsesMeleeStrike(UnitRole.Melee));
            Assert.IsTrue(CombatAttackRules.UsesMeleeStrike(UnitRole.Siege));
            Assert.IsTrue(CombatAttackRules.UsesMeleeStrike(UnitRole.Super));
            Assert.IsFalse(CombatAttackRules.UsesMeleeStrike(UnitRole.Ranged));
            Assert.IsFalse(CombatAttackRules.UsesMeleeStrike(UnitRole.Flying));
        }

        [Test]
        public void UsesProjectile_ExcludesSiegeAndSuper()
        {
            Assert.IsTrue(CombatAttackRules.UsesProjectile(UnitRole.Ranged));
            Assert.IsTrue(CombatAttackRules.UsesProjectile(UnitRole.Caster));
            Assert.IsTrue(CombatAttackRules.UsesProjectile(UnitRole.Flying));
            Assert.IsFalse(CombatAttackRules.UsesProjectile(UnitRole.Siege));
            Assert.IsFalse(CombatAttackRules.UsesProjectile(UnitRole.Super));
            Assert.IsFalse(CombatAttackRules.UsesProjectile(UnitRole.Melee));
        }

        [Test]
        public void Tick_RangedAttack_AppliesDamageOnProjectileImpact()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var archerStats = new UnitCombatStats(
                UnitRole.Ranged,
                maxHp: 100f,
                armor: 0f,
                damageMin: 40f,
                damageMax: 40f,
                attackSpeed: 10f,
                attackRange: 30f,
                moveSpeed: 0f,
                goldBounty: 1);

            var victimStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            // Keep a few meters of separation so the projectile has flight time.
            var meet0 = lane0.Path.ProjectDistance(new Vector3(-4f, 0f, 0f));
            var meet1 = lane1.Path.ProjectDistance(new Vector3(4f, 0f, 0f));

            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Ranged, archerStats, meet0);
            var victim = combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats, meet1);

            for (var i = 0; i < 40; i++)
            {
                combat.Tick(0.05f);
                if (combat.Projectiles.Count > 0)
                {
                    break;
                }
            }

            Assert.Greater(combat.Projectiles.Count, 0);
            var hpWhenFired = victim.CurrentHp;
            combat.Tick(0.04f);
            Assert.AreEqual(hpWhenFired, victim.CurrentHp, 0.01f, "Damage should not apply before projectile impact");
            combat.Tick(1f);
            Assert.Less(victim.CurrentHp, hpWhenFired);
        }

        [Test]
        public void Tick_MeleeAttack_AppliesDamageAfterStrikeDelay()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            var meleeStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 40f,
                damageMax: 40f,
                attackSpeed: 10f,
                attackRange: 30f,
                moveSpeed: 0f,
                goldBounty: 1);

            var victimStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meetPoint = Vector3.zero;
            var meet0 = lane0.Path.ProjectDistance(meetPoint);
            var meet1 = lane1.Path.ProjectDistance(meetPoint);

            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, meleeStats, meet0);
            var victim = combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats, meet1);

            for (var i = 0; i < 40; i++)
            {
                combat.Tick(0.05f);
                if (combat.MeleeStrikes.Count > 0)
                {
                    break;
                }
            }

            Assert.Greater(combat.MeleeStrikes.Count, 0);
            var hpWhenStrike = victim.CurrentHp;
            combat.Tick(0.04f);
            Assert.AreEqual(hpWhenStrike, victim.CurrentHp, 0.01f, "Melee damage should land after strike delay");
            combat.Tick(CombatAttackRules.MeleeStrikeDuration);
            Assert.Less(victim.CurrentHp, hpWhenStrike);
        }

        [Test]
        public void Tick_ProjectileImpact_DespawnDuringResolve_DoesNotThrow()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);

            // Mid-impact despawn mutates the projectile list (elimination / DespawnUnitsForOwner).
            combat.UnitKilled += _ => combat.DespawnUnitsForOwner(0);

            var archerStats = new UnitCombatStats(
                UnitRole.Ranged,
                maxHp: 100f,
                armor: 0f,
                damageMin: 200f,
                damageMax: 200f,
                attackSpeed: 10f,
                attackRange: 30f,
                moveSpeed: 0f,
                goldBounty: 1);

            var victimStats = new UnitCombatStats(
                UnitRole.Melee,
                maxHp: 50f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 1);

            controller.Graph.TryGetLane(0, GameIds.Lanes.Center, out var lane0);
            controller.Graph.TryGetLane(1, GameIds.Lanes.Center, out var lane1);
            var meet0 = lane0.Path.ProjectDistance(new Vector3(-4f, 0f, 0f));
            var meet1 = lane1.Path.ProjectDistance(new Vector3(4f, 0f, 0f));

            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Ranged, archerStats, meet0);
            combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Ranged, archerStats, meet0 + 0.5f);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats, meet1);
            combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats, meet1 + 0.5f);

            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 80; i++)
                {
                    combat.Tick(0.05f);
                }
            });
        }
    }
}
