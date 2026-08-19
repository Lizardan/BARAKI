using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AbilityFxPreviewCasterRulesTests
    {
        [Test]
        public void Resolve_KingKit_IsHeroSlot1()
        {
            var caster = AbilityFxPreviewCasterRules.Resolve(AbilityIds.Strike);
            Assert.AreEqual(UnitRole.Hero, caster.Role);
            Assert.AreEqual(HeroAbilityRules.KingSlot, caster.HeroSlot);
            Assert.AreEqual("Король", caster.DisplayName);
            Assert.IsFalse(caster.IsBuilding);
        }

        [Test]
        public void Resolve_PaladinAndPriest_UseHeroSlots()
        {
            var paladin = AbilityFxPreviewCasterRules.Resolve(AbilityIds.Smite);
            Assert.AreEqual(HeroAbilityRules.PaladinSlot, paladin.HeroSlot);
            Assert.AreEqual("Паладин", paladin.DisplayName);

            var priest = AbilityFxPreviewCasterRules.Resolve(AbilityIds.HolyNova);
            Assert.AreEqual(HeroAbilityRules.PriestSlot, priest.HeroSlot);
            Assert.AreEqual("Жрец", priest.DisplayName);
        }

        [Test]
        public void Resolve_TitanAndCasterKits()
        {
            var titan = AbilityFxPreviewCasterRules.Resolve(AbilityIds.Slam);
            Assert.AreEqual(UnitRole.Titan, titan.Role);
            Assert.AreEqual("Титан", titan.DisplayName);

            var caster = AbilityFxPreviewCasterRules.Resolve(AbilityIds.Frost);
            Assert.AreEqual(UnitRole.Caster, caster.Role);
            Assert.AreEqual(0, caster.BonusSlot);
            Assert.AreEqual("Кастер", caster.DisplayName);
        }

        [Test]
        public void Resolve_BonusKits_UseBonusSlots()
        {
            var melee = AbilityFxPreviewCasterRules.Resolve(AbilityIds.MeleeCleave);
            Assert.AreEqual(UnitRole.Melee, melee.Role);
            Assert.AreEqual(HumanBonusUnitRules.BonusSlotForRole(UnitRole.Melee), melee.BonusSlot);

            var siege = AbilityFxPreviewCasterRules.Resolve(AbilityIds.AuraHpRegen);
            Assert.AreEqual(UnitRole.Siege, siege.Role);
            Assert.AreEqual(HumanBonusUnitRules.BonusSlotForRole(UnitRole.Siege), siege.BonusSlot);
        }

        [Test]
        public void Resolve_DivineBlessing_IsMainBuilding()
        {
            var building = AbilityFxPreviewCasterRules.Resolve(AbilityIds.MainBuildingSmite);
            Assert.IsTrue(building.IsBuilding);
            Assert.AreEqual("Главное здание", building.DisplayName);

            var unit = AbilityFxPreviewCasterRules.Resolve(AbilityIds.MainUnitSmite);
            Assert.IsTrue(unit.IsBuilding);
            Assert.AreEqual("Главное здание", unit.DisplayName);
        }
    }
}
