using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Faceless unit bonus slots 1–6 (FACELESS-011).</summary>
    public sealed class FacelessBonusUnitRulesTests
    {
        [Test]
        public void HungerOfTheOldOne_MeleeBonus_HealsHalfOfDamageDealt()
        {
            var combat = CreateFacelessCombat();
            var attacker = Spawn(combat, 0, UnitRole.Melee, bonusSlot: 1);
            var target = Spawn(combat, 1, UnitRole.Melee);

            const float raw = 20f;
            var healedOnce = false;
            for (var i = 0; i < 300 && !healedOnce; i++)
            {
                target.CurrentHp = target.Stats.MaxHp;
                attacker.CurrentHp = attacker.Stats.MaxHp * 0.5f;
                var before = attacker.CurrentHp;
                MeleeHit(combat, attacker, target, raw);
                var gain = attacker.CurrentHp - before;
                if (gain > 0f)
                {
                    Assert.AreEqual(
                        raw * FacelessBonusUnitRules.VampiricHealPercent,
                        gain,
                        0.001f);
                    healedOnce = true;
                }
            }

            Assert.IsTrue(healedOnce, "vampiric heal must proc at least once over 300 hits");
        }

        [Test]
        public void TaintingBolt_RangedBonus_AppliesDot()
        {
            var combat = CreateFacelessCombat();
            var attacker = Spawn(combat, 0, UnitRole.Ranged, bonusSlot: 2);
            var target = Spawn(combat, 1, UnitRole.Melee);

            var procced = false;
            for (var i = 0; i < 300 && !procced; i++)
            {
                target.CurrentHp = target.Stats.MaxHp;
                target.BurnSecondsRemaining = 0f;
                RangedHit(combat, attacker, target, 5f);
                if (target.BurnSecondsRemaining > 0f)
                {
                    Assert.AreEqual(
                        FacelessBonusUnitRules.TaintingBoltDamagePerSecond,
                        target.BurnDamagePerSecond,
                        0.001f);
                    Assert.AreEqual(
                        FacelessBonusUnitRules.TaintingBoltDurationSeconds,
                        target.BurnSecondsRemaining,
                        0.001f);
                    procced = true;
                }
            }

            Assert.IsTrue(procced, "Tainting Bolt must proc at least once over 300 shots");
        }

        [Test]
        public void CallOfTheAbyss_CasterBonus_SpawnsMiniMeleeOnKill()
        {
            var combat = CreateFacelessCombat();
            var allyMelee = Spawn(combat, 0, UnitRole.Melee);
            var caster = Spawn(combat, 0, UnitRole.Caster, bonusSlot: 3);
            var victim = Spawn(combat, 1, UnitRole.Melee);

            var before = combat.Units.Count;
            combat.ApplyDamage(caster, victim, 10_000f, caster.OwnerSlot);

            Assert.AreEqual(before, combat.Units.Count, "victim is removed and replaced by a mini-melee");
            var spawned = combat.Units[combat.Units.Count - 1];
            Assert.AreEqual(UnitRole.Melee, spawned.Role);
            Assert.AreEqual(
                allyMelee.Stats.MaxHp * FacelessBonusUnitRules.MiniMeleeStatScale,
                spawned.Stats.MaxHp,
                0.001f);
        }

        [Test]
        public void DeathExplosion_SiegeBonus_DamagesNearbyEnemiesOnly()
        {
            var combat = CreateFacelessCombat();
            var bomber = Spawn(combat, 0, UnitRole.Siege, bonusSlot: 4);

            var nearEnemy = Spawn(combat, 1, UnitRole.Melee);
            nearEnemy.WorldPosition = bomber.WorldPosition + new Vector3(2f, 0f, 0f);
            var farEnemy = Spawn(combat, 1, UnitRole.Melee);
            farEnemy.WorldPosition = bomber.WorldPosition + new Vector3(30f, 0f, 0f);

            var nearBefore = nearEnemy.CurrentHp;
            var farBefore = farEnemy.CurrentHp;
            var expectedDamage = bomber.Stats.MaxHp * FacelessBonusUnitRules.DeathExplosionMaxHpPercent;

            combat.ApplyDamage(null, bomber, 10_000f, 1);

            Assert.AreEqual(expectedDamage, nearBefore - nearEnemy.CurrentHp, 0.001f,
                "enemy within radius 3 must take 10% of the bomber's max HP");
            Assert.AreEqual(farBefore, farEnemy.CurrentHp, "enemy outside the radius must be untouched");
        }

        [Test]
        public void HungeringFlight_FlyingBonus_StacksAttackSpeedUpToThree()
        {
            var combat = CreateFacelessCombat();
            var flyer = Spawn(combat, 0, UnitRole.Flying, bonusSlot: 5);
            var baseInterval = combat.GetAttackIntervalSeconds(flyer);

            // Four kills: stacks must cap at 3.
            for (var i = 0; i < FacelessBonusUnitRules.MaxFeastStacks + 1; i++)
            {
                var victim = Spawn(combat, 1, UnitRole.Melee);
                combat.ApplyDamage(flyer, victim, 10_000f, flyer.OwnerSlot);
            }

            Assert.AreEqual(FacelessBonusUnitRules.MaxFeastStacks, flyer.FeastStacks, "stacks cap at 3");
            var expected = 1f
                + FacelessBonusUnitRules.HungeringFlightAttackSpeedPerStack
                * FacelessBonusUnitRules.MaxFeastStacks;
            Assert.AreEqual(baseInterval / expected, combat.GetAttackIntervalSeconds(flyer), 0.001f);
        }

        [Test]
        public void HungeringFlight_StacksExpireAfterDuration()
        {
            var combat = CreateFacelessCombat();
            var flyer = Spawn(combat, 0, UnitRole.Flying, bonusSlot: 5);
            var baseInterval = combat.GetAttackIntervalSeconds(flyer);

            var victim = Spawn(combat, 1, UnitRole.Melee);
            combat.ApplyDamage(flyer, victim, 10_000f, flyer.OwnerSlot);
            Assert.AreEqual(1, flyer.FeastStacks);

            combat.Tick(FacelessBonusUnitRules.FeastBuffDurationSeconds + 0.1f);
            Assert.AreEqual(0, flyer.FeastStacks);
            Assert.AreEqual(baseInterval, combat.GetAttackIntervalSeconds(flyer), 0.001f);
        }

        [Test]
        public void FeastOnTheFallen_SuperBonus_HealsAndStacks()
        {
            var combat = CreateFacelessCombat();
            var super = Spawn(combat, 0, UnitRole.Super, bonusSlot: 6);
            super.CurrentHp = super.Stats.MaxHp * 0.5f;

            var before = super.CurrentHp;
            var victim = Spawn(combat, 1, UnitRole.Melee);
            combat.ApplyDamage(super, victim, 10_000f, super.OwnerSlot);

            Assert.AreEqual(before + FacelessBonusUnitRules.FeastHealFlat, super.CurrentHp, 0.001f);
            Assert.AreEqual(1, super.FeastStacks);
            Assert.AreEqual(
                FacelessBonusUnitRules.FeastAttackSpeedPerStack,
                super.FeastAttackSpeedPerStack,
                0.001f);
        }

        [Test]
        public void FacelessBonuses_NeverFireForHumanRace()
        {
            var combat = CreateHumanCombat();
            var melee = Spawn(combat, 0, UnitRole.Melee, bonusSlot: 1);
            var target = Spawn(combat, 1, UnitRole.Melee);

            melee.CurrentHp = melee.Stats.MaxHp * 0.5f;
            var before = melee.CurrentHp;
            for (var i = 0; i < 60; i++)
            {
                target.CurrentHp = target.Stats.MaxHp;
                MeleeHit(combat, melee, target, 20f);
            }

            Assert.AreEqual(before, melee.CurrentHp,
                "a Human melee must never heal from the Faceless vampiric bonus");
        }

        [Test]
        public void FacelessBonuses_NeverFireWithoutBonusSlot()
        {
            var combat = CreateFacelessCombat();
            var melee = Spawn(combat, 0, UnitRole.Melee, bonusSlot: 0);
            var target = Spawn(combat, 1, UnitRole.Melee);

            melee.CurrentHp = melee.Stats.MaxHp * 0.5f;
            var before = melee.CurrentHp;
            for (var i = 0; i < 60; i++)
            {
                target.CurrentHp = target.Stats.MaxHp;
                MeleeHit(combat, melee, target, 20f);
            }

            Assert.AreEqual(before, melee.CurrentHp,
                "a base Faceless melee (no bonus pick) must not heal");
        }

        static void MeleeHit(
            MatchCombatSystem combat,
            MatchUnitState attacker,
            MatchUnitState target,
            float rawDamage) =>
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                attacker.UnitId,
                target.UnitId,
                rawDamage,
                duration: 0.01f));

        static void RangedHit(
            MatchCombatSystem combat,
            MatchUnitState attacker,
            MatchUnitState target,
            float rawDamage) =>
            combat.ResolveProjectileImpact(new CombatProjectileState(
                projectileId: NextId(),
                attacker.UnitId,
                target.UnitId,
                attacker.OwnerSlot,
                attacker.Role,
                GameIds.Races.Faceless,
                rawDamage,
                flightDuration: 0.01f,
                startPosition: attacker.WorldPosition,
                targetPosition: target.WorldPosition,
                isParabolic: false));

        static MatchUnitState Spawn(
            MatchCombatSystem combat,
            int ownerSlot,
            UnitRole role,
            int bonusSlot = 0)
        {
            var stats = role switch
            {
                UnitRole.Melee => new UnitCombatStats(role, 120f, 0f, 8f, 10f, 1f, 1.5f, 4f, 8),
                UnitRole.Ranged => new UnitCombatStats(role, 70f, 0f, 6f, 8f, 1f, 1.5f, 3.5f, 6),
                UnitRole.Caster => new UnitCombatStats(role, 80f, 0f, 4f, 5f, 1f, 1.5f, 3.5f, 10, 200f),
                UnitRole.Siege => new UnitCombatStats(role, 200f, 0f, 12f, 16f, 1f, 1.5f, 3.5f, 15),
                UnitRole.Flying => new UnitCombatStats(role, 100f, 0f, 8f, 10f, 1f, 1.5f, 3.5f, 10),
                UnitRole.Super => new UnitCombatStats(role, 500f, 2f, 30f, 40f, 0.5f, 10f, 3.5f, 50),
                _ => new UnitCombatStats(role, 300f, 0f, 10f, 12f, 1f, 2f, 3.5f, 20),
            };

            return combat.SpawnUnit(
                ownerSlot,
                GameIds.Lanes.Left,
                role,
                stats,
                distanceAlongLane: 20f,
                isHero: false,
                heroSlot: 0,
                bonusSlot: bonusSlot);
        }

        static int _nextTestId = 1;

        static int NextId() => _nextTestId++;

        static MatchCombatSystem CreateFacelessCombat(int seed = 12345) =>
            CreateCombat(new[] { GameIds.Races.Faceless, GameIds.Races.Faceless }, seed);

        static MatchCombatSystem CreateHumanCombat(int seed = 12345) =>
            CreateCombat(new[] { GameIds.Races.Human, GameIds.Races.Human }, seed);

        static MatchCombatSystem CreateCombat(string[] raceIds, int seed)
        {
            var controller = new MatchController();
            controller.StartMatch(new MatchConfig(playerCount: 2, raceIds: raceIds));
            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, seed);
            return combat;
        }
    }
}
