using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Faceless (Древние) turn their Super (крип) and Flying into melee fighters:
    /// strike in close range with no minimum-arc-band, no projectile, and Flying can
    /// still hit flying targets. These rules live on <see cref="UnitCombatIdentity"/>.
    /// </summary>
    public sealed class UnitCombatIdentityTests
    {
        const string Faceless = "RACE_FACELESS";

        static UnitCombatIdentity Identity(UnitRole role, int heroSlot = 0) =>
            UnitCombatIdentityFactory.Of(Faceless, role, heroSlot != 0, heroSlot);

        [Test]
        public void FacelessSuper_IsMelee_NoProjectile_NoMinRange()
        {
            var identity = Identity(UnitRole.Super);
            Assert.IsTrue(identity.UsesMeleeStrike);
            Assert.IsFalse(identity.UsesProjectile);
            Assert.AreEqual(0f, identity.GetMinAttackRange(), 0.001f);
        }

        [Test]
        public void FacelessSuper_AttacksGroundTargets_ButNotFlying()
        {
            var identity = Identity(UnitRole.Super);
            Assert.IsTrue(identity.CanAttackTarget(UnitRole.Melee));
            Assert.IsFalse(identity.CanAttackTarget(UnitRole.Flying));
            Assert.IsTrue(identity.CanAttackTarget(UnitRole.Titan));
        }

        [Test]
        public void FacelessFlying_IsMelee_ButHitsFlyingTargets()
        {
            var identity = Identity(UnitRole.Flying);
            Assert.IsTrue(identity.UsesMeleeStrike);
            Assert.IsFalse(identity.UsesProjectile);
            Assert.IsTrue(identity.CanAttackTarget(UnitRole.Flying));
        }

        [Test]
        public void FacelessMelee_KeepsBaseDelivery()
        {
            var identity = Identity(UnitRole.Melee);
            Assert.IsTrue(identity.UsesMeleeStrike);
            Assert.IsFalse(identity.UsesProjectile);
        }

        [Test]
        public void FacelessRanged_KeepsProjectileDelivery()
        {
            var identity = Identity(UnitRole.Ranged);
            Assert.IsFalse(identity.UsesMeleeStrike);
            Assert.IsTrue(identity.UsesProjectile);
            Assert.IsTrue(identity.CanAttackTarget(UnitRole.Flying));
        }

        [Test]
        public void HumanRoles_UnchangedByIdentity()
        {
            var humanSuper = UnitCombatIdentityFactory.Of(GameIds.Races.Human, UnitRole.Super);
            Assert.IsFalse(humanSuper.UsesMeleeStrike);
            Assert.IsTrue(humanSuper.UsesProjectile);
            Assert.AreEqual(CombatRules.GetMinAttackRange(UnitRole.Super), humanSuper.GetMinAttackRange(), 0.001f);

            var humanFlying = UnitCombatIdentityFactory.Of(GameIds.Races.Human, UnitRole.Flying);
            Assert.IsFalse(humanFlying.UsesMeleeStrike);
            Assert.IsTrue(humanFlying.UsesProjectile);
        }

        [Test]
        public void FacelessHero_KeepsHeroMeleeDelivery()
        {
            var hero = Identity(UnitRole.Hero, heroSlot: HeroAbilityRules.KingSlot);
            Assert.IsTrue(hero.UsesMeleeStrike);
            Assert.IsFalse(hero.UsesProjectile);
        }

        [Test]
        public void FacelessHeroPriest_ShootsLikeCaster()
        {
            var priest = Identity(UnitRole.Hero, heroSlot: HeroAbilityRules.PriestSlot);
            Assert.IsFalse(priest.UsesMeleeStrike);
            Assert.IsTrue(priest.UsesProjectile);
        }

        [Test]
        public void FacelessSuper_MeleeImpactUsesMidSwingTiming()
        {
            var identity = Identity(UnitRole.Super);
            var delay = CombatAttackRules.ResolveSwingImpactDelay(1f, identity);
            Assert.AreEqual(CombatAttackRules.SwingImpactNormalizedTime, delay, 0.001f);
        }
    }
}