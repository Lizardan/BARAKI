using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Plan0909, Фаза 5 — integration coverage of the Faceless champion kit share
    /// (heroes + titan): one Human slot is replaced by Call of the Deep, which summons
    /// 1–2 servants from a recent corpse (consumed) or beside the champion.
    /// </summary>
    public sealed class FacelessHeroRulesTests
    {
        const string Center = GameIds.Lanes.Center;

        static UnitCombatStats HeroStats(float maxHp = 500f)
        {
            return new UnitCombatStats(
                UnitRole.Hero,
                maxHp: maxHp,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: 0f,
                goldBounty: 10,
                maxMana: 0f);
        }

        MatchCombatSystem CreateCombat()
        {
            var config = new MatchConfig(
                2,
                new List<string> { GameIds.Races.Faceless, GameIds.Races.Human });
            var controller = new MatchController();
            controller.StartMatch(config);

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);
            return combat;
        }

        static void Place(MatchUnitState unit, Vector3 position)
        {
            unit.WorldPosition = position;
        }

        static MatchUnitState KillEnemy(MatchCombatSystem combat, Vector3 position)
        {
            var enemy = combat.SpawnUnit(1, Center, UnitRole.Melee, new UnitCombatStats(
                UnitRole.Melee, maxHp: 50f, armor: 0f, damageMin: 1f, damageMax: 1f,
                attackSpeed: 0.1f, attackRange: 1f, moveSpeed: 0f, goldBounty: 5));
            Place(enemy, position);
            combat.ApplyExternalDamage(enemy.UnitId, 1000f, killerOwnerSlot: 1);
            return enemy;
        }

        [Test]
        public void FacelessHero_CallOfTheDeep_WithCorpse_SummonsTwoServants_AndConsumesCorpse()
        {
            var combat = CreateCombat();
            var hero = combat.SpawnUnit(0, Center, UnitRole.Hero, HeroStats(), isHero: true, heroSlot: 1, level: 4);
            Place(hero, new Vector3(0f, 0f, 0f));
            KillEnemy(combat, new Vector3(0f, 0f, 3f));
            Assert.AreEqual(1, combat.Corpses.Count, "Kill should leave a corpse in cast range.");

            AbilityCastEvent? cast = null;
            combat.AbilityCast += e => cast = e;

            combat.Tick(0.1f);

            Assert.AreEqual(AbilityIds.CallOfTheDeep, cast?.Def.AbilityId, "Call of the Deep should fire.");
            Assert.AreEqual(3, combat.Units.Count, "Hero + two servants should be alive.");
            Assert.AreEqual(0, combat.Corpses.Count, "Corpse should be consumed.");
            Assert.Greater(hero.AbilityCooldownRemaining[0], 0f, "Call cooldown should be armed.");
        }

        [Test]
        public void FacelessHero_CallOfTheDeep_WithoutCorpse_SummonsOneAtHeroSide()
        {
            var combat = CreateCombat();
            var hero = combat.SpawnUnit(0, Center, UnitRole.Hero, HeroStats(), isHero: true, heroSlot: 1, level: 4);
            Place(hero, new Vector3(0f, 0f, 0f));

            AbilityCastEvent? cast = null;
            combat.AbilityCast += e => cast = e;

            combat.Tick(0.1f);

            Assert.AreEqual(AbilityIds.CallOfTheDeep, cast?.Def.AbilityId, "Call fires even without a corpse.");
            Assert.AreEqual(2, combat.Units.Count, "Hero + one servant should be alive.");
            Assert.AreEqual(FacelessHeroRules.ServantsWithoutCorpse, combat.GetUnit(cast.Value.TargetUnitId) != null ? 1 : 0);
            Assert.Greater(hero.AbilityCooldownRemaining[0], 0f, "Call cooldown should be armed.");
        }

        [Test]
        public void FacelessHero_Servants_AreOwnedByCaster_AndBehaveAsServants()
        {
            var combat = CreateCombat();
            var hero = combat.SpawnUnit(0, Center, UnitRole.Hero, HeroStats(), isHero: true, heroSlot: 1, level: 4);
            Place(hero, new Vector3(0f, 0f, 0f));
            KillEnemy(combat, new Vector3(0f, 0f, 3f));

            AbilityCastEvent? cast = null;
            combat.AbilityCast += e => cast = e;
            combat.Tick(0.1f);

            Assert.AreEqual(3, combat.Units.Count);
            foreach (var unit in combat.Units)
            {
                if (unit.UnitId == hero.UnitId)
                {
                    continue;
                }

                Assert.AreEqual(0, unit.OwnerSlot, "Servant must be owned by the Faceless champion.");
                Assert.AreEqual(UnitRole.Melee, unit.Role, "Servants are melee minions.");
                Assert.AreEqual(BonusKitRules.SummonBonusSlot, unit.BonusSlot, "Servant marker slot.");
            }
        }

        [Test]
        public void FacelessHero_LowLevel_DoesNotCastServantSummon()
        {
            var combat = CreateCombat();
            var hero = combat.SpawnUnit(0, Center, UnitRole.Hero, HeroStats(), isHero: true, heroSlot: 1, level: 1);
            Place(hero, new Vector3(0f, 0f, 0f));
            KillEnemy(combat, new Vector3(0f, 0f, 3f));

            var castCount = 0;
            combat.AbilityCast += _ => castCount++;
            combat.Tick(0.1f);

            // Call (unlock 4) is gated behind hero level — neither it nor any other kit
            // ability may fire without a living enemy target.
            Assert.AreEqual(0, castCount, "No champion ability should cast below level 4.");
            Assert.AreEqual(1, combat.Corpses.Count, "Corpse must survive while Call is locked.");
            Assert.AreEqual(1, combat.Units.Count, "No servant may be summoned.");
        }

        [Test]
        public void FacelessTitan_CallOfTheDeep_SummonsServants()
        {
            var combat = CreateCombat();
            var titan = combat.SpawnUnit(0, Center, UnitRole.Titan, HeroStats(maxHp: 800f), level: 10);
            Place(titan, new Vector3(0f, 0f, 0f));
            KillEnemy(combat, new Vector3(0f, 0f, 2f));

            AbilityCastEvent? cast = null;
            combat.AbilityCast += e => cast = e;
            combat.Tick(0.1f);

            Assert.AreEqual(AbilityIds.CallOfTheDeep, cast?.Def.AbilityId, "Titan kit must use Call of the Deep.");
            Assert.AreEqual(3, combat.Units.Count, "Titan + two servants should be alive.");
            Assert.AreEqual(0, combat.Corpses.Count, "Corpse should be consumed.");
        }

        [Test]
        public void FacelessHero_CallOfTheDeep_SecondCastWaitsForCooldown()
        {
            var combat = CreateCombat();
            var hero = combat.SpawnUnit(0, Center, UnitRole.Hero, HeroStats(), isHero: true, heroSlot: 1, level: 4);
            Place(hero, new Vector3(0f, 0f, 0f));

            var castCount = 0;
            combat.AbilityCast += _ => castCount++;
            combat.Tick(0.1f);
            Assert.AreEqual(1, castCount, "First summon should fire immediately.");
            Assert.AreEqual(2, combat.Units.Count);

            // A fresh corpse appears (e.g. an ally died) but the 25s cooldown is still armed —
            // nothing may cast while heroes also have no living enemy for Strike.
            KillEnemy(combat, new Vector3(0f, 0f, 3f));
            combat.Tick(0.1f);

            Assert.AreEqual(1, castCount, "Call must stay on cooldown.");
            Assert.AreEqual(2, combat.Units.Count, "No extra servants while on cooldown.");
        }
    }
}