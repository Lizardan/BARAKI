using System.Collections.Generic;
using Game.Core;
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
            var seen = new HashSet<int>();
            foreach (var kit in new[]
                     {
                         AbilityKitDefaults.CreateKing(),
                         AbilityKitDefaults.CreatePaladin(),
                         AbilityKitDefaults.CreatePriest(),
                         AbilityKitDefaults.CreateTitan(),
                         AbilityKitDefaults.CreateCaster(),
                         AbilityKitDefaults.CreateMeleeBonus(),
                         AbilityKitDefaults.CreateRangedBonus(),
                         AbilityKitDefaults.CreateCasterBonus(),
                         AbilityKitDefaults.CreateSiegeRegen(),
                         AbilityKitDefaults.CreateFlyingBonus(),
                         AbilityKitDefaults.CreateSuperBonus(),
                     })
            {
                foreach (var def in kit)
                {
                    if (def != null && seen.Add(def.AbilityId))
                    {
                        yield return def;
                    }
                }
            }
        }

        [Test]
        public void BonusKits_HaveAlwaysUnlockedPassives()
        {
            Assert.AreEqual(AbilityIds.MeleeCleave, AbilityKitDefaults.CreateMeleeBonus()[0].AbilityId);
            Assert.AreEqual(AbilityUnlock.Always, AbilityKitDefaults.CreateMeleeBonus()[0].Unlock);
            Assert.AreEqual(4, AbilityKitDefaults.CreateCasterBonus().Length);
            Assert.AreEqual(AbilityIds.CasterHybrid, AbilityKitDefaults.CreateCasterBonus()[^1].AbilityId);
            Assert.AreEqual(AbilityIds.SuperCatapult, AbilityKitDefaults.CreateBonus(UnitRole.Super)[0].AbilityId);
        }

        [Test]
        public void CreateForSpawn_UsesBonusKitWhenBonusSlotMatchesRole()
        {
            var kit = AbilityKitDefaults.CreateForSpawn(
                UnitRole.Ranged,
                heroSlot: 0,
                bonusSlot: HumanBonusUnitRules.BonusSlotForRole(UnitRole.Ranged));
            Assert.AreEqual(1, kit.Length);
            Assert.AreEqual(AbilityIds.RangedCrit, kit[0].AbilityId);

            var baseKit = AbilityKitDefaults.CreateForSpawn(UnitRole.Ranged, 0, bonusSlot: 0);
            Assert.AreEqual(0, baseKit.Length);
        }

        [Test]
        public void CreateForSpawn_Faceless_ReturnsEmptyKit()
        {
            Assert.IsFalse(HumanBonusUnitRules.HasBonusKit(GameIds.Races.Faceless));

            foreach (var role in new[] { UnitRole.Melee, UnitRole.Ranged, UnitRole.Caster, UnitRole.Titan })
            {
                var kit = AbilityKitDefaults.CreateForSpawn(
                    GameIds.Races.Faceless, role, heroSlot: 1, bonusSlot: 0);
                Assert.AreEqual(0, kit.Length, $"Faceless {role} must not inherit Human kit.");
            }

            var casterKit = AbilityKitDefaults.CreateForSpawn(GameIds.Races.Faceless, UnitRole.Caster, 0, 0);
            Assert.AreEqual(0, casterKit.Length);
        }

        [Test]
        public void EffectiveBonusSlots_Faceless_AlwaysZero()
        {
            Assert.IsFalse(HumanBonusUnitRules.HasBonusKit(GameIds.Races.Faceless));
            Assert.IsTrue(HumanBonusUnitRules.HasBonusKit(GameIds.Races.Human));

            Assert.AreEqual(0, HumanBonusUnitRules.EffectiveBonusSlotForRole(
                GameIds.Races.Faceless, HumanBonusUnitRules.BonusSlotForRole(UnitRole.Melee), UnitRole.Melee));
            Assert.AreEqual(0, HumanBonusUnitRules.EffectiveBonusSlotForHero(GameIds.Races.Faceless, 8, 2));
            Assert.AreEqual(0, HumanBonusUnitRules.EffectiveBonusSlotForTitan(GameIds.Races.Faceless, 10));

            Assert.AreEqual(HumanBonusUnitRules.BonusSlotForRole(UnitRole.Melee),
                HumanBonusUnitRules.EffectiveBonusSlotForRole(
                    GameIds.Races.Human, HumanBonusUnitRules.BonusSlotForRole(UnitRole.Melee), UnitRole.Melee));
        }

        [Test]
        public void HumanMeleeBonusPrefab_HasCleaveWhenSeeded()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanMeleeBonusPath);
            Assert.IsNotNull(prefab);
            var settings = prefab.GetComponentInChildren<UnitCombatSettings>(true);
            Assert.IsNotNull(settings, "Seed Human_Melee_BONUS via BARAKI/Units/Seed Unit Abilities.");
            Assert.AreEqual(1, settings.Abilities.Length);
            Assert.AreEqual(AbilityIds.MeleeCleave, settings.Abilities[0].AbilityId);
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
