using System.Collections.Generic;
using Game.Editor;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
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
            Assert.AreEqual(AbilityIds.GreaterHeal, kit[0].AbilityId);
            Assert.AreEqual(AbilityIds.Revive, kit[1].AbilityId);
            Assert.AreEqual(AbilityIds.HolyNova, kit[2].AbilityId);
            Assert.AreEqual(AbilityIds.AuraArmorPercent, kit[3].AbilityId);
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
            Assert.AreEqual(AbilityIds.Rally, kit[0].AbilityId);
            Assert.AreEqual(AbilityIds.Stomp, kit[1].AbilityId);
            Assert.AreEqual(AbilityIds.Slam, kit[2].AbilityId);
            Assert.AreEqual(AbilityIds.AuraMaxHpPercent, kit[3].AbilityId);
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
        public void DisplayNames_AreUniqueAcrossAllKits()
        {
            var names = new HashSet<string>();
            foreach (var def in CollectAllDefaults())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.DisplayName), $"Ability id {def.AbilityId} has no display name.");
                Assert.IsTrue(names.Add(def.DisplayName), $"Duplicate ability display name: '{def.DisplayName}'");
            }
        }

        static IEnumerable<UnitAbilityDef> CollectAllDefaults()
        {
            foreach (var kit in new[]
                     {
                         AbilityKitDefaults.CreateKing(),
                         AbilityKitDefaults.CreatePaladin(),
                         AbilityKitDefaults.CreatePriest(),
                         AbilityKitDefaults.CreateTitan(),
                         AbilityKitDefaults.CreateCaster(),
                         AbilityKitDefaults.CreateSiegeRegen(),
                     })
            {
                foreach (var def in kit)
                {
                    if (def != null)
                    {
                        yield return def;
                    }
                }
            }
        }

        [Test]
        public void HumanHero3Prefab_HasPriestKitWhenSeeded()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanHero3Path);
            Assert.IsNotNull(prefab);
            var settings = prefab.GetComponentInChildren<UnitCombatSettings>(true);
            Assert.IsNotNull(settings, "Seed Human_Hero3 via BARAKI/Units/Seed Unit Abilities.");

            Assert.AreEqual(4, settings.Abilities.Length);
            Assert.AreEqual(AbilityIds.HolyNova, settings.Abilities[2].AbilityId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(settings.Abilities[2].Description));
        }

        [Test]
        public void HumanTitanPrefab_HasTitanKitWhenSeeded()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanTitanPath);
            Assert.IsNotNull(prefab);
            var settings = prefab.GetComponentInChildren<UnitCombatSettings>(true);
            Assert.IsNotNull(settings, "Seed Human_Titan via BARAKI/Units/Seed Unit Abilities.");

            Assert.AreEqual(4, settings.Abilities.Length);
            Assert.AreEqual(AbilityIds.Slam, settings.Abilities[2].AbilityId);
        }
    }
}
