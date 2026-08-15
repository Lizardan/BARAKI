using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MainExtraAbilityRulesTests
    {
        [Test]
        public void IsValidId_AcceptsOneThroughSix()
        {
            Assert.IsFalse(MainExtraAbilityRules.IsValidId(0));
            Assert.IsTrue(MainExtraAbilityRules.IsValidId(1));
            Assert.IsTrue(MainExtraAbilityRules.IsValidId(6));
            Assert.IsFalse(MainExtraAbilityRules.IsValidId(7));
        }

        [Test]
        public void IsUnlocked_RequiresCombatGateForImplementedAbilities()
        {
            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(1, 6, 7, 7, 0, 2));
            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(1, 7, 7, 7, 0, 1));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(1, 7, 7, 7, 0, 2));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(2, 7, 7, 7, 0, 2));
            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(3, 9, 9, 9, 0, 3));
            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(6, 9, 9, 9, 0, 3));
        }

        [Test]
        public void CanPick_RequiresBlessingCompleteUnlockedAndOnce()
        {
            var player = new MatchPlayerState(0, "RACE_HUMAN", 1000)
            {
                DivineBlessingComplete = false,
                MeleeDamageLevel = 7,
                RangedDamageLevel = 7,
                HpArmorLevel = 7,
                MagicLevel = 2,
            };
            Assert.IsFalse(MainExtraAbilityRules.CanPick(player, 1));

            player.DivineBlessingComplete = true;
            Assert.IsTrue(MainExtraAbilityRules.CanPick(player, 1));
            Assert.IsTrue(MainExtraAbilityRules.CanPick(player, 2));
            Assert.IsFalse(MainExtraAbilityRules.CanPick(player, 3));

            player.MainExtraAbilityId = 1;
            Assert.IsFalse(MainExtraAbilityRules.CanPick(player, 1));
            Assert.IsFalse(MainExtraAbilityRules.CanPick(player, 2));
        }

        [Test]
        public void Tuning_MatchesDesignNumbers()
        {
            Assert.AreEqual(1200f, MainExtraAbilityRules.BuildingSmiteDamage);
            Assert.AreEqual(5000f, MainExtraAbilityRules.UnitSmiteDamage);
            Assert.AreEqual(180f, MainExtraAbilityRules.CooldownSeconds);
            Assert.AreEqual(200f, MainExtraAbilityRules.ManaCost);
            Assert.AreEqual(200f, MainExtraAbilityRules.GetMainManaMax(2));
            Assert.AreEqual(300f, MainExtraAbilityRules.GetMainManaMax(3));
        }
    }
}
