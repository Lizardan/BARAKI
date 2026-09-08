using Game.Gameplay.Combat;
using Game.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AbilityVfxKindRulesTests
    {
        [Test]
        public void Resolve_Auras_AreAuraPalette()
        {
            Assert.AreEqual(AbilityVfxKind.Aura, AbilityVfxKindRules.Resolve(AbilityIds.AuraDamagePercent));
            Assert.AreEqual(AbilityVfxKind.Aura, AbilityVfxKindRules.Resolve(AbilityIds.AuraAttackSpeedPercent));
            Assert.AreEqual(AbilityVfxKind.Aura, AbilityVfxKindRules.Resolve(AbilityIds.AuraArmorPercent));
            Assert.AreEqual(AbilityVfxKind.Aura, AbilityVfxKindRules.Resolve(AbilityIds.AuraMaxHpPercent));
            Assert.AreEqual(AbilityVfxKind.Aura, AbilityVfxKindRules.Resolve(AbilityIds.AuraHpRegen));
            Assert.AreEqual(AbilityVfxKind.Aura, AbilityVfxKindRules.Resolve(AbilityIds.AuraOfHunger));
        }

        [Test]
        public void Resolve_StrikeAndCleave_AreHitPalette()
        {
            Assert.AreEqual(AbilityVfxKind.Hit, AbilityVfxKindRules.Resolve(AbilityIds.Strike));
            Assert.AreEqual(AbilityVfxKind.Hit, AbilityVfxKindRules.Resolve(AbilityIds.MeleeCleave));
            Assert.AreEqual(AbilityVfxKind.Hit, AbilityVfxKindRules.Resolve(AbilityIds.Smite));
            Assert.AreEqual(AbilityVfxKind.Hit, AbilityVfxKindRules.Resolve(AbilityIds.SuperCatapult));
            Assert.AreEqual(AbilityVfxKind.Hit, AbilityVfxKindRules.Resolve(AbilityIds.BlightingGaze));
            Assert.AreEqual(AbilityVfxKind.Hit, AbilityVfxKindRules.Resolve(AbilityIds.VoidDrain));
            Assert.AreEqual(AbilityVfxKind.Hit, AbilityVfxKindRules.Resolve(AbilityIds.AncientMantle));
        }

        [Test]
        public void Resolve_MendAndDivineBlessing_AreCastPalette()
        {
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.CasterHeal));
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.Frost));
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.FlyingSpawn));
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.MainBuildingSmite));
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.MainUnitSmite));
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.RaiseDrowned));
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.AreaOfMiss));
            Assert.AreEqual(AbilityVfxKind.Cast, AbilityVfxKindRules.Resolve(AbilityIds.FeastZone));
        }

        [Test]
        public void ResolveDefaultAnchor_AurasAndStrike_AreCaster()
        {
            Assert.AreEqual(AbilityVfxAnchor.Caster, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.AuraDamagePercent));
            Assert.AreEqual(AbilityVfxAnchor.Caster, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.Strike));
            Assert.AreEqual(AbilityVfxAnchor.Caster, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.Slam));
            Assert.AreEqual(AbilityVfxAnchor.Caster, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.RaiseDrowned));
            Assert.AreEqual(AbilityVfxAnchor.Caster, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.AncientMantle));
            Assert.AreEqual(AbilityVfxAnchor.Caster, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.FeastZone));
            Assert.AreEqual(AbilityVfxAnchor.Caster, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.AuraOfHunger));
        }

        [Test]
        public void ResolveDefaultAnchor_SmiteAndMend_AreTarget()
        {
            Assert.AreEqual(AbilityVfxAnchor.Target, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.Smite));
            Assert.AreEqual(AbilityVfxAnchor.Target, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.CasterHeal));
            Assert.AreEqual(AbilityVfxAnchor.Target, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.RangedCrit));
            Assert.AreEqual(AbilityVfxAnchor.Target, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.MainUnitSmite));
            Assert.AreEqual(AbilityVfxAnchor.Target, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.BlightingGaze));
            Assert.AreEqual(
                AbilityVfxAnchor.Target,
                AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.MainBuildingSmite));
        }

        [Test]
        public void ResolveDefaultAnchor_FrostAndConsecration_AreGround()
        {
            Assert.AreEqual(AbilityVfxAnchor.Ground, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.Frost));
            Assert.AreEqual(AbilityVfxAnchor.Ground, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.Consecration));
            Assert.AreEqual(AbilityVfxAnchor.Ground, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.VoidDrain));
            Assert.AreEqual(AbilityVfxAnchor.Ground, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.AreaOfMiss));
        }

        [Test]
        public void ResolveDefaultAnchor_CatapultLastCall_AreImpact()
        {
            Assert.AreEqual(AbilityVfxAnchor.Impact, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.SuperCatapult));
            Assert.AreEqual(AbilityVfxAnchor.Impact, AbilityVfxKindRules.ResolveDefaultAnchor(AbilityIds.FlyingSpawn));
        }

        [Test]
        public void ResolveAnchor_Unspecified_UsesDefault_AuthoredWins()
        {
            Assert.AreEqual(
                AbilityVfxAnchor.Target,
                AbilityVfxKindRules.ResolveAnchor(AbilityIds.Smite, AbilityVfxAnchor.Unspecified));
            Assert.AreEqual(
                AbilityVfxAnchor.Ground,
                AbilityVfxKindRules.ResolveAnchor(AbilityIds.Smite, AbilityVfxAnchor.Ground));
        }

        [Test]
        public void KitLabel_Faceless_MatchesOwner()
        {
            Assert.AreEqual("Faceless Caster", AbilityVfxKindRules.KitLabel(AbilityIds.BlightingGaze));
            Assert.AreEqual("Faceless Caster", AbilityVfxKindRules.KitLabel(AbilityIds.VoidDrain));
            Assert.AreEqual("Faceless Caster", AbilityVfxKindRules.KitLabel(AbilityIds.RaiseDrowned));
            Assert.AreEqual("Faceless King", AbilityVfxKindRules.KitLabel(AbilityIds.AncientMantle));
            Assert.AreEqual("Faceless Warlock", AbilityVfxKindRules.KitLabel(AbilityIds.AreaOfMiss));
            Assert.AreEqual("Faceless Berserker", AbilityVfxKindRules.KitLabel(AbilityIds.FeastZone));
            Assert.AreEqual("Faceless Titan", AbilityVfxKindRules.KitLabel(AbilityIds.AuraOfHunger));
        }
    }
}
