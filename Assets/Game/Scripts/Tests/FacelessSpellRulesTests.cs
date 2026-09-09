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
    /// FACELESS-016 — integration coverage of the Faceless caster kit
    /// (Blighting Gaze / Void Drain / Raise the Drowned). Mirrors
    /// <see cref="CasterSpellRulesTests"/> but player 0 is Faceless so the
    /// combat system wires the Faceless kit through <see cref="AbilityKitDefaults.CreateForSpawn"/>.
    /// </summary>
    public sealed class FacelessSpellRulesTests
    {
        const string Center = GameIds.Lanes.Center;

        static UnitCombatStats CasterStats()
        {
            return new UnitCombatStats(
                UnitRole.Caster,
                maxHp: 100f,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 30f,
                moveSpeed: 0f,
                goldBounty: 1,
                maxMana: 200f);
        }

        static UnitCombatStats UnitStats(UnitRole role, float maxHp = 100f, int bounty = 1, float moveSpeed = 0f)
        {
            return new UnitCombatStats(
                role,
                maxHp: maxHp,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 0.1f,
                attackRange: 1f,
                moveSpeed: moveSpeed,
                goldBounty: bounty);
        }

        MatchCombatSystem CreateCombat(int magicLevel)
        {
            // Player 0 is Faceless so the caster inherits the Faceless kit on spawn.
            var config = new MatchConfig(
                2,
                new List<string> { GameIds.Races.Faceless, GameIds.Races.Human });
            var controller = new MatchController();
            controller.StartMatch(config);
            controller.Players[0].MagicLevel = magicLevel;

            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph);
            return combat;
        }

        static void Place(MatchUnitState unit, Vector3 position)
        {
            unit.WorldPosition = position;
        }

        [Test]
        public void FacelessCaster_NoMagicLevel_DoesNotCast()
        {
            var combat = CreateCombat(magicLevel: 0);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var enemy = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(enemy, new Vector3(0f, 0f, 2f));

            combat.Tick(0.1f);

            Assert.AreEqual(100f, enemy.CurrentHp, 0.01f, "No Faceless spell should fire without magic level 1.");
            Assert.AreEqual(0f, caster.AbilityCooldownRemaining[0], 0.01f);
        }

        [Test]
        public void FacelessCaster_MagicLevel1_CastsBlightingGaze_BurstAndDot()
        {
            var combat = CreateCombat(magicLevel: 1);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var enemy = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(enemy, new Vector3(0f, 0f, 2f));

            combat.Tick(0.1f);

            // Gaze: 30 burst (enemy has no armor) plus a 4/s rot.
            Assert.AreEqual(70f, enemy.CurrentHp, 1.5f, "Blighting Gaze should burst ~30 damage.");
            Assert.Greater(enemy.BurnDamagePerSecond, 0f, "Gaze should apply a damage-over-time rot.");
            Assert.Greater(caster.AbilityCooldownRemaining[0], 0f, "Gaze cooldown should be armed.");
            Assert.AreEqual(0f, caster.AbilityCooldownRemaining[1], 0.01f, "Drain is not unlocked at magic level 1.");
            Assert.AreEqual(0f, caster.AbilityCooldownRemaining[2], 0.01f, "Raise is not unlocked at magic level 1.");
        }

        [Test]
        public void FacelessCaster_MagicLevel1_GazeTargetsHighestHpEnemy()
        {
            var combat = CreateCombat(magicLevel: 1);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var low = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee, maxHp: 50f));
            var high = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee, maxHp: 100f));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(low, new Vector3(0f, 0f, 2f));
            Place(high, new Vector3(0f, 0f, 3f));

            combat.Tick(0.1f);

            // Gaze prioritises the highest-current-HP enemy, per canon.
            Assert.Greater(high.BurnDamagePerSecond, 0f, "Gaze should rot the higher-HP enemy.");
            Assert.AreEqual(0f, low.BurnDamagePerSecond, 0.01f, "The lower-HP enemy must be left untouched.");
            Assert.AreEqual(50f, low.CurrentHp, 0.5f, "Lower-HP enemy should take no damage this tick.");
            Assert.AreEqual(70f, high.CurrentHp, 2f, "Higher-HP enemy takes the 30 burst.");
        }

        [Test]
        public void FacelessCaster_MagicLevel2_CastsVoidDrain_AoeDamageAndLifesteal()
        {
            var combat = CreateCombat(magicLevel: 2);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var a = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var b = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(a, new Vector3(0f, 0f, 2f));
            Place(b, new Vector3(0f, 0f, 3f));

            // Tick 1: slot 0 (Gaze) fires first, hitting one enemy.
            combat.Tick(0.1f);
            Assert.Greater(caster.CastLockRemainingSeconds, 0f, "Gaze must apply a cast lock so spells queue.");

            // The cast lock keeps the caster busy ~1.5s; slot 1 (Void Drain) can only
            // land AFTER that lock expires (spells are strictly sequential, FACELESS-016 fix).
            caster.CurrentHp = 50f;
            for (var i = 0; i < 15; i++)
            {
                combat.Tick(0.1f);
            }

            // 'b' never took Gaze, so its only damage is the 40 Drain burst.
            Assert.AreEqual(60f, b.CurrentHp, 0.5f, "Void Drain should deal 40 AoE damage to each enemy.");
            Assert.Greater(caster.CurrentHp, 50f, "Caster should heal from Drain lifesteal.");
            Assert.LessOrEqual(caster.CurrentHp, 100f, "Heal must clamp at max HP.");
            Assert.Greater(caster.AbilityCooldownRemaining[1], 0f, "Drain cooldown should be armed.");
        }

        [Test]
        public void FacelessCaster_SpellsCastSequentially_NotSimultaneously()
        {
            var combat = CreateCombat(magicLevel: 2);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var a = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var b = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(a, new Vector3(0f, 0f, 2f));
            Place(b, new Vector3(0f, 0f, 3f));

            var castCount = 0;
            combat.AbilityCast += _ => castCount++;

            // First tick: Gaze (slot 0) fires and locks the caster.
            combat.Tick(0.1f);
            Assert.AreEqual(1, castCount, "Exactly one spell should cast on the first tick.");
            Assert.Greater(caster.CastLockRemainingSeconds, 0f, "Caster must be locked while the cast animation plays.");
            var lockAfterFirstCast = caster.CastLockRemainingSeconds;

            // While still locked, the next tick must NOT start another spell — even though
            // Gaze is on cooldown and Drain (slot 1) is ready.
            combat.Tick(0.1f);
            Assert.AreEqual(1, castCount, "No second spell may cast while the caster is still locked.");
            Assert.AreEqual(100f, b.CurrentHp, 0.5f, "Drain must not fire until the previous cast lock ends.");
            Assert.Less(caster.CastLockRemainingSeconds, lockAfterFirstCast, "Lock should be counting down.");

            // After the lock fully expires the queued spell (Drain) finally lands.
            for (var i = 0; i < 15; i++)
            {
                combat.Tick(0.1f);
            }

            Assert.AreEqual(2, castCount, "The second spell should cast only after the lock expired.");
            Assert.AreEqual(60f, b.CurrentHp, 0.5f, "Drain should land once the caster is free again.");
        }

        [Test]
        public void FacelessCaster_MagicLevel3_RaiseDrowned_ConsumesAnyCorpseAndSummonsMinion()
        {
            var combat = CreateCombat(magicLevel: 3);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var enemy = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee, bounty: 5));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(enemy, new Vector3(0f, 0f, 2f));

            // Kill the enemy so it becomes a corpse (within Gaze/Drain range, but no living enemies).
            combat.ApplyExternalDamage(enemy.UnitId, 1000f, killerOwnerSlot: 1);
            Assert.AreEqual(1, combat.Corpses.Count, "Dead enemy should leave a corpse.");
            Assert.AreEqual(1, combat.Units.Count, "Only the caster should remain alive.");

            AbilityCastEvent? cast = null;
            combat.AbilityCast += e => cast = e;

            combat.Tick(0.1f);

            Assert.AreEqual(AbilityIds.RaiseDrowned, cast?.Def.AbilityId, "Raise the Drowned should fire.");
            Assert.AreEqual(2, combat.Units.Count, "Caster + raised minion should be alive.");
            Assert.AreEqual(0, combat.Corpses.Count, "Corpse should be consumed on raise.");

            var minion = combat.GetUnit(cast.Value.TargetUnitId);
            Assert.IsNotNull(minion, "Raise should spawn a minion under the caster's control.");
            Assert.AreEqual(0, minion.OwnerSlot, "Minion must be owned by the Faceless caster.");
            Assert.AreEqual(UnitRole.Melee, minion.Role, "Raise summons a melee minion.");
            Assert.Greater(caster.AbilityCooldownRemaining[2], 0f, "Raise cooldown should be armed.");
        }

        [Test]
        public void FacelessCaster_RaiseDrowned_WorksOnEnemyCorpse()
        {
            var combat = CreateCombat(magicLevel: 3);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var enemy = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee, bounty: 5));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(enemy, new Vector3(0f, 0f, 2f));

            // Enemy-owned corpse — Faceless raise ANY corpse, unlike the Human Resurrect.
            combat.ApplyExternalDamage(enemy.UnitId, 1000f, killerOwnerSlot: 0);
            Assert.AreEqual(1, combat.Corpses.Count);
            Assert.AreEqual(1, combat.Corpses[0].OwnerSlot, "Corpse should be enemy-owned.");

            combat.Tick(0.1f);

            Assert.AreEqual(2, combat.Units.Count, "Enemy corpse still becomes a Faceless minion.");
            Assert.AreEqual(0, combat.Corpses.Count, "Enemy corpse should be consumed.");
        }
    }
}
