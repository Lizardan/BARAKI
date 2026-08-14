using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TitanRulesTests
    {
        [Test]
        public void Constants_MatchGdd()
        {
            Assert.AreEqual(180f, TitanRules.ResearchSeconds);
            Assert.AreEqual(2500, TitanRules.DeployGold);
            Assert.AreEqual(300f, TitanRules.DeathCooldownSeconds);
            Assert.AreEqual(3, TitanRules.RequiredMainLevel);
            Assert.AreEqual(3, TitanRules.RequiredHeroesHired);
            Assert.AreEqual(3, TitanRules.RequiredHeroesIdleAtBase);
            Assert.AreEqual(3f, TitanRules.BaseStatMultiplier);
        }

        [Test]
        public void AreResearchGatesMet_RequiresMainLevelThree()
        {
            var roster = CreateAllIdleRoster();
            var player = new MatchPlayerState(0, GameIds.Races.Human, 0);
            player.MainLevel = 2;
            Assert.IsFalse(TitanRules.AreResearchGatesMet(player, roster));
            player.MainLevel = 3;
            Assert.IsTrue(TitanRules.AreResearchGatesMet(player, roster));
        }

        [Test]
        public void AreResearchGatesMet_RequiresAllHeroesHired()
        {
            var roster = new HeroRosterState();
            roster.Get(1).State = HeroLifecycleState.IdleAtBase;
            roster.Get(2).State = HeroLifecycleState.IdleAtBase;
            var player = new MatchPlayerState(0, GameIds.Races.Human, 0);
            player.MainLevel = 3;
            Assert.IsFalse(TitanRules.AreResearchGatesMet(player, roster));
            roster.Get(3).State = HeroLifecycleState.IdleAtBase;
            Assert.IsTrue(TitanRules.AreResearchGatesMet(player, roster));
        }

        [Test]
        public void AreResearchGatesMet_RequiresAllHeroesIdleAtBase()
        {
            var roster = CreateAllIdleRoster();
            var player = new MatchPlayerState(0, GameIds.Races.Human, 0);
            player.MainLevel = 3;
            Assert.IsTrue(TitanRules.AreResearchGatesMet(player, roster));
            roster.Get(2).State = HeroLifecycleState.Deployed;
            Assert.IsFalse(TitanRules.AreResearchGatesMet(player, roster));
            roster.Get(2).State = HeroLifecycleState.Dead;
            Assert.IsFalse(TitanRules.AreResearchGatesMet(player, roster));
        }

        [Test]
        public void CanDeploy_RequiresIdleAtBaseOrDead_NoCooldown_Gold_Barracks()
        {
            Assert.IsTrue(TitanRules.CanDeploy(TitanLifecycleState.IdleAtBase, 0f, 2500, true));
            Assert.IsTrue(TitanRules.CanDeploy(TitanLifecycleState.Dead, 0f, 2500, true));
            Assert.IsFalse(TitanRules.CanDeploy(TitanLifecycleState.Locked, 0f, 2500, true));
            Assert.IsFalse(TitanRules.CanDeploy(TitanLifecycleState.Deployed, 0f, 2500, true));
            Assert.IsFalse(TitanRules.CanDeploy(TitanLifecycleState.IdleAtBase, 10f, 2500, true));
            Assert.IsFalse(TitanRules.CanDeploy(TitanLifecycleState.IdleAtBase, 0f, 2499, true));
            Assert.IsFalse(TitanRules.CanDeploy(TitanLifecycleState.IdleAtBase, 0f, 2500, false));
        }

        [Test]
        public void ShouldShowResearchBar_WhileLockedWithProgressOrGates()
        {
            Assert.IsTrue(TitanRules.ShouldShowResearchBar(TitanLifecycleState.Locked, 0f, gatesMet: true));
            Assert.IsTrue(TitanRules.ShouldShowResearchBar(TitanLifecycleState.Locked, 10f, gatesMet: false));
            Assert.IsFalse(TitanRules.ShouldShowResearchBar(TitanLifecycleState.Locked, 0f, gatesMet: false));
            Assert.IsFalse(TitanRules.ShouldShowResearchBar(TitanLifecycleState.IdleAtBase, 180f, gatesMet: true));
            Assert.IsFalse(TitanRules.ShouldShowResearchBar(TitanLifecycleState.Deployed, 180f, gatesMet: true));
        }

        [Test]
        public void ScaleForTitan_MultipliesCombatStatsByThree()
        {
            var hero = new UnitCombatStats(UnitRole.Hero, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);
            var titan = TitanRules.ScaleForTitan(hero);
            Assert.AreEqual(UnitRole.Titan, titan.Role);
            Assert.AreEqual(1800f, titan.MaxHp, 0.001f);
            Assert.AreEqual(12f, titan.Armor, 0.001f);
            Assert.AreEqual(105f, titan.DamageMin, 0.001f);
            Assert.AreEqual(135f, titan.DamageMax, 0.001f);
            Assert.AreEqual(1f, titan.AttackSpeed, 0.001f);
            Assert.AreEqual(1.5f, titan.AttackRange, 0.001f);
            Assert.AreEqual(4f, titan.MoveSpeed, 0.001f);
            Assert.AreEqual(80, titan.GoldBounty);
        }

        static HeroRosterState CreateAllIdleRoster()
        {
            var roster = new HeroRosterState();
            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                roster.Get(slot).State = HeroLifecycleState.IdleAtBase;
            }

            return roster;
        }
    }
}
