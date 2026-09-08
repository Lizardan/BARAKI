using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Faceless champion veterans (bonus slots 7–10) — FACELESS-012.</summary>
    public sealed class FacelessVeteranBonusTests
    {
        [Test]
        public void CreateVeteranKit_Faceless_SlotMapsToSignatureAbility()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AbilityIds.Heal,
                    AbilityIds.AncientMantle,
                    AbilityIds.Strike,
                    AbilityIds.AuraDamagePercent,
                },
                Ids(AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, 7)));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AbilityIds.AreaOfMiss,
                    AbilityIds.Consecration,
                    AbilityIds.Smite,
                    AbilityIds.AuraAttackSpeedPercent,
                },
                Ids(AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, 8)));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AbilityIds.FeastZone,
                    AbilityIds.Revive,
                    AbilityIds.HolyNova,
                    AbilityIds.AuraArmorPercent,
                },
                Ids(AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, 9)));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AbilityIds.Rally,
                    AbilityIds.Stomp,
                    AbilityIds.Slam,
                    AbilityIds.AuraOfHunger,
                },
                Ids(AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, 10)));

            // Human slots keep their own signature ids.
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AbilityIds.Heal,
                    AbilityIds.KingsCommand,
                    AbilityIds.Strike,
                    AbilityIds.AuraDamagePercent,
                },
                Ids(AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Human, 7)));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AbilityIds.Aegis,
                    AbilityIds.Consecration,
                    AbilityIds.Smite,
                    AbilityIds.AuraAttackSpeedPercent,
                },
                Ids(AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Human, 8)));
        }

        [Test]
        public void MatchesUnit_MapsBonusSlotToHeroOrTitan()
        {
            Assert.IsTrue(BonusKitRules.MatchesUnit(7, UnitRole.Hero, 1));
            Assert.IsFalse(BonusKitRules.MatchesUnit(7, UnitRole.Hero, 2));
            Assert.IsFalse(BonusKitRules.MatchesUnit(7, UnitRole.Melee, 1));
            Assert.IsTrue(BonusKitRules.MatchesUnit(10, UnitRole.Titan, 0));
            Assert.IsFalse(BonusKitRules.MatchesUnit(10, UnitRole.Hero, 3));
            // Unit slots 1–6 still map by role.
            Assert.IsTrue(BonusKitRules.MatchesUnit(6, UnitRole.Super, 0));
            Assert.IsFalse(BonusKitRules.MatchesUnit(6, UnitRole.Melee, 0));
        }

        [Test]
        public void EffectiveBonusSlotForHeroTitan_FacelessGated()
        {
            // The Faceless bonus kit is GATE-locked (FACELESS-014): no veteran slot resolves
            // until the gate is lifted, same as any other race without a kit.
            Assert.AreEqual(0, BonusKitRules.EffectiveBonusSlotForHero(
                GameIds.Races.Faceless, 7, 1));
            Assert.AreEqual(0, BonusKitRules.EffectiveBonusSlotForTitan(
                GameIds.Races.Faceless, 10));

            // A race with a kit resolves slots normally.
            Assert.AreEqual(7, BonusKitRules.EffectiveBonusSlotForHero(
                GameIds.Races.Human, 7, 1));
            Assert.AreEqual(0, BonusKitRules.EffectiveBonusSlotForHero(
                GameIds.Races.Human, 8, 1));
            Assert.AreEqual(10, BonusKitRules.EffectiveBonusSlotForTitan(
                GameIds.Races.Human, 10));
            Assert.AreEqual(0, BonusKitRules.EffectiveBonusSlotForTitan(
                GameIds.Races.Human, 7));
        }

        [Test]
        public void AuraOfHunger_FacelessTitan_HealsAttackerOnDamage()
        {
            var combat = CreateFacelessCombat();
            Spawn(combat, 0, UnitRole.Titan, bonusSlot: 10); // living veteran titan
            var attacker = Spawn(combat, 0, UnitRole.Melee);
            var enemy = Spawn(combat, 1, UnitRole.Melee);

            attacker.CurrentHp = attacker.Stats.MaxHp * 0.5f;
            enemy.CurrentHp = enemy.Stats.MaxHp;
            var attackerBefore = attacker.CurrentHp;
            var enemyBefore = enemy.CurrentHp;

            combat.ApplyDamage(attacker, enemy, 100f, attacker.OwnerSlot);

            var dealt = enemyBefore - enemy.CurrentHp;
            Assert.Greater(dealt, 0f, "enemy must take damage");
            Assert.AreEqual(
                attackerBefore + dealt * FacelessBonusUnitRules.AuraOfHungerHealFraction,
                attacker.CurrentHp,
                0.001f,
                "attacker heals 15% of the damage it dealt (Aura of Hunger)");
        }

        [Test]
        public void AuraOfHunger_NeverFiresWithoutVeteranTitan()
        {
            var combat = CreateFacelessCombat();
            var attacker = Spawn(combat, 0, UnitRole.Melee); // no veteran titan on the field
            var enemy = Spawn(combat, 1, UnitRole.Melee);

            attacker.CurrentHp = attacker.Stats.MaxHp * 0.5f;
            combat.ApplyDamage(attacker, enemy, 100f, attacker.OwnerSlot);

            Assert.AreEqual(attacker.Stats.MaxHp * 0.5f, attacker.CurrentHp, 0.001f,
                "no veteran titan means no lifesteal");
        }

        [Test]
        public void AreaOfMiss_EvadeGate_BlocksAllDamage()
        {
            var combat = CreateFacelessCombat();
            var attacker = Spawn(combat, 0, UnitRole.Melee);
            var enemy = Spawn(combat, 1, UnitRole.Melee);
            enemy.CurrentHp = enemy.Stats.MaxHp;

            enemy.EvadeRemainingSeconds = 4f;
            var dealt = combat.ApplyDamage(attacker, enemy, 1000f, attacker.OwnerSlot);

            Assert.AreEqual(0f, dealt, "an evading target takes no damage");
            Assert.AreEqual(enemy.Stats.MaxHp, enemy.CurrentHp, "evading target HP is unchanged");
        }

        [Test]
        public void FeastZone_FacelessHero_HealsAttackerOnDamage()
        {
            var combat = CreateFacelessCombat();
            var attacker = Spawn(combat, 0, UnitRole.Melee);
            var enemy = Spawn(combat, 1, UnitRole.Melee);

            combat.ReplaceHealZone(new HeroHealZoneState
            {
                OwnerSlot = attacker.OwnerSlot,
                Center = attacker.WorldPosition,
                Radius = FacelessBonusUnitRules.FeastZoneRadius,
                RemainingSeconds = FacelessBonusUnitRules.FeastZoneSeconds,
                HealFractionOfDamageDealt = FacelessBonusUnitRules.FeastZoneHealFraction,
                FollowUnitId = attacker.UnitId,
            });

            attacker.CurrentHp = attacker.Stats.MaxHp * 0.5f;
            enemy.CurrentHp = enemy.Stats.MaxHp;
            var attackerBefore = attacker.CurrentHp;
            var enemyBefore = enemy.CurrentHp;

            combat.ApplyDamage(attacker, enemy, 100f, attacker.OwnerSlot);

            var dealt = enemyBefore - enemy.CurrentHp;
            Assert.Greater(dealt, 0f);
            Assert.AreEqual(
                attackerBefore + dealt * FacelessBonusUnitRules.FeastZoneHealFraction,
                attacker.CurrentHp,
                0.001f,
                "attacker inside Feast Zone heals 30% of the damage it dealt");
        }

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
                UnitRole.Titan => new UnitCombatStats(role, 400f, 4f, 30f, 40f, 1f, 1.5f, 3.5f, 80),
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

        static MatchCombatSystem CreateFacelessCombat(int seed = 12345) =>
            CreateCombat(new[] { GameIds.Races.Faceless, GameIds.Races.Faceless }, seed);

        static MatchCombatSystem CreateCombat(string[] raceIds, int seed)
        {
            var controller = new MatchController();
            controller.StartMatch(new MatchConfig(playerCount: 2, raceIds: raceIds));
            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, seed);
            return combat;
        }

        static System.Collections.Generic.List<int> Ids(UnitAbilityDef[] kit)
        {
            var ids = new System.Collections.Generic.List<int>();
            foreach (var def in kit)
            {
                ids.Add(def.AbilityId);
            }

            return ids;
        }
    }
}
