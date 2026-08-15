using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HeroAbilityRulesTests
    {
        [Test]
        public void GatherEnemiesInRadius_ReturnsOnlyOpponentsWithinRange()
        {
            var hero = MakeUnit(ownerSlot: 0, UnitRole.Hero, new Vector3(0f, 0f, 0f));
            var nearbyEnemy = MakeUnit(1, UnitRole.Melee, new Vector3(3f, 0f, 0f));
            var farEnemy = MakeUnit(1, UnitRole.Melee, new Vector3(20f, 0f, 0f));
            var ally = MakeUnit(0, UnitRole.Melee, new Vector3(2f, 0f, 0f));
            var units = new List<MatchUnitState> { hero, nearbyEnemy, farEnemy, ally };

            var enemies = HeroAbilityRules.GatherEnemiesInRadius(hero, units, 4f);

            Assert.AreEqual(1, enemies.Count);
            Assert.AreEqual(nearbyEnemy.UnitId, enemies[0].UnitId);
        }

        [Test]
        public void GatherAlliesInRadius_IncludesSelfAndAllies()
        {
            var hero = MakeUnit(0, UnitRole.Hero, new Vector3(0f, 0f, 0f));
            var ally = MakeUnit(0, UnitRole.Melee, new Vector3(3f, 0f, 0f));
            var enemy = MakeUnit(1, UnitRole.Melee, new Vector3(2f, 0f, 0f));
            var units = new List<MatchUnitState> { hero, ally, enemy };

            var allies = HeroAbilityRules.GatherAlliesInRadius(hero, units, 6f);

            Assert.AreEqual(2, allies.Count);
            Assert.IsTrue(allies.Contains(hero));
            Assert.IsTrue(allies.Contains(ally));
        }

        [Test]
        public void ApplyHeal_ClampsToMaxHp()
        {
            var full = HeroAbilityRules.ApplyHeal(600f, 600f);
            Assert.AreEqual(600f, full);

            var partial = HeroAbilityRules.ApplyHeal(500f, 600f);
            Assert.AreEqual(600f, partial);

            var low = HeroAbilityRules.ApplyHeal(10f, 600f);
            Assert.AreEqual(10f + HeroAbilityRules.HealAmount, low);
        }

        [Test]
        public void ApplyHeal_CustomAmount_ClampsToMaxHp()
        {
            Assert.AreEqual(80f, HeroAbilityRules.ApplyHeal(30f, 80f, 100f));
            Assert.AreEqual(80f, HeroAbilityRules.ApplyHeal(30f, 200f, 50f));
        }

        [Test]
        public void FindNearestEnemy_PicksClosestOpponent()
        {
            var hero = MakeUnit(0, UnitRole.Hero, new Vector3(0f, 0f, 0f));
            var near = MakeUnit(1, UnitRole.Melee, new Vector3(2f, 0f, 0f));
            var far = MakeUnit(1, UnitRole.Melee, new Vector3(4f, 0f, 0f));
            var ally = MakeUnit(0, UnitRole.Melee, new Vector3(1f, 0f, 0f));
            var units = new List<MatchUnitState> { hero, near, far, ally };

            var found = HeroAbilityRules.FindNearestEnemy(hero, units, 5f);

            Assert.AreEqual(near.UnitId, found.UnitId);
        }

        [Test]
        public void Kits_MatchExpectedAbilitiesPerHeroSlot()
        {
            var king = AbilityKitDefaults.Create(UnitRole.Hero, HeroAbilityRules.KingSlot);
            Assert.AreEqual(AbilityIds.Strike, king[2].AbilityId);
            Assert.AreEqual(AbilityIds.Heal, king[0].AbilityId);
            Assert.AreEqual(AbilityIds.Ultimate, king[1].AbilityId);
            Assert.AreEqual(AbilityIds.AuraDamagePercent, king[3].AbilityId);

            var paladin = AbilityKitDefaults.Create(UnitRole.Hero, HeroAbilityRules.PaladinSlot);
            Assert.AreEqual(AbilityIds.Smite, paladin[2].AbilityId);
            Assert.AreEqual(AbilityIds.Shield, paladin[0].AbilityId);
            Assert.AreEqual(AbilityIds.Consecration, paladin[1].AbilityId);
            Assert.AreEqual(AbilityIds.AuraAttackSpeedPercent, paladin[3].AbilityId);

            var priest = AbilityKitDefaults.Create(UnitRole.Hero, HeroAbilityRules.PriestSlot);
            Assert.AreEqual(AbilityIds.HolyNova, priest[2].AbilityId);
            Assert.AreEqual(AbilityIds.GreaterHeal, priest[0].AbilityId);
            Assert.AreEqual(AbilityIds.Revive, priest[1].AbilityId);
            Assert.AreEqual(AbilityIds.AuraArmorPercent, priest[3].AbilityId);
        }

        [Test]
        public void DisplayNames_CoverPaladinAndPriestAbilities()
        {
            Assert.AreEqual("Smite", Def(AbilityIds.Smite).DisplayName);
            Assert.AreEqual("Shield", Def(AbilityIds.Shield).DisplayName);
            Assert.AreEqual("Consecration", Def(AbilityIds.Consecration).DisplayName);
            Assert.AreEqual("Holy Nova", Def(AbilityIds.HolyNova).DisplayName);
            Assert.AreEqual("Greater Heal", Def(AbilityIds.GreaterHeal).DisplayName);
            Assert.AreEqual("Revive", Def(AbilityIds.Revive).DisplayName);
            Assert.AreEqual("Slam", Def(AbilityIds.Slam).DisplayName);
            Assert.AreEqual("Stomp", Def(AbilityIds.Stomp).DisplayName);
        }

        private static UnitAbilityDef Def(int abilityId)
        {
            foreach (var role in new[] { UnitRole.Hero, UnitRole.Titan, UnitRole.Caster })
            {
                var slots = role == UnitRole.Hero
                    ? new[] { HeroAbilityRules.KingSlot, HeroAbilityRules.PaladinSlot, HeroAbilityRules.PriestSlot }
                    : new[] { 0 };

                foreach (var slot in slots)
                {
                    var kit = AbilityKitDefaults.Create(role, slot);
                    for (var i = 0; i < kit.Length; i++)
                    {
                        if (kit[i].AbilityId == abilityId)
                        {
                            return kit[i];
                        }
                    }
                }
            }

            Assert.Fail($"Ability {abilityId} not found in default kits.");
            return null;
        }

        [Test]
        public void PickHolyNovaAnchor_PrefersAllyThatHealsAndHitsEnemies()
        {
            var priest = MakeUnit(0, UnitRole.Hero, new Vector3(0f, 0f, 0f));
            var farAlly = MakeUnit(0, UnitRole.Melee, new Vector3(8f, 0f, 0f));
            farAlly.CurrentHp = 200f;
            var enemyAtAlly = MakeUnit(1, UnitRole.Melee, new Vector3(8.5f, 0f, 0f));
            var enemyAtPriest = MakeUnit(1, UnitRole.Melee, new Vector3(1f, 0f, 0f));
            var units = new List<MatchUnitState> { priest, farAlly, enemyAtAlly, enemyAtPriest };

            var anchor = HeroAbilityRules.PickHolyNovaAnchor(priest, units, 10f, 4f);

            Assert.AreEqual(farAlly.UnitId, anchor.UnitId);
        }

        [Test]
        public void GatherAlliesAround_UsesCenterNotCaster()
        {
            var priest = MakeUnit(0, UnitRole.Hero, Vector3.zero);
            var ally = MakeUnit(0, UnitRole.Melee, new Vector3(8f, 0f, 0f));
            var units = new List<MatchUnitState> { priest, ally };

            var aroundAlly = HeroAbilityRules.GatherAlliesAround(0, ally.WorldPosition, units, 4f);
            Assert.AreEqual(1, aroundAlly.Count);
            Assert.AreEqual(ally.UnitId, aroundAlly[0].UnitId);
        }

        static int _nextId = 1;

        static MatchUnitState MakeUnit(int ownerSlot, UnitRole role, Vector3 position)
        {
            var stats = new UnitCombatStats(role, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);
            var unit = new MatchUnitState(
                _nextId++,
                ownerSlot,
                GameIds.Lanes.Center,
                role,
                stats,
                stats.MaxHp,
                position);
            return unit;
        }
    }
}
