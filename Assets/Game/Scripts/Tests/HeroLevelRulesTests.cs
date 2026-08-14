using Game.Gameplay.Data;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HeroLevelRulesTests
    {
        [Test]
        public void XpToNext_GrowsWithLevel()
        {
            Assert.AreEqual(100, HeroLevelRules.XpToNext(1));
            Assert.AreEqual(500, HeroLevelRules.XpToNext(5));
            Assert.AreEqual(1000, HeroLevelRules.XpToNext(10));
        }

        [Test]
        public void CanLevelUp_RequiresThresholdAndNotMax()
        {
            Assert.IsFalse(HeroLevelRules.CanLevelUp(1, 99));
            Assert.IsTrue(HeroLevelRules.CanLevelUp(1, 100));
            Assert.IsFalse(HeroLevelRules.CanLevelUp(HeroLevelRules.MaxLevel, 100000));
        }

        [Test]
        public void IsAbilityUnlocked_FollowsUnlockLevels()
        {
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Strike, 1));
            Assert.IsFalse(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Heal, 3));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Heal, 4));
            Assert.IsFalse(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Aura, 6));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Aura, 7));
            Assert.IsFalse(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Ultimate, 9));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Ultimate, 10));

            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Smite, 1));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.HolyNova, 1));
            Assert.IsFalse(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Shield, 3));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Shield, 4));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.GreaterHeal, 4));
            Assert.IsFalse(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Consecration, 9));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Consecration, 10));
            Assert.IsTrue(HeroLevelRules.IsAbilityUnlocked(HeroAbilityType.Revive, 10));
        }

        [Test]
        public void ApplyLevelGrowth_ScalesStatsPerLevel()
        {
            var baseStats = new UnitCombatStats(UnitRole.Hero, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);
            var level1 = HeroLevelRules.ApplyLevelGrowth(baseStats, 1);
            Assert.AreEqual(600f, level1.MaxHp);
            Assert.AreEqual(4f, level1.Armor);
            Assert.AreEqual(35f, level1.DamageMin);
            Assert.AreEqual(45f, level1.DamageMax);

            var level5 = HeroLevelRules.ApplyLevelGrowth(baseStats, 5);
            Assert.AreEqual(600f + 4 * HeroLevelRules.MaxHpPerLevel, level5.MaxHp);
            Assert.AreEqual(4f + 4 * HeroLevelRules.ArmorPerLevel, level5.Armor);
            Assert.AreEqual(35f + 4 * HeroLevelRules.DamageMinPerLevel, level5.DamageMin);
            Assert.AreEqual(45f + 4 * HeroLevelRules.DamageMaxPerLevel, level5.DamageMax);
        }

        [Test]
        public void ApplyLevelGrowth_ClampsBelowStartingLevel()
        {
            var baseStats = new UnitCombatStats(UnitRole.Hero, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);
            var level0 = HeroLevelRules.ApplyLevelGrowth(baseStats, 0);
            Assert.AreEqual(600f, level0.MaxHp);
            Assert.AreEqual(45f, level0.DamageMax);
        }

        [Test]
        public void KillXp_IsTargetBounty()
        {
            Assert.AreEqual(0, HeroLevelRules.GetKillXp(0));
            Assert.AreEqual(80, HeroLevelRules.GetKillXp(80));
        }

        [Test]
        public void BuildingKillXp_IsConstant()
        {
            Assert.AreEqual(HeroLevelRules.XpForBuildingKill, HeroLevelRules.GetBuildingKillXp());
        }
    }
}
