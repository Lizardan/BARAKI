using Game.Gameplay.Combat;
using Game.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AbilityFxMechanicRulesTests
    {
        [Test]
        public void Auras_FollowBearer_AndShowRing()
        {
            var mechanic = AbilityFxMechanicRules.Resolve(AbilityIds.AuraDamagePercent);
            Assert.AreEqual(AbilityFxMechanicShape.AuraAroundSelf, mechanic.Shape);
            Assert.IsTrue(mechanic.FollowsHost);
            Assert.IsTrue(mechanic.ShowRing);
            Assert.IsFalse(mechanic.FlatOnGround);
            Assert.AreEqual(AbilityVfxAnchor.Caster, mechanic.RingHost);
            Assert.AreEqual(HeroAbilityRules.AuraRadius, mechanic.Radius);
            Assert.AreEqual("Аура вокруг себя", mechanic.Title);
            StringAssert.Contains("едет", mechanic.Motion);
        }

        [Test]
        public void GreaterHealAndConsecration_StayOnGroundAroundCaster()
        {
            var heal = AbilityFxMechanicRules.Resolve(AbilityIds.GreaterHeal);
            Assert.AreEqual(AbilityFxMechanicShape.AreaOnGround, heal.Shape);
            Assert.IsFalse(heal.FollowsHost);
            Assert.IsTrue(heal.FlatOnGround);
            Assert.AreEqual(AbilityVfxAnchor.Ground, heal.RingHost);
            Assert.AreEqual(HeroAbilityRules.GreaterHealRadius, heal.Radius);

            var consecration = AbilityFxMechanicRules.Resolve(AbilityIds.Consecration);
            Assert.AreEqual(AbilityFxMechanicShape.AreaOnGround, consecration.Shape);
            Assert.AreEqual(AbilityVfxAnchor.Ground, consecration.RingHost);
            StringAssert.Contains("земле", consecration.Motion);
        }

        [Test]
        public void Frost_IsGroundAreaAroundEnemyCluster()
        {
            var frost = AbilityFxMechanicRules.Resolve(AbilityIds.Frost);
            Assert.AreEqual(AbilityFxMechanicShape.AreaOnGround, frost.Shape);
            Assert.IsFalse(frost.FollowsHost);
            Assert.AreEqual(AbilityVfxAnchor.Target, frost.RingHost);
            Assert.AreEqual(CasterSpellRules.FrostRadius, frost.Radius);
            StringAssert.Contains("враг", frost.Title);
        }

        [Test]
        public void StrikeAndCleave_AreBurstAroundSelf()
        {
            Assert.AreEqual(
                AbilityFxMechanicShape.BurstAroundSelf,
                AbilityFxMechanicRules.ResolveShape(AbilityIds.Strike));
            Assert.AreEqual(
                AbilityFxMechanicShape.BurstAroundSelf,
                AbilityFxMechanicRules.ResolveShape(AbilityIds.MeleeCleave));
            var strike = AbilityFxMechanicRules.Resolve(AbilityIds.Strike);
            Assert.AreEqual(HeroAbilityRules.StrikeRadius, strike.Radius);
            Assert.IsTrue(strike.ShowRing);
            Assert.IsFalse(strike.FollowsHost);
        }

        [Test]
        public void HolyNova_IsBurstAroundTarget()
        {
            var nova = AbilityFxMechanicRules.Resolve(AbilityIds.HolyNova);
            Assert.AreEqual(AbilityFxMechanicShape.BurstAroundTarget, nova.Shape);
            Assert.AreEqual(AbilityVfxAnchor.Target, nova.RingHost);
            Assert.AreEqual(HeroAbilityRules.NovaRadius, nova.Radius);
        }

        [Test]
        public void Catapult_IsImpactSplash()
        {
            var catapult = AbilityFxMechanicRules.Resolve(AbilityIds.SuperCatapult);
            Assert.AreEqual(AbilityFxMechanicShape.BurstAtImpact, catapult.Shape);
            Assert.AreEqual(AbilityVfxAnchor.Impact, catapult.RingHost);
            Assert.AreEqual(HumanBonusUnitRules.CatapultAoeRadius, catapult.Radius);
            Assert.IsTrue(catapult.FlatOnGround);
        }

        [Test]
        public void SmiteAndMend_ArePoint_NoRingEvenWithSearchRadius()
        {
            var smite = AbilityFxMechanicRules.Resolve(AbilityIds.Smite, defRadius: 5f);
            Assert.AreEqual(AbilityFxMechanicShape.PointOnTarget, smite.Shape);
            Assert.IsFalse(smite.ShowRing);
            Assert.AreEqual(5f, smite.Reach);
            StringAssert.Contains("досягаемость", smite.RadiusLabel);

            var mend = AbilityFxMechanicRules.Resolve(AbilityIds.CasterHeal, castRange: 6f);
            Assert.AreEqual(AbilityFxMechanicShape.PointOnTarget, mend.Shape);
            Assert.IsFalse(mend.ShowRing);
            Assert.AreEqual(6f, mend.Reach);
        }

        [Test]
        public void DivineBlessingSmites_ArePointOnTarget_NoRing()
        {
            var building = AbilityFxMechanicRules.Resolve(AbilityIds.MainBuildingSmite, defRadius: 2.2f);
            Assert.AreEqual(AbilityFxMechanicShape.PointOnTarget, building.Shape);
            Assert.IsFalse(building.ShowRing);
            Assert.AreEqual(0f, building.Reach);
            StringAssert.Contains("без круга", building.RadiusLabel);

            var unit = AbilityFxMechanicRules.Resolve(AbilityIds.MainUnitSmite);
            Assert.AreEqual(AbilityFxMechanicShape.PointOnTarget, unit.Shape);
            Assert.IsFalse(unit.ShowRing);
        }

        [Test]
        public void AuthoredRadius_WinsOverFallback()
        {
            var frost = AbilityFxMechanicRules.Resolve(AbilityIds.Frost, defRadius: 9f);
            Assert.AreEqual(9f, frost.Radius);
        }

        [Test]
        public void FacelessCaster_Spells_HaveTheirShapes()
        {
            Assert.AreEqual(
                AbilityFxMechanicShape.PointOnTarget,
                AbilityFxMechanicRules.ResolveShape(AbilityIds.BlightingGaze));

            var drain = AbilityFxMechanicRules.Resolve(AbilityIds.VoidDrain);
            Assert.AreEqual(AbilityFxMechanicShape.AreaOnGround, drain.Shape);
            Assert.IsTrue(drain.FlatOnGround);
            Assert.AreEqual(AbilityVfxAnchor.Ground, drain.RingHost);

            var raise = AbilityFxMechanicRules.Resolve(AbilityIds.RaiseDrowned);
            Assert.AreEqual(AbilityFxMechanicShape.PointOnSelf, raise.Shape);
            Assert.IsFalse(raise.ShowRing);
            Assert.AreEqual(AbilityVfxAnchor.Unspecified, raise.RingHost);
        }

        [Test]
        public void FacelessVeterans_HaveTheirShapes()
        {
            var mantle = AbilityFxMechanicRules.Resolve(AbilityIds.AncientMantle);
            Assert.AreEqual(AbilityFxMechanicShape.BurstAroundSelf, mantle.Shape);
            Assert.AreEqual(AbilityVfxAnchor.Caster, mantle.RingHost);

            var miss = AbilityFxMechanicRules.Resolve(AbilityIds.AreaOfMiss);
            Assert.AreEqual(AbilityFxMechanicShape.AreaOnGround, miss.Shape);
            Assert.AreEqual(AbilityVfxAnchor.Ground, miss.RingHost);

            var feast = AbilityFxMechanicRules.Resolve(AbilityIds.FeastZone);
            Assert.AreEqual(AbilityFxMechanicShape.AuraAroundSelf, feast.Shape);
            Assert.IsTrue(feast.FollowsHost);
            Assert.AreEqual(AbilityVfxAnchor.Caster, feast.RingHost);

            var hunger = AbilityFxMechanicRules.Resolve(AbilityIds.AuraOfHunger);
            Assert.AreEqual(AbilityFxMechanicShape.AuraAroundSelf, hunger.Shape);
            Assert.IsTrue(hunger.FollowsHost);
            Assert.AreEqual(AbilityVfxAnchor.Caster, hunger.RingHost);
            Assert.AreEqual(HeroAbilityRules.AuraRadius, hunger.Radius);
        }

        [Test]
        public void PreviewRingRadius_IsCombatMetres()
        {
            Assert.AreEqual(
                CasterSpellRules.FrostRadius,
                AbilityFxMechanicRules.PreviewRingRadius(CasterSpellRules.FrostRadius));
            Assert.AreEqual(
                HeroAbilityRules.AuraRadius,
                AbilityFxMechanicRules.PreviewRingRadius(HeroAbilityRules.AuraRadius));
            Assert.AreEqual(0f, AbilityFxMechanicRules.PreviewRingRadius(0f));
        }
    }
}
