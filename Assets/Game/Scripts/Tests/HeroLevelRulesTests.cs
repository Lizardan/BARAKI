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
            Assert.IsTrue(IsUnlocked(AbilityIds.Strike, 1));
            Assert.IsFalse(IsUnlocked(AbilityIds.Heal, 3));
            Assert.IsTrue(IsUnlocked(AbilityIds.Heal, 4));
            Assert.IsFalse(IsUnlocked(AbilityIds.AuraDamagePercent, 6));
            Assert.IsTrue(IsUnlocked(AbilityIds.AuraDamagePercent, 7));
            Assert.IsTrue(IsUnlocked(AbilityIds.AuraMaxHpPercent, 7));
            Assert.IsTrue(IsUnlocked(AbilityIds.Slam, 1));
            Assert.IsTrue(IsUnlocked(AbilityIds.Stomp, 10));
            Assert.IsFalse(IsUnlocked(AbilityIds.Ultimate, 9));
            Assert.IsTrue(IsUnlocked(AbilityIds.Ultimate, 10));

            Assert.IsTrue(IsUnlocked(AbilityIds.Smite, 1));
            Assert.IsTrue(IsUnlocked(AbilityIds.HolyNova, 1));
            Assert.IsFalse(IsUnlocked(AbilityIds.Shield, 3));
            Assert.IsTrue(IsUnlocked(AbilityIds.Shield, 4));
            Assert.IsTrue(IsUnlocked(AbilityIds.GreaterHeal, 4));
            Assert.IsFalse(IsUnlocked(AbilityIds.Consecration, 9));
            Assert.IsTrue(IsUnlocked(AbilityIds.Consecration, 10));
            Assert.IsTrue(IsUnlocked(AbilityIds.Revive, 10));
        }

        private static bool IsUnlocked(int abilityId, int level)
        {
            var def = FindDef(abilityId);
            return def != null && def.Unlock == AbilityUnlock.HeroLevel && level >= def.UnlockValue;
        }

        private static UnitAbilityDef FindDef(int abilityId)
        {
            foreach (var role in new[] { UnitRole.Hero, UnitRole.Titan, UnitRole.Caster })
            {
                var slots = role == UnitRole.Hero
                    ? new[] { HeroAbilityRules.KingSlot, HeroAbilityRules.PaladinSlot, HeroAbilityRules.PriestSlot }
                    : new[] { 0 };

                foreach (var slot in slots)
                {
                    var kit = AbilityKitDefaults.Create(role, slot);
                    for (var i = 0; i < kit.Length; i++)
                    {
                        if (kit[i].AbilityId == abilityId)
                        {
                            return kit[i];
                        }
                    }
                }
            }

            return null;
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
