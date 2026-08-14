using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AbilityKitDefaultsTests
    {
        [Test]
        public void MeleeKit_IsEmpty()
        {
            var kit = AbilityKitDefaults.Create(UnitRole.Melee, 0);
            Assert.AreEqual(0, kit.Length);
        }

        [Test]
        public void PriestKit_HasFourSlotsWithTextAndNumbers()
        {
            var kit = AbilityKitDefaults.CreatePriest();
            Assert.AreEqual(4, kit.Length);
            Assert.AreEqual(AbilityType.GreaterHeal, kit[0].Type);
            Assert.AreEqual(AbilityType.Revive, kit[1].Type);
            Assert.AreEqual(AbilityType.HolyNova, kit[2].Type);
            Assert.AreEqual(AbilityType.AuraArmorPercent, kit[3].Type);
            Assert.AreEqual(4, kit[0].UnlockValue);
            Assert.AreEqual(1, kit[2].UnlockValue);
            Assert.IsFalse(string.IsNullOrWhiteSpace(kit[2].DisplayName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(kit[2].Description));
            Assert.AreEqual(HeroAbilityRules.NovaDamage, kit[2].Damage);
            Assert.AreEqual(HeroAbilityRules.NovaCastRange, kit[2].CastRange);
            Assert.AreEqual(HeroAbilityRules.GreaterHealHealPerSecond, kit[0].HealPerSecond);
        }

        [Test]
        public void TitanKit_HasSlamRallyColossusStomp()
        {
            var kit = AbilityKitDefaults.CreateTitan();
            Assert.AreEqual(4, kit.Length);
            Assert.AreEqual(AbilityType.Rally, kit[0].Type);
            Assert.AreEqual(AbilityType.Stomp, kit[1].Type);
            Assert.AreEqual(AbilityType.Slam, kit[2].Type);
            Assert.AreEqual(AbilityType.AuraMaxHpPercent, kit[3].Type);
            Assert.AreEqual("Colossus", kit[3].DisplayName);
            Assert.AreEqual(HeroAbilityRules.AuraMaxHpBonusPercent, kit[3].Percent);
        }

        [Test]
        public void CasterKit_UnlocksByMagicLevel()
        {
            var kit = AbilityKitDefaults.CreateCaster();
            Assert.AreEqual(3, kit.Length);
            Assert.AreEqual(AbilityUnlock.MagicLevel, kit[0].Unlock);
            Assert.AreEqual(1, kit[0].UnlockValue);
            Assert.AreEqual(2, kit[1].UnlockValue);
            Assert.AreEqual(3, kit[2].UnlockValue);
        }

        [Test]
        public void HumanHero3Prefab_HasPriestKitWhenSeeded()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Races/Humans/Heroes/Human_Hero3.prefab");
            Assert.IsNotNull(prefab);
            var kit = prefab.GetComponentInChildren<UnitAbilityKit>(true);
            Assert.IsNotNull(kit, "Seed Human_Hero3 via BARAKI/Units/Seed Ability Kits.");

            Assert.AreEqual(4, kit.Slots.Length);
            Assert.AreEqual(AbilityType.HolyNova, kit.Slots[2].Type);
            Assert.IsFalse(string.IsNullOrWhiteSpace(kit.Slots[2].Description));
        }

        [Test]
        public void HumanTitanPrefab_HasTitanKitWhenSeeded()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Races/Humans/Units/Human_Titan.prefab");
            Assert.IsNotNull(prefab);
            var kit = prefab.GetComponentInChildren<UnitAbilityKit>(true);
            Assert.IsNotNull(kit, "Seed Human_Titan via BARAKI/Units/Seed Ability Kits.");

            Assert.AreEqual(4, kit.Slots.Length);
            Assert.AreEqual(AbilityType.Slam, kit.Slots[2].Type);
        }
    }
}
