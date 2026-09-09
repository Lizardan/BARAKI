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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.Strike, casts[0].Def.AbilityId);
            Assert.AreEqual(hero.UnitId, casts[0].CasterUnitId);
            Assert.AreEqual(0, casts[0].TargetUnitId);
            Assert.AreEqual(UnitBehaviorState.Attack, hero.BehaviorState);
            Assert.Greater(hero.CastLockRemainingSeconds, 0f);
            Assert.IsTrue(hero.CastLockUsesAttackAnim);
            var swingAfterCast = hero.AttackSwingSerial;
            controller.Combat.Tick(0.05f);
            Assert.AreEqual(swingAfterCast, hero.AttackSwingSerial, "Cast lock must block a new auto-attack swing");
        }

        [Test]
        public void CastLockExpiry_ClearsAuthoredAnimOverride_SoHeroResumesWalk()
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
            Assert.Greater(hero.CastLockRemainingSeconds, 0f, "Smite must start a cast lock.");
            Assert.AreEqual(UnitBehaviorState.Attack, hero.BehaviorState);

            // Mirror runtime host kits (e.g. Faceless Hero2 Priest/Smite assets) where the
            // authored def carries a non-empty AnimState the presenter uses as override.
            hero.CastLockAnimState = "Attack";
            hero.CastLockAnimVariant = 1;

            // Let the lock fully expire: the stale override must be cleared, otherwise the
            // presenter keeps forcing the attack animation while the hero marches.
            for (var i = 0; i < 15; i++)
            {
                controller.Combat.Tick(0.1f);
            }

            Assert.LessOrEqual(hero.CastLockRemainingSeconds, 0f, "Cast lock must fully expire.");
            Assert.IsTrue(
                string.IsNullOrEmpty(hero.CastLockAnimState),
                "Authored anim override must be cleared when the cast lock ends.");
            Assert.AreEqual(0, hero.CastLockAnimVariant);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.Heal, casts[0].Def.AbilityId);
            Assert.AreEqual(ally.UnitId, casts[0].TargetUnitId);
            Assert.AreEqual(UnitBehaviorState.Cast, hero.BehaviorState);
            Assert.Greater(hero.CastLockRemainingSeconds, 0f);
            Assert.IsFalse(hero.CastLockUsesAttackAnim);
            Assert.AreEqual(UnitBehaviorState.Cast, hero.BehaviorState);
            controller.Combat.Tick(0.2f);
            Assert.AreEqual(UnitBehaviorState.Cast, hero.BehaviorState, "Heal cast lock must keep Cast until clip ends");
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.Ultimate, casts[0].Def.AbilityId);
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
            Assert.AreEqual(0, controller.Combat.ConsumePendingAbilityCasts().Count);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.Smite, casts[0].Def.AbilityId);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(AbilityIds.Smite, casts[0].Def.AbilityId);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.Shield, casts[0].Def.AbilityId);

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
            Assert.AreEqual(AbilityIds.Smite, controller.Combat.ConsumePendingAbilityCasts()[0].Def.AbilityId);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.Consecration, casts[0].Def.AbilityId);
        }

        [Test]
        public void PaladinAura_BoostsOwnerArmyAttackSpeed()
        {
            var controller = CreateEarlyMatch();
            var paladin = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 5f, isHero: true, heroSlot: 2, level: 7);
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 25f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 30f);
            warrior.WorldPosition = enemy.WorldPosition + new Vector3(1f, 0f, 0f);
            paladin.WorldPosition = warrior.WorldPosition + new Vector3(3f, 0f, 0f);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.HolyNova, casts[0].Def.AbilityId);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(AbilityIds.HolyNova, casts[0].Def.AbilityId);
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

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.GreaterHeal, casts[0].Def.AbilityId);

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
            Assert.AreEqual(AbilityIds.HolyNova, controller.Combat.ConsumePendingAbilityCasts()[0].Def.AbilityId);
        }

        [Test]
        public void PriestAura_BoostsOwnerArmyArmor()
        {
            var controller = CreateEarlyMatch();
            var priest = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 5f, isHero: true, heroSlot: 3, level: 7);
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, ArmoredMeleeStats(),
                distanceAlongLane: 25f);
            var enemy = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 30f);
            priest.WorldPosition = warrior.WorldPosition + new Vector3(2f, 0f, 0f);

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
            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.Revive, casts[0].Def.AbilityId);

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

            Assert.AreEqual(AbilityIds.HolyNova, controller.Combat.ConsumePendingAbilityCasts()[0].Def.AbilityId);
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
            Assert.AreEqual(AbilityIds.Slam, controller.Combat.ConsumePendingAbilityCasts()[0].Def.AbilityId);
        }

        [Test]
        public void TitanColossus_RaisesArmyMaxHpWhileAlive()
        {
            var controller = CreateEarlyMatch();
            var titan = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Titan, TitanStats(),
                distanceAlongLane: 5f, level: 7);
            var warrior = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 25f);
            titan.WorldPosition = warrior.WorldPosition + new Vector3(2f, 0f, 0f);
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
            Assert.AreEqual(AbilityIds.Stomp, controller.Combat.ConsumePendingAbilityCasts()[0].Def.AbilityId);
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

            Assert.AreEqual(0, controller.Combat.ConsumePendingAbilityCasts().Count);
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
            new UnitCombatStats(UnitRole.Titan, 1800f, 12f, 105f, 135f, 1f, 3f, 4f, 80);

        static UnitCombatStats MeleeStats() =>
            new UnitCombatStats(UnitRole.Melee, MeleeMaxHp, 0f, 35f, 45f, 1f, 1.5f, 4f, 80);

        static UnitCombatStats ArmoredMeleeStats() =>
            new UnitCombatStats(UnitRole.Melee, MeleeMaxHp, 10f, 35f, 45f, 1f, 1.5f, 4f, 80);
    }
}
