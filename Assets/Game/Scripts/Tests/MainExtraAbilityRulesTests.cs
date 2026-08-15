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
        public void IsUnlocked_RespectsStubGates()
        {
            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(1, 0, 0, 0, 0, 0));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(1, 0, 0, 0, 0, 1));

            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(2, 2, 0, 0, 0, 0));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(2, 3, 0, 0, 0, 0));

            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(3, 0, 2, 2, 0, 0));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(3, 0, 3, 0, 0, 0));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(3, 0, 0, 3, 0, 0));

            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(4, 0, 0, 0, 5, 0));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(4, 0, 0, 0, 6, 0));

            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(5, 6, 0, 0, 0, 1));
            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(5, 5, 5, 5, 0, 2));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(5, 6, 0, 0, 0, 2));

            Assert.IsFalse(MainExtraAbilityRules.IsUnlocked(6, 6, 6, 5, 0, 3));
            Assert.IsTrue(MainExtraAbilityRules.IsUnlocked(6, 6, 6, 6, 0, 3));
        }

        [Test]
        public void CanPick_RequiresBlessingCompleteUnlockedAndOnce()
        {
            var player = new MatchPlayerState(0, "RACE_HUMAN", 1000)
            {
                DivineBlessingComplete = false,
                MagicLevel = 1,
            };
            Assert.IsFalse(MainExtraAbilityRules.CanPick(player, 1));

            player.DivineBlessingComplete = true;
            Assert.IsTrue(MainExtraAbilityRules.CanPick(player, 1));

            player.MainExtraAbilityId = 1;
            Assert.IsFalse(MainExtraAbilityRules.CanPick(player, 1));
            Assert.IsFalse(MainExtraAbilityRules.CanPick(player, 2));
        }
    }
}
