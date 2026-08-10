using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RaceUpgradeStatsRulesTests
    {
        static readonly UnitCombatStats MeleeBase = new(
            UnitRole.Melee, 100f, 2f, 10f, 14f, 1f, 1.5f, 3.5f, 20);

        [Test]
        public void Apply_NoUpgrades_ReturnsBaseStats()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Human, 500);

            var result = RaceUpgradeStatsRules.Apply(MeleeBase, player);

            Assert.AreEqual(100f, result.MaxHp);
            Assert.AreEqual(2f, result.Armor);
            Assert.AreEqual(10f, result.DamageMin, 0.01f);
            Assert.AreEqual(14f, result.DamageMax, 0.01f);
        }

        [Test]
        public void Apply_NullPlayer_ReturnsBaseStats()
        {
            var result = RaceUpgradeStatsRules.Apply(MeleeBase, null);
            Assert.AreEqual(10f, result.DamageMin, 0.01f);
            Assert.AreEqual(100f, result.MaxHp);
        }

        [Test]
        public void Apply_MeleeLevel_AddsPercentDamage()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Human, 500) { MeleeDamageLevel = 3 };

            var result = RaceUpgradeStatsRules.Apply(MeleeBase, player);

            Assert.AreEqual(10.9f, result.DamageMin, 0.01f);
            Assert.AreEqual(15.26f, result.DamageMax, 0.01f);
            Assert.AreEqual(100f, result.MaxHp);
        }

        [Test]
        public void Apply_RangedLevel_DoesNotAffectMeleeDamage()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Human, 500) { RangedDamageLevel = 9 };

            var result = RaceUpgradeStatsRules.Apply(MeleeBase, player);

            Assert.AreEqual(10f, result.DamageMin, 0.01f);
            Assert.AreEqual(14f, result.DamageMax, 0.01f);
        }

        [Test]
        public void Apply_HpArmorLevel_AddsFlatHpAndArmor()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Human, 500) { HpArmorLevel = 2 };

            var result = RaceUpgradeStatsRules.Apply(MeleeBase, player);

            Assert.AreEqual(150f, result.MaxHp);
            Assert.AreEqual(6f, result.Armor);
            Assert.AreEqual(10f, result.DamageMin, 0.01f);
        }

        [Test]
        public void Apply_HeroGetsHpArmorButNotRoleDamage()
        {
            var heroBase = new UnitCombatStats(
                UnitRole.Hero, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);
            var player = new MatchPlayerState(0, GameIds.Races.Human, 500)
            {
                HpArmorLevel = 1,
                MeleeDamageLevel = 5,
            };

            var result = RaceUpgradeStatsRules.Apply(heroBase, player);

            Assert.AreEqual(625f, result.MaxHp);
            Assert.AreEqual(6f, result.Armor);
            Assert.AreEqual(35f, result.DamageMin, 0.01f);
            Assert.AreEqual(45f, result.DamageMax, 0.01f);
        }

        [Test]
        public void Apply_CasterLevel_AddsFlatDamage()
        {
            var casterBase = new UnitCombatStats(
                UnitRole.Caster, 60f, 1f, 5f, 7f, 1f, 6f, 3f, 40, maxMana: 200f);
            var player = new MatchPlayerState(0, GameIds.Races.Human, 500) { MagicLevel = 3 };

            var result = RaceUpgradeStatsRules.Apply(casterBase, player);

            Assert.AreEqual(14f, result.DamageMin, 0.01f);
            Assert.AreEqual(16f, result.DamageMax, 0.01f);
            Assert.AreEqual(60f, result.MaxHp);
            Assert.AreEqual(200f, result.MaxMana);
        }
    }
}
