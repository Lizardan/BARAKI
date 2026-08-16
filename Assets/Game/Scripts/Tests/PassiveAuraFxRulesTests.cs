using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PassiveAuraFxRulesTests
    {
        [Test]
        public void ResolveKind_AllPassiveAurasUseRunic()
        {
            Assert.AreEqual(PassiveAuraFxKind.Runic, PassiveAuraFxRules.ResolveKind(AbilityIds.AuraHpRegen));
            Assert.AreEqual(PassiveAuraFxKind.Runic, PassiveAuraFxRules.ResolveKind(AbilityIds.AuraAttackSpeedPercent));
            Assert.AreEqual(PassiveAuraFxKind.Runic, PassiveAuraFxRules.ResolveKind(AbilityIds.AuraDamagePercent));
            Assert.AreEqual(PassiveAuraFxKind.Runic, PassiveAuraFxRules.ResolveKind(AbilityIds.AuraArmorPercent));
            Assert.AreEqual(PassiveAuraFxKind.Runic, PassiveAuraFxRules.ResolveKind(AbilityIds.AuraMaxHpPercent));
        }

        [Test]
        public void ResolveAbilityIdFromColor_MatchesNearestAuraTint()
        {
            Assert.AreEqual(
                AbilityIds.AuraHpRegen,
                PassiveAuraFxRules.ResolveAbilityIdFromColor(AbilityFxColors.AuraHpRegen));
            Assert.AreEqual(
                AbilityIds.AuraAttackSpeedPercent,
                PassiveAuraFxRules.ResolveAbilityIdFromColor(AbilityFxColors.AuraAttackSpeed));
            Assert.AreEqual(
                AbilityIds.AuraDamagePercent,
                PassiveAuraFxRules.ResolveAbilityIdFromColor(AbilityFxColors.AuraDamage));
            Assert.AreEqual(
                AbilityIds.AuraArmorPercent,
                PassiveAuraFxRules.ResolveAbilityIdFromColor(AbilityFxColors.AuraArmor));
        }

        [Test]
        public void ResolveScale_GrowsWithAuraRadius_ButIsVisuallyShrunk()
        {
            var small = PassiveAuraFxRules.ResolveScale(PassiveAuraFxKind.Runic, 2f);
            var large = PassiveAuraFxRules.ResolveScale(PassiveAuraFxKind.Runic, HeroAbilityRules.AuraRadius);
            Assert.Greater(large, small);
            var fullFootprint = HeroAbilityRules.AuraRadius / PassiveAuraFxRules.RunicReferenceRadius;
            Assert.AreEqual(
                fullFootprint * PassiveAuraFxRules.VisualFootprintMultiplier,
                large,
                0.001f);
            Assert.Less(large, fullFootprint);
        }
    }
}
