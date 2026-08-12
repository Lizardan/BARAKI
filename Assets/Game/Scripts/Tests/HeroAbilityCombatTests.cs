using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HeroAbilityCombatTests
    {
        [Test]
        public void HeroCastsStrike_OnEnemyInRange()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 1, level: 1);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.StrikeDamage, enemy.CurrentHp, 0.001f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.Strike, casts[0].Ability);
            Assert.AreEqual(hero.UnitId, casts[0].CasterUnitId);
            Assert.AreEqual(0, casts[0].TargetUnitId);
        }

        [Test]
        public void HeroCastsHeal_OnInjuredAllyInRange()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 1, level: 4);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(3f, 0f, 0f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(10f, 0f, 0f);

            var before = ally.CurrentHp;
            ally.CurrentHp = before - 200f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(
                Mathf.Min(MeleeMaxHp, before - 200f + HeroAbilityRules.HealAmount),
                ally.CurrentHp,
                0.001f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.Heal, casts[0].Ability);
            Assert.AreEqual(ally.UnitId, casts[0].TargetUnitId);
        }

        [Test]
        public void HeroCastsUltimate_OnEnemyCluster()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 1, level: 10);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(3f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            var expectedDamage = HeroAbilityRules.UltimateDamage
                                 * (1f + HeroAbilityRules.AuraDamageBonusPercent);
            Assert.AreEqual(MeleeMaxHp - expectedDamage, enemy.CurrentHp, 0.001f);
            Assert.AreEqual(HeroAbilityRules.UltimateSelfBuffSeconds, hero.UltimateBuffRemaining, 0.001f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.Ultimate, casts[0].Ability);
        }

        [Test]
        public void Aura_BoostsOwnerArmyDamageWhileAliveHeroAtUnlockLevel()
        {
            var controller = CreateEarlyMatch();
            controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 1, level: 7);
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 25f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 30f);

            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                warrior.UnitId, enemy.UnitId, 100f, 0.1f));

            var expected = MeleeMaxHp - 100f * (1f + HeroAbilityRules.AuraDamageBonusPercent);
            Assert.AreEqual(expected, enemy.CurrentHp, 0.001f);
        }

        [Test]
        public void Aura_Inactive_WithoutAliveOwnerHero()
        {
            var controller = CreateEarlyMatch();
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 25f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 30f);

            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                warrior.UnitId, enemy.UnitId, 100f, 0.1f));

            Assert.AreEqual(MeleeMaxHp - 100f, enemy.CurrentHp, 0.001f);
        }

        [Test]
        public void HeroBelowHealLevel_DoesNotCastHeal()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 1, level: 1);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);
            ally.CurrentHp -= 200f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - 200f, ally.CurrentHp, 0.001f);
            Assert.AreEqual(0, controller.Combat.ConsumePendingHeroAbilityCasts().Count);
        }

        [Test]
        public void UltimateBuff_BoostsSubsequentHeroDamageWhileActive()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 1, level: 10);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(3f, 0f, 0f);

            controller.Combat.Tick(0.1f);
            var afterUltimate = enemy.CurrentHp;

            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                hero.UnitId, enemy.UnitId, 100f, 0.1f));

            var auraMultiplier = 1f + HeroAbilityRules.AuraDamageBonusPercent;
            var buffMultiplier = 1f + HeroAbilityRules.UltimateSelfDamageBonusPercent;
            Assert.AreEqual(
                afterUltimate - 100f * auraMultiplier * buffMultiplier,
                enemy.CurrentHp,
                0.001f);
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
        }

        const float MeleeMaxHp = 600f;

        static UnitCombatStats HeroStats() =>
            new UnitCombatStats(UnitRole.Hero, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);

        static UnitCombatStats MeleeStats() =>
            new UnitCombatStats(UnitRole.Melee, MeleeMaxHp, 0f, 35f, 45f, 1f, 1.5f, 4f, 80);
    }
}
