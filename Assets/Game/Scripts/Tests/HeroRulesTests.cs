using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HeroRulesTests
    {
        [Test]
        public void GetMaxHiredHeroes_FollowsMainLevel()
        {
            Assert.AreEqual(0, HeroRules.GetMaxHiredHeroes(0));
            Assert.AreEqual(1, HeroRules.GetMaxHiredHeroes(1));
            Assert.AreEqual(2, HeroRules.GetMaxHiredHeroes(2));
            Assert.AreEqual(3, HeroRules.GetMaxHiredHeroes(3));
            Assert.AreEqual(3, HeroRules.GetMaxHiredHeroes(5));
        }

        [Test]
        public void CanHire_RequiresGoldSlotAndNoneState()
        {
            Assert.IsTrue(HeroRules.CanHire(HeroLifecycleState.None, heroSlot: 1, mainLevel: 1, gold: 500));
            Assert.IsFalse(HeroRules.CanHire(HeroLifecycleState.None, heroSlot: 1, mainLevel: 1, gold: 499));
            Assert.IsFalse(HeroRules.CanHire(HeroLifecycleState.IdleAtBase, heroSlot: 1, mainLevel: 1, gold: 500));
            Assert.IsFalse(HeroRules.CanHire(HeroLifecycleState.None, heroSlot: 2, mainLevel: 1, gold: 500));
            Assert.IsTrue(HeroRules.CanHire(HeroLifecycleState.None, heroSlot: 2, mainLevel: 2, gold: 500));
        }

        [Test]
        public void ShouldShowHire_UnlocksByMainLevel_DoesNotRequirePreviousSlot()
        {
            Assert.IsTrue(HeroRules.ShouldShowHire(HeroLifecycleState.None, heroSlot: 1, mainLevel: 1));
            Assert.IsFalse(HeroRules.ShouldShowHire(HeroLifecycleState.None, heroSlot: 2, mainLevel: 1));
            Assert.IsTrue(HeroRules.ShouldShowHire(HeroLifecycleState.None, heroSlot: 2, mainLevel: 2));
            Assert.IsFalse(HeroRules.ShouldShowHire(HeroLifecycleState.None, heroSlot: 3, mainLevel: 2));
            Assert.IsTrue(HeroRules.ShouldShowHire(HeroLifecycleState.None, heroSlot: 3, mainLevel: 3));
            Assert.IsFalse(HeroRules.ShouldShowHire(HeroLifecycleState.IdleAtBase, heroSlot: 1, mainLevel: 3));
        }

        [Test]
        public void CanDeploy_RequiresIdleOrDeadReadyAndBarracks()
        {
            Assert.IsTrue(HeroRules.CanDeploy(HeroLifecycleState.IdleAtBase, 0f, 1000, true));
            Assert.IsTrue(HeroRules.CanDeploy(HeroLifecycleState.Dead, 0f, 1000, true));
            Assert.IsFalse(HeroRules.CanDeploy(HeroLifecycleState.Dead, 10f, 1000, true));
            Assert.IsFalse(HeroRules.CanDeploy(HeroLifecycleState.Deployed, 0f, 1000, true));
            Assert.IsFalse(HeroRules.CanDeploy(HeroLifecycleState.IdleAtBase, 0f, 999, true));
            Assert.IsFalse(HeroRules.CanDeploy(HeroLifecycleState.IdleAtBase, 0f, 1000, false));
        }

        [Test]
        public void ShouldShowDeploy_IdleOrDeadOnly()
        {
            Assert.IsTrue(HeroRules.ShouldShowDeploy(HeroLifecycleState.IdleAtBase));
            Assert.IsTrue(HeroRules.ShouldShowDeploy(HeroLifecycleState.Dead));
            Assert.IsFalse(HeroRules.ShouldShowDeploy(HeroLifecycleState.None));
            Assert.IsFalse(HeroRules.ShouldShowDeploy(HeroLifecycleState.Deployed));
        }

        [Test]
        public void HireUpgradeId_RoundTrip()
        {
            var id = HeroRules.BuildHireUpgradeId(2);
            Assert.IsTrue(HeroRules.TryParseHireUpgradeId(id, out var slot));
            Assert.AreEqual(2, slot);
            Assert.AreEqual(25f, HeroRules.HireResearchSeconds);
        }
    }
}

