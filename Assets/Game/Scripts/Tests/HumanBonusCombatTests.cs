using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HumanBonusCombatTests
    {
        [Test]
        public void MeleeBonus_OnHitAoe_DamagesNearbyEnemiesOnly()
        {
            var combat = CreateCombat();
            var attacker = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 20f, bonusSlot: 1);
            var primary = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 22f);
            var nearbyEnemy = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 22f);
            var ally = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 22f);
            primary.WorldPosition = attacker.WorldPosition + new Vector3(1.5f, 0f, 0f);
            nearbyEnemy.WorldPosition = primary.WorldPosition + new Vector3(0.5f, 0f, 0f);
            ally.WorldPosition = primary.WorldPosition + new Vector3(0f, 0f, 0.5f);

            var nearbyBefore = nearbyEnemy.CurrentHp;
            var allyBefore = ally.CurrentHp;
            const float raw = 20f;

            combat.ApplyDamage(attacker, primary, raw, attacker.OwnerSlot);
            combat.ApplySplashDamage(
                attacker,
                primary.WorldPosition,
                raw,
                HumanBonusUnitRules.MeleeAoeRadius,
                attacker.OwnerSlot,
                excludeUnitId: primary.UnitId);

            Assert.Less(nearbyEnemy.CurrentHp, nearbyBefore);
            Assert.AreEqual(allyBefore, ally.CurrentHp);
        }

        [Test]
        public void RangedBonus_CritDoublesRawDamage()
        {
            var seed = FindSeedThatProcs(HumanBonusUnitRules.OnHitProcChance);
            var combat = CreateCombat(seed);
            var attacker = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Ranged, RangedStats(),
                distanceAlongLane: 20f, bonusSlot: 2);
            var target = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            target.WorldPosition = attacker.WorldPosition + new Vector3(6f, 0f, 0f);

            var projectile = new CombatProjectileState(
                1,
                attacker.UnitId,
                target.UnitId,
                attacker.OwnerSlot,
                UnitRole.Ranged,
                GameIds.Races.Human,
                rawDamage: 10f,
                flightDuration: 0.01f,
                attacker.WorldPosition,
                target.WorldPosition,
                isParabolic: true);

            var before = target.CurrentHp;
            combat.ResolveProjectileImpact(projectile);
            var expected = CombatRules.ApplyArmor(20f, target.Stats.Armor);
            Assert.AreEqual(before - expected, target.CurrentHp, 0.01f);
        }

        [Test]
        public void EffectiveBonusSlotForRole_OnlyMatchesOwnRole()
        {
            Assert.AreEqual(1, BonusKitRules.EffectiveBonusSlotForRole(1, UnitRole.Melee));
            Assert.AreEqual(0, BonusKitRules.EffectiveBonusSlotForRole(1, UnitRole.Ranged));
            Assert.AreEqual(3, BonusKitRules.EffectiveBonusSlotForRole(3, UnitRole.Caster));
            Assert.AreEqual(0, BonusKitRules.EffectiveBonusSlotForRole(0, UnitRole.Melee));
        }

        [Test]
        public void CasterBonus_HybridMelee_WhenCloserThanTwoMeters()
        {
            Assert.IsTrue(HumanBonusUnitRules.IsHybridMeleeRange(1.9f));
            Assert.IsFalse(HumanBonusUnitRules.IsHybridMeleeRange(2f));
            Assert.IsFalse(HumanBonusUnitRules.IsHybridMeleeRange(2.1f));

            var player = new MatchPlayerState(0, GameIds.Races.Human, 0) { MeleeDamageLevel = 2 };
            var damage = HumanBonusUnitRules.RollHybridMeleeDamage(player, new System.Random(1));
            var min = HumanBonusUnitRules.HybridMeleeDamageMin
                * (1f + 2 * MatchEconomyRules.MeleeDamagePercentPerLevel);
            var max = HumanBonusUnitRules.HybridMeleeDamageMax
                * (1f + 2 * MatchEconomyRules.MeleeDamagePercentPerLevel);
            Assert.GreaterOrEqual(damage, min - 0.01f);
            Assert.LessOrEqual(damage, max + 0.01f);
        }

        [Test]
        public void CasterBonus_IsHybridMeleeNow_RequiresBonusSlotAndCloseTarget()
        {
            var combat = CreateCombat();
            var caster = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Caster, CasterStats(),
                distanceAlongLane: 20f, bonusSlot: 3);
            Assert.IsTrue(HumanBonusUnitRules.IsHybridMeleeNow(
                caster, caster.WorldPosition, caster.WorldPosition + new Vector3(1.5f, 0f, 0f)));
            Assert.IsFalse(HumanBonusUnitRules.IsHybridMeleeNow(
                caster, caster.WorldPosition, caster.WorldPosition + new Vector3(3f, 0f, 0f)));

            var baseCaster = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Caster, CasterStats(),
                distanceAlongLane: 24f, bonusSlot: 0);
            Assert.IsFalse(HumanBonusUnitRules.IsHybridMeleeNow(
                baseCaster, baseCaster.WorldPosition, baseCaster.WorldPosition + new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void CasterBonus_AttackVariants_StaffZeroMaceOne()
        {
            Assert.AreEqual(0f, HumanCasterBonusWeaponVisuals.StaffAttackVariant);
            Assert.AreEqual(1f, HumanCasterBonusWeaponVisuals.MaceAttackVariant);
        }

        [Test]
        public void FlyingBonus_OnDeath_SpawnsBaseRanged()
        {
            var seed = FindSeedThatProcs(HumanBonusUnitRules.OnDeathSpawnChance);
            var combat = CreateCombat(seed);
            var flyer = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Flying, FlyingStats(),
                distanceAlongLane: 20f, bonusSlot: 5);
            var killer = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 22f);
            combat.ApplyDamage(killer, flyer, 10_000f, killer.OwnerSlot);

            MatchUnitState spawned = null;
            for (var i = 0; i < combat.Units.Count; i++)
            {
                if (combat.Units[i].Role == UnitRole.Ranged && combat.Units[i].OwnerSlot == 0)
                {
                    spawned = combat.Units[i];
                    break;
                }
            }

            Assert.IsNotNull(spawned);
            Assert.AreEqual(0, spawned.BonusSlot);
            Assert.AreEqual(UnitRole.Ranged, spawned.Role);
            var casts = combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.FlyingSpawn, casts[0].Def.AbilityId);
        }

        [Test]
        public void CatapultBonus_SplashDamagesNearbyEnemies()
        {
            var combat = CreateCombat();
            var attacker = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Super, SuperStats(),
                distanceAlongLane: 20f, bonusSlot: 6);
            var primary = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            var splash = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            primary.WorldPosition = attacker.WorldPosition + new Vector3(8f, 0f, 0f);
            splash.WorldPosition = primary.WorldPosition + new Vector3(1f, 0f, 0f);

            var projectile = new CombatProjectileState(
                1,
                attacker.UnitId,
                primary.UnitId,
                attacker.OwnerSlot,
                UnitRole.Super,
                GameIds.Races.Human,
                rawDamage: 40f,
                flightDuration: 0.01f,
                attacker.WorldPosition,
                primary.WorldPosition,
                isParabolic: true,
                appliesSplashAoe: true);

            var splashBefore = splash.CurrentHp;
            combat.ResolveProjectileImpact(projectile);
            Assert.Less(splash.CurrentHp, splashBefore);
        }

        [Test]
        public void SiegeBonus_RegenAura_HealsCarrierAndAlly()
        {
            var combat = CreateCombat();
            var siege = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Siege, SiegeStats(),
                distanceAlongLane: 20f, bonusSlot: 4);
            siege.Abilities = AbilityKitDefaults.CreateSiegeRegen();
            siege.AbilityCooldownRemaining = new float[siege.Abilities.Length];
            var ally = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 22f);
            ally.WorldPosition = siege.WorldPosition + new Vector3(2f, 0f, 0f);
            siege.CurrentHp = siege.Stats.MaxHp - 20f;
            ally.CurrentHp = ally.Stats.MaxHp - 20f;

            var siegeBefore = siege.CurrentHp;
            var allyBefore = ally.CurrentHp;
            combat.Tick(1f);

            Assert.Greater(siege.CurrentHp, siegeBefore);
            Assert.Greater(ally.CurrentHp, allyBefore);
        }

        [Test]
        public void TryGetAuraVisual_SiegeRegen_ShowsDisc_CatapultTraitDoesNot()
        {
            var combat = CreateCombat();
            var siege = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Siege, SiegeStats(),
                distanceAlongLane: 20f, bonusSlot: 4);
            siege.Abilities = AbilityKitDefaults.CreateSiegeRegen();
            siege.AbilityCooldownRemaining = new float[siege.Abilities.Length];

            Assert.IsTrue(combat.TryGetAuraVisual(siege, out var radius, out _));
            Assert.AreEqual(HeroAbilityRules.AuraRadius, radius, 0.001f);

            var catapult = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Super, SuperStats(),
                distanceAlongLane: 10f, bonusSlot: 6);
            catapult.Abilities = AbilityKitDefaults.CreateSuperBonus();
            catapult.AbilityCooldownRemaining = new float[catapult.Abilities.Length];

            Assert.IsFalse(combat.TryGetAuraVisual(catapult, out _, out _));
        }

        [Test]
        public void MeleeBonus_CleaveProc_EmitsAbilityFx()
        {
            var seed = FindSeedThatProcs(HumanBonusUnitRules.OnHitProcChance);
            var combat = CreateCombat(seed);
            var attacker = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 20f, bonusSlot: 1);
            var target = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 22f);
            target.WorldPosition = attacker.WorldPosition + new Vector3(1.5f, 0f, 0f);

            combat.ConsumePendingAbilityCasts();
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                attacker.UnitId,
                target.UnitId,
                rawDamage: 20f,
                duration: 0.1f));

            var casts = combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.MeleeCleave, casts[0].Def.AbilityId);
            Assert.AreEqual(target.UnitId, casts[0].TargetUnitId);
        }

        [Test]
        public void RangedBonus_CritProc_EmitsAbilityFx()
        {
            var seed = FindSeedThatProcs(HumanBonusUnitRules.OnHitProcChance);
            var combat = CreateCombat(seed);
            var attacker = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Ranged, RangedStats(),
                distanceAlongLane: 20f, bonusSlot: 2);
            var target = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            target.WorldPosition = attacker.WorldPosition + new Vector3(6f, 0f, 0f);

            combat.ConsumePendingAbilityCasts();
            combat.ResolveProjectileImpact(new CombatProjectileState(
                1,
                attacker.UnitId,
                target.UnitId,
                attacker.OwnerSlot,
                UnitRole.Ranged,
                GameIds.Races.Human,
                rawDamage: 10f,
                flightDuration: 0.01f,
                attacker.WorldPosition,
                target.WorldPosition,
                isParabolic: true));

            var casts = combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.RangedCrit, casts[0].Def.AbilityId);
        }

        [Test]
        public void SuperBonus_CatapultSplash_EmitsAbilityFx()
        {
            var combat = CreateCombat();
            var attacker = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Super, SuperStats(),
                distanceAlongLane: 20f, bonusSlot: 6);
            var primary = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            primary.WorldPosition = attacker.WorldPosition + new Vector3(8f, 0f, 0f);

            combat.ConsumePendingAbilityCasts();
            combat.ResolveProjectileImpact(new CombatProjectileState(
                1,
                attacker.UnitId,
                primary.UnitId,
                attacker.OwnerSlot,
                UnitRole.Super,
                GameIds.Races.Human,
                rawDamage: 40f,
                flightDuration: 0.01f,
                attacker.WorldPosition,
                primary.WorldPosition,
                isParabolic: true,
                appliesSplashAoe: true));

            var casts = combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.SuperCatapult, casts[0].Def.AbilityId);
        }

        [Test]
        public void SuperAttackBand_RequiresMinRangeFive()
        {
            Assert.IsFalse(CombatRules.IsWithinAttackBand(4.9f, 10f, UnitRole.Super, UnitRole.Melee));
            Assert.IsTrue(CombatRules.IsWithinAttackBand(5f, 10f, UnitRole.Super, UnitRole.Melee));
            Assert.IsTrue(CombatRules.IsWithinAttackBand(10f, 10f, UnitRole.Super, UnitRole.Melee));
            Assert.IsFalse(CombatRules.IsWithinAttackBand(10.1f, 10f, UnitRole.Super, UnitRole.Melee));
            Assert.IsTrue(CombatRules.IsWithinAttackBand(12f, 12f, UnitRole.Super, UnitRole.Melee));
            Assert.IsTrue(CombatRules.IsWithinAttackBand(1f, 8f, UnitRole.Ranged, UnitRole.Melee));
        }

        [Test]
        public void SuperPendingShot_FiresIntoEmptyWhenTargetDies()
        {
            var combat = CreateCombat();
            var attacker = combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Super, SuperStats(),
                distanceAlongLane: 20f, bonusSlot: 0);
            var target = combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 28f);
            target.WorldPosition = attacker.WorldPosition + new Vector3(8f, 0f, 0f);
            var aim = target.WorldPosition;

            combat.ReleasePendingProjectile(new CombatPendingProjectileState(
                attacker.UnitId,
                target.UnitId,
                rawDamage: 40f,
                delaySeconds: 0f,
                aimWorldPosition: aim));

            // Kill target, then release a second pending shot locked to aim.
            combat.ApplyDamage(attacker, target, 10_000f, attacker.OwnerSlot);
            Assert.IsFalse(target.IsAlive);

            var beforeCount = combat.Projectiles.Count;
            combat.ReleasePendingProjectile(new CombatPendingProjectileState(
                attacker.UnitId,
                target.UnitId,
                rawDamage: 40f,
                delaySeconds: 0f,
                aimWorldPosition: aim));
            Assert.Greater(combat.Projectiles.Count, beforeCount);
            var last = combat.Projectiles[combat.Projectiles.Count - 1];
            Assert.AreEqual(aim.x, last.TargetPosition.x, 0.01f);
            Assert.AreEqual(aim.z, last.TargetPosition.z, 0.01f);
        }

        static MatchCombatSystem CreateCombat(int seed = 12345)
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, seed);
            return combat;
        }

        static int FindSeedThatProcs(float chance)
        {
            for (var seed = 0; seed < 10_000; seed++)
            {
                if (BonusKitRules.RollProc(new System.Random(seed), chance))
                {
                    return seed;
                }
            }

            Assert.Fail("No seed produced a proc");
            return 0;
        }

        static UnitCombatStats MeleeStats() =>
            new(UnitRole.Melee, 200f, 0f, 10f, 10f, 1f, 1.5f, 4f, 8);

        static UnitCombatStats CasterStats() =>
            new(UnitRole.Caster, 80f, 0f, 4f, 5f, 1f, 6f, 3.5f, 10, 200f);

        static UnitCombatStats RangedStats() =>
            new(UnitRole.Ranged, 70f, 0f, 6f, 8f, 1f, 8f, 3.5f, 6);

        static UnitCombatStats FlyingStats() =>
            new(UnitRole.Flying, 100f, 0f, 8f, 10f, 1f, 6f, 3.5f, 10);

        static UnitCombatStats SuperStats() =>
            new(UnitRole.Super, 500f, 2f, 30f, 40f, 0.5f, 10f, 3.5f, 50);

        static UnitCombatStats SiegeStats() =>
            new(UnitRole.Siege, 250f, 0f, 12f, 16f, 1f, 1.5f, 3.5f, 15);
    }
}
