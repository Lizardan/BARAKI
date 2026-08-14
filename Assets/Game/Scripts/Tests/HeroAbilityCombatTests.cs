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

        [Test]
        public void PaladinCastsSmite_OnNearestEnemy()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 2, level: 1);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.SmiteDamage, enemy.CurrentHp, 0.001f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.Smite, casts[0].Ability);
            Assert.AreEqual(enemy.UnitId, casts[0].TargetUnitId);
        }

        [Test]
        public void PaladinDoesNotCastKingStrike()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 2, level: 1);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(HeroAbilityType.Smite, casts[0].Ability);
            Assert.AreNotEqual(MeleeMaxHp - HeroAbilityRules.StrikeDamage, enemy.CurrentHp);
        }

        [Test]
        public void PaladinCastsShield_BuffsAlliesWhenEnemyNearby()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 2, level: 4);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(3f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            Assert.Greater(ally.ArmorBuffRemaining, HeroAbilityRules.ShieldDurationSeconds - 0.15f);
            Assert.Greater(hero.ArmorBuffRemaining, HeroAbilityRules.ShieldDurationSeconds - 0.15f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.Shield, casts[0].Ability);

            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                enemy.UnitId, ally.UnitId, 100f, 0.1f));
            Assert.AreEqual(MeleeMaxHp - CombatRules.ApplyArmor(100f, HeroAbilityRules.ShieldArmorBonus), ally.CurrentHp, 0.001f);
        }

        [Test]
        public void PaladinBelowShieldLevel_DoesNotCastShield()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 2, level: 1);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(0f, hero.ArmorBuffRemaining, 0.001f);
            Assert.AreEqual(HeroAbilityType.Smite, controller.Combat.ConsumePendingHeroAbilityCasts()[0].Ability);
        }

        [Test]
        public void PaladinCastsConsecration_DamagesAndStuns()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 2, level: 10);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(3f, 0f, 0f);
            hero.ArmorBuffRemaining = 1f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.ConsecrationDamage, enemy.CurrentHp, 0.001f);
            Assert.Greater(enemy.FrozenRemainingSeconds, HeroAbilityRules.ConsecrationStunSeconds - 0.15f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.Consecration, casts[0].Ability);
        }

        [Test]
        public void PaladinAura_BoostsOwnerArmyAttackSpeed()
        {
            var controller = CreateEarlyMatch();
            controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 5f, isHero: true, heroSlot: 2, level: 7);
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 25f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 30f);
            warrior.WorldPosition = enemy.WorldPosition + new Vector3(1f, 0f, 0f);
            warrior.CurrentTargetId = enemy.UnitId;
            warrior.AttackCooldownRemaining = 0f;

            controller.Combat.Tick(0.05f);

            var expected = CombatRules.GetAttackIntervalSeconds(
                1f * (1f + HeroAbilityRules.AuraAttackSpeedBonusPercent));
            Assert.AreEqual(expected, warrior.AttackCooldownRemaining, 0.001f);
        }

        [Test]
        public void PaladinAura_DoesNotGrantKingDamageAura()
        {
            var controller = CreateEarlyMatch();
            controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 2, level: 7);
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
        public void PriestCastsHolyNova_DamagesEnemiesAndHealsAllies()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 1);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(2.5f, 0f, 0f);
            ally.CurrentHp = MeleeMaxHp - 200f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - 200f + HeroAbilityRules.NovaHealAmount, ally.CurrentHp, 0.001f);
            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.NovaDamage, enemy.CurrentHp, 0.001f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.HolyNova, casts[0].Ability);
            Assert.AreEqual(ally.UnitId, casts[0].TargetUnitId);
            Assert.AreEqual(ally.WorldPosition.x, casts[0].CenterPosition.x, 0.01f);
            Assert.AreEqual(ally.WorldPosition.z, casts[0].CenterPosition.z, 0.01f);
        }

        [Test]
        public void PriestCastsHolyNova_OnDistantAlly_DoesNotHitEnemyAtFeet()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 1);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            var farEnemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            var nearEnemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 22f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(8f, 0f, 0f);
            farEnemy.WorldPosition = hero.WorldPosition + new Vector3(8.5f, 0f, 0f);
            nearEnemy.WorldPosition = hero.WorldPosition + new Vector3(1f, 0f, 0f);
            ally.CurrentHp = MeleeMaxHp - 200f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - 200f + HeroAbilityRules.NovaHealAmount, ally.CurrentHp, 0.001f);
            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.NovaDamage, farEnemy.CurrentHp, 0.001f);
            Assert.AreEqual(MeleeMaxHp, nearEnemy.CurrentHp, 0.001f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(HeroAbilityType.HolyNova, casts[0].Ability);
            Assert.AreEqual(ally.UnitId, casts[0].TargetUnitId);
        }

        [Test]
        public void PriestCastsGreaterHeal_OnInjuredAlly()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 4);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(3f, 0f, 0f);
            ally.CurrentHp = MeleeMaxHp - 200f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(
                MeleeMaxHp - 200f + HeroAbilityRules.GreaterHealHealPerSecond * 0.1f,
                ally.CurrentHp,
                0.001f);

            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.GreaterHeal, casts[0].Ability);

            hero.AbilityCooldownRemaining[2] = 99f;
            controller.Combat.Tick(1f);
            Assert.AreEqual(
                MeleeMaxHp - 200f + HeroAbilityRules.GreaterHealHealPerSecond * 1.1f,
                ally.CurrentHp,
                0.05f);
        }

        [Test]
        public void PriestGreaterHeal_DoesNotHealAllyOutsideZone()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 4);
            var inside = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            var outside = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 30f);
            inside.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);
            outside.WorldPosition = hero.WorldPosition + new Vector3(20f, 0f, 0f);
            inside.CurrentHp = MeleeMaxHp - 200f;
            outside.CurrentHp = MeleeMaxHp - 200f;

            controller.Combat.Tick(0.5f);

            Assert.Greater(inside.CurrentHp, MeleeMaxHp - 200f);
            Assert.AreEqual(MeleeMaxHp - 200f, outside.CurrentHp, 0.001f);
        }

        [Test]
        public void PriestBelowGreaterHealLevel_DoesNotCastGreaterHeal()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 1);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);
            ally.CurrentHp = MeleeMaxHp - 200f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - 200f + HeroAbilityRules.NovaHealAmount, ally.CurrentHp, 0.001f);
            Assert.AreEqual(HeroAbilityType.HolyNova, controller.Combat.ConsumePendingHeroAbilityCasts()[0].Ability);
        }

        [Test]
        public void PriestAura_BoostsOwnerArmyArmor()
        {
            var controller = CreateEarlyMatch();
            controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 5f, isHero: true, heroSlot: 3, level: 7);
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, ArmoredMeleeStats(),
                distanceAlongLane: 25f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 30f);

            controller.Combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                enemy.UnitId, warrior.UnitId, 100f, 0.1f));

            var expectedArmor = 10f * (1f + HeroAbilityRules.AuraArmorBonusPercent);
            Assert.AreEqual(600f - CombatRules.ApplyArmor(100f, expectedArmor), warrior.CurrentHp, 0.001f);
        }

        [Test]
        public void PriestCastsRevive_OnAlliedCorpse()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 10);
            var ally = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            ally.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);

            controller.Combat.ApplyExternalDamage(ally.UnitId, 1000f, killerOwnerSlot: 1);
            Assert.AreEqual(1, controller.Combat.Corpses.Count);

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(0, controller.Combat.Corpses.Count);
            var casts = controller.Combat.ConsumePendingHeroAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(HeroAbilityType.Revive, casts[0].Ability);

            var revived = controller.Combat.GetUnit(casts[0].TargetUnitId);
            Assert.IsNotNull(revived);
            Assert.AreEqual(UnitRole.Melee, revived.Role);
            Assert.AreEqual(MeleeMaxHp, revived.CurrentHp, 0.001f);
        }

        [Test]
        public void PriestDoesNotCastKingStrike()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 1);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = hero.WorldPosition + new Vector3(2f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(HeroAbilityType.HolyNova, controller.Combat.ConsumePendingHeroAbilityCasts()[0].Ability);
            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.NovaDamage, enemy.CurrentHp, 0.001f);
        }

        [Test]
        public void TitanCastsSlam_OnEnemyInRange()
        {
            var controller = CreateEarlyMatch();
            var titan = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Titan, TitanStats(),
                distanceAlongLane: 20f, level: 1);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = titan.WorldPosition + new Vector3(3f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.SlamDamage, enemy.CurrentHp, 0.001f);
            Assert.AreEqual(HeroAbilityType.Slam, controller.Combat.ConsumePendingHeroAbilityCasts()[0].Ability);
        }

        [Test]
        public void TitanColossus_RaisesArmyMaxHpWhileAlive()
        {
            var controller = CreateEarlyMatch();
            controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Titan, TitanStats(),
                distanceAlongLane: 5f, level: 7);
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 25f);
            warrior.CurrentHp = 800f;

            controller.Combat.Tick(0.01f);

            var expectedMax = MeleeMaxHp * (1f + HeroAbilityRules.AuraMaxHpBonusPercent);
            Assert.AreEqual(expectedMax, warrior.CurrentHp, 0.001f);
        }

        [Test]
        public void TitanCastsStomp_DamagesAndStuns()
        {
            var controller = CreateEarlyMatch();
            var titan = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Titan, TitanStats(),
                distanceAlongLane: 20f, level: 10);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = titan.WorldPosition + new Vector3(3f, 0f, 0f);
            titan.ArmorBuffRemaining = 1f;

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(MeleeMaxHp - HeroAbilityRules.StompDamage, enemy.CurrentHp, 0.001f);
            Assert.Greater(enemy.FrozenRemainingSeconds, HeroAbilityRules.StompStunSeconds - 0.15f);
            Assert.AreEqual(HeroAbilityType.Stomp, controller.Combat.ConsumePendingHeroAbilityCasts()[0].Ability);
        }

        [Test]
        public void MeleeWithoutKit_DoesNotCastAbilities()
        {
            var controller = CreateEarlyMatch();
            var melee = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 20f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            enemy.WorldPosition = melee.WorldPosition + new Vector3(2f, 0f, 0f);

            controller.Combat.Tick(0.1f);

            Assert.AreEqual(0, controller.Combat.ConsumePendingHeroAbilityCasts().Count);
            Assert.AreEqual(0, melee.Abilities.Length);
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

        static UnitCombatStats TitanStats() =>
            new UnitCombatStats(UnitRole.Titan, 1800f, 12f, 105f, 135f, 1f, 1.5f, 4f, 80);

        static UnitCombatStats MeleeStats() =>
            new UnitCombatStats(UnitRole.Melee, MeleeMaxHp, 0f, 35f, 45f, 1f, 1.5f, 4f, 80);

        static UnitCombatStats ArmoredMeleeStats() =>
            new UnitCombatStats(UnitRole.Melee, MeleeMaxHp, 10f, 35f, 45f, 1f, 1.5f, 4f, 80);
    }
}
