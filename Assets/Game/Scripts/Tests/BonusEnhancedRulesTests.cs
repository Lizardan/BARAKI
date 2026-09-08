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
    public sealed class BonusEnhancedRulesTests
    {
        [Test]
        public void BonusSlotMapping_IsSymmetricForUnitRoles()
        {
            Assert.AreEqual(1, BonusKitRules.BonusSlotForRole(UnitRole.Melee));
            Assert.AreEqual(2, BonusKitRules.BonusSlotForRole(UnitRole.Ranged));
            Assert.AreEqual(3, BonusKitRules.BonusSlotForRole(UnitRole.Caster));
            Assert.AreEqual(4, BonusKitRules.BonusSlotForRole(UnitRole.Siege));
            Assert.AreEqual(5, BonusKitRules.BonusSlotForRole(UnitRole.Flying));
            Assert.AreEqual(6, BonusKitRules.BonusSlotForRole(UnitRole.Super));
            Assert.AreEqual(0, BonusKitRules.BonusSlotForRole(UnitRole.Hero));

            for (var slot = 1; slot <= 6; slot++)
            {
                Assert.IsTrue(BonusKitRules.IsBonusSlot(slot));
                Assert.AreEqual(slot, BonusKitRules.BonusSlotForRole(BonusKitRules.RoleForBonusSlot(slot)));
            }

            Assert.IsFalse(BonusKitRules.IsBonusSlot(0));
            Assert.IsFalse(BonusKitRules.IsBonusSlot(7));
        }

        [Test]
        public void ResolveBase_BonusSlot_UsesBonusDefinitionStats()
        {
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(RaceContentBuilder.CatalogPath);
            Assume.That(raceCatalog != null, "RaceCatalog missing — run RaceContentBuilder.EnsureContent.");
            var catalog = new RaceCatalogCombatCatalog(raceCatalog);
            var race = raceCatalog.GetRace(GameIds.Races.Human);
            Assume.That(race != null);
            Assume.That(race.GetUnitBonus(UnitRole.Melee) != null, "Bonus defs missing — run EnsureContent.");

            Assert.AreEqual(100f, ResolveBonus(catalog, UnitRole.Melee).MaxHp, 0.01f);
            Assert.AreEqual(2f, ResolveBonus(catalog, UnitRole.Melee).AttackRange, 0.01f);
            Assert.AreEqual(1f, ResolveBonus(catalog, UnitRole.Ranged).Armor, 0.01f);
            Assert.AreEqual(80f, ResolveBonus(catalog, UnitRole.Caster).MaxHp, 0.01f);
            Assert.AreEqual(250f, ResolveBonus(catalog, UnitRole.Siege).MaxHp, 0.01f);
            Assert.AreEqual(100f, ResolveBonus(catalog, UnitRole.Flying).MaxHp, 0.01f);
            Assert.AreEqual(12f, ResolveBonus(catalog, UnitRole.Super).AttackRange, 0.01f);
        }

        [Test]
        public void Resolve_BonusSlot_StacksWithRaceUpgrades()
        {
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(RaceContentBuilder.CatalogPath);
            Assume.That(raceCatalog != null);
            Assume.That(raceCatalog.GetRace(GameIds.Races.Human)?.GetUnitBonus(UnitRole.Melee) != null);
            var catalog = new RaceCatalogCombatCatalog(raceCatalog);

            var player = new MatchPlayerState(0, GameIds.Races.Human, 100)
            {
                MeleeDamageLevel = 2,
                HpArmorLevel = 1,
            };
            var stats = UnitStatsResolver.Resolve(
                catalog,
                visualCatalog: null,
                GameIds.Races.Human,
                UnitRole.Melee,
                player,
                bonusSlot: 1);

            var expectedHp = 100f + MatchEconomyRules.UpgradeHpPerLevel;
            var damageMult = 1f + 2 * MatchEconomyRules.MeleeDamagePercentPerLevel;
            Assert.AreEqual(expectedHp, stats.MaxHp, 0.01f);
            Assert.AreEqual(8f * damageMult, stats.DamageMin, 0.01f);
            Assert.AreEqual(10f * damageMult, stats.DamageMax, 0.01f);
        }

        static UnitCombatStats ResolveBonus(ICombatUnitCatalog catalog, UnitRole role) =>
            UnitStatsResolver.ResolveBase(
                catalog,
                visualCatalog: null,
                GameIds.Races.Human,
                role,
                bonusSlot: BonusKitRules.BonusSlotForRole(role));
    }
}
