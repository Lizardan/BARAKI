using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CasterSpellRulesTests
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
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
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
        public void Caster_NoMagicLevel_DoesNotCast()
        {
            var combat = CreateCombat(magicLevel: 0);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var ally = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(ally, new Vector3(0f, 0f, 1f));
            ally.CurrentHp = 30f;

            combat.Tick(0.1f);

            Assert.AreEqual(30f, ally.CurrentHp, 0.01f, "Heal should not fire without magic level 1.");
            Assert.AreEqual(0f, caster.AbilityCooldownRemaining[0], 0.01f);
        }

        [Test]
        public void Caster_MagicLevel1_CastsHealOnLowestHpAlly_ClampedToMax()
        {
            var combat = CreateCombat(magicLevel: 1);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var low = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var high = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(low, new Vector3(0f, 0f, 1f));
            Place(high, new Vector3(0f, 0f, -1f));
            low.CurrentHp = 30f;
            high.CurrentHp = 70f;

            combat.Tick(0.1f);

            Assert.AreEqual(100f, low.CurrentHp, 0.01f, "Lowest-HP ally should be healed and clamped to max.");
            Assert.AreEqual(70f, high.CurrentHp, 0.01f, "Other ally should not be healed in the same tick.");
            Assert.Greater(caster.AbilityCooldownRemaining[0], 0f, "Heal cooldown should be armed.");
        }

        [Test]
        public void Caster_HealCooldown_BlocksRepeatedCast_ThenRecovers()
        {
            var combat = CreateCombat(magicLevel: 1);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var first = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var second = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(first, new Vector3(0f, 0f, 1f));
            Place(second, new Vector3(0f, 0f, -1f));
            first.CurrentHp = 30f;
            second.CurrentHp = 60f;

            combat.Tick(0.1f);
            Assert.AreEqual(100f, first.CurrentHp, 0.01f);

            combat.Tick(0.1f);
            Assert.AreEqual(60f, second.CurrentHp, 0.01f, "Heal on cooldown must not re-cast.");

            combat.Tick(CasterSpellRules.HealCooldownSeconds);
            Assert.AreEqual(100f, second.CurrentHp, 0.01f, "Heal should fire again after cooldown elapsed.");
        }

        [Test]
        public void Caster_MagicLevel2_CastsFrost_DealsAoeDamageToAllEnemiesInRadius()
        {
            var combat = CreateCombat(magicLevel: 2);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var e1 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var e2 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var e3 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(e1, new Vector3(0f, 0f, 2f));
            Place(e2, new Vector3(0f, 0f, 3f));
            Place(e3, new Vector3(0f, 0f, 4f));

            combat.Tick(0.1f);

            Assert.AreEqual(60f, e1.CurrentHp, 0.01f, "Frost should deal 40 damage to each enemy in AoE.");
            Assert.AreEqual(60f, e2.CurrentHp, 0.01f);
            Assert.AreEqual(60f, e3.CurrentHp, 0.01f);
            Assert.Greater(caster.AbilityCooldownRemaining[1], 0f, "Frost cooldown should be armed.");
        }

        [Test]
        public void Caster_FrostTargetsDensestCluster_LeavesSparseClusterUntouched()
        {
            var combat = CreateCombat(magicLevel: 2);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var a1 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var a2 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var a3 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var b1 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var b2 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));

            Place(caster, new Vector3(0f, 0f, 0f));
            Place(a1, new Vector3(3f, 0f, 3f));
            Place(a2, new Vector3(4f, 0f, 3f));
            Place(a3, new Vector3(3f, 0f, 4f));
            Place(b1, new Vector3(3f, 0f, -3f));
            Place(b2, new Vector3(4f, 0f, -3f));

            combat.Tick(0.1f);

            Assert.AreEqual(60f, a1.CurrentHp, 0.01f, "Densest cluster (3 enemies) should take the burst.");
            Assert.AreEqual(60f, a2.CurrentHp, 0.01f);
            Assert.AreEqual(60f, a3.CurrentHp, 0.01f);
            Assert.AreEqual(100f, b1.CurrentHp, 0.01f, "Sparse cluster (2 enemies) must be untouched.");
            Assert.AreEqual(100f, b2.CurrentHp, 0.01f);
        }

        [Test]
        public void Caster_MagicLevel3_ResurrectsRecentCorpse_FullHpSameLaneAndOwner()
        {
            var combat = CreateCombat(magicLevel: 3);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var ally = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee, bounty: 5));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(ally, new Vector3(0f, 0f, 2f));
            ally.CurrentHp = 40f;

            combat.ApplyExternalDamage(ally.UnitId, 1000f, killerOwnerSlot: 1);
            Assert.AreEqual(1, combat.Corpses.Count);

            AbilityCastEvent? cast = null;
            combat.AbilityCast += e => cast = e;

            combat.Tick(0.1f);

            Assert.AreEqual(AbilityIds.Resurrect, cast?.Def.AbilityId);
            Assert.AreEqual(2, combat.Units.Count, "Caster + revived unit should be alive.");
            Assert.AreEqual(0, combat.Corpses.Count, "Corpse should be consumed on resurrect.");

            var revived = combat.GetUnit(cast.Value.TargetUnitId);
            Assert.IsNotNull(revived);
            Assert.AreNotEqual(ally.UnitId, revived.UnitId);
            Assert.AreEqual(0, revived.OwnerSlot);
            Assert.AreEqual(Center, revived.LaneId);
            Assert.AreEqual(UnitRole.Melee, revived.Role);
            Assert.AreEqual(ally.Stats.MaxHp, revived.CurrentHp, 0.01f, "Revived unit spawns at full health.");
            Assert.Greater(caster.AbilityCooldownRemaining[2], 0f, "Resurrect cooldown should be armed.");
        }

        [Test]
        public void Caster_Frost_FreezesVictimsAndStopsTheirMovement()
        {
            var combat = CreateCombat(magicLevel: 2);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var e1 = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee, moveSpeed: 5f));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(e1, new Vector3(0f, 0f, 2f));

            combat.Tick(0.1f);

            Assert.AreEqual(60f, e1.CurrentHp, 0.01f, "Frost still deals its AoE damage.");
            Assert.Greater(e1.FrozenRemainingSeconds, 0f, "Frost victim should be frozen.");
            Assert.AreEqual(UnitBehaviorState.Frozen, e1.BehaviorState, "Frozen unit should report Frozen behavior state.");

            var frozenAt = e1.WorldPosition;
            var ticks = 0;
            while (e1.FrozenRemainingSeconds > 0f && ticks < 100)
            {
                combat.Tick(0.1f);
                Assert.AreEqual(frozenAt, e1.WorldPosition, "Frozen unit must not move while stunned.");
                ticks++;
            }

            Assert.Greater(ticks, 0);
            Assert.AreEqual(0f, e1.FrozenRemainingSeconds, 0.01f, "Freeze should expire after FrostFreezeSeconds.");
            combat.Tick(0.1f);
            Assert.AreNotEqual(frozenAt, e1.WorldPosition, "Unit should resume moving after the freeze ends.");
        }

        [Test]
        public void Caster_Resurrect_ExpiredCorpseIsCulled()
        {
            var combat = CreateCombat(magicLevel: 3);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var ally = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            // Out of cast range so the caster cannot resurrect it.
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(ally, new Vector3(10f, 0f, 10f));

            combat.ApplyExternalDamage(ally.UnitId, 1000f, killerOwnerSlot: 1);
            Assert.AreEqual(1, combat.Corpses.Count);

            combat.Tick(0.1f);
            Assert.AreEqual(1, combat.Corpses.Count, "Fresh corpse should persist while in the window.");

            combat.Tick(CasterSpellRules.ResurrectCorpseMaxAgeSeconds + 1f);
            Assert.AreEqual(0, combat.Corpses.Count, "Corpse older than the window should be culled.");
            Assert.AreEqual(1, combat.Units.Count);
        }

        [Test]
        public void Caster_HealTakesPriorityOverFrostAndResurrect()
        {
            var combat = CreateCombat(magicLevel: 3);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var ally = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            var enemy = combat.SpawnUnit(1, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(ally, new Vector3(0f, 0f, 1f));
            Place(enemy, new Vector3(0f, 0f, -1f));
            ally.CurrentHp = 30f;

            AbilityCastEvent? cast = null;
            combat.AbilityCast += e => cast = e;

            combat.Tick(0.1f);

            Assert.AreEqual(AbilityIds.CasterHeal, cast?.Def.AbilityId);
            Assert.AreEqual(100f, ally.CurrentHp, 0.01f);
            Assert.AreEqual(100f, enemy.CurrentHp, 0.01f, "Frost must not be cast while heal has a valid target.");
        }

        [Test]
        public void Caster_Cast_ConsumesMana()
        {
            var combat = CreateCombat(magicLevel: 1);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var ally = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(ally, new Vector3(0f, 0f, 1f));
            ally.CurrentHp = 30f;

            Assert.AreEqual(200f, caster.CurrentMana, 0.01f, "Caster should spawn at full mana.");

            combat.Tick(0.1f);

            Assert.AreEqual(100f, ally.CurrentHp, 0.01f);
            Assert.AreEqual(
                200f - CasterSpellRules.HealManaCost,
                caster.CurrentMana,
                0.01f,
                "Heal should spend its mana cost (regen clamps at the full pool first).");
        }

        [Test]
        public void Caster_NotEnoughMana_DoesNotCast()
        {
            var combat = CreateCombat(magicLevel: 1);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            var ally = combat.SpawnUnit(0, Center, UnitRole.Melee, UnitStats(UnitRole.Melee));
            Place(caster, new Vector3(0f, 0f, 0f));
            Place(ally, new Vector3(0f, 0f, 1f));
            ally.CurrentHp = 30f;
            caster.CurrentMana = 0f;

            combat.Tick(0.1f);

            Assert.AreEqual(30f, ally.CurrentHp, 0.01f, "No heal without enough mana.");
            Assert.AreEqual(0f, caster.AbilityCooldownRemaining[0], 0.01f, "Cooldown must not arm on a failed cast.");
        }

        [Test]
        public void Caster_ManaRegen_FillsAndClampsToMax()
        {
            var combat = CreateCombat(magicLevel: 1);
            var caster = combat.SpawnUnit(0, Center, UnitRole.Caster, CasterStats());
            // No valid heal target (caster at full HP, no allies/foes) -> pure regen path.
            Place(caster, new Vector3(0f, 0f, 0f));
            caster.CurrentMana = 10f;

            combat.Tick(1f);
            Assert.AreEqual(15f, caster.CurrentMana, 0.01f, "Mana should regen at 5/s.");

            combat.Tick(40f);
            Assert.AreEqual(200f, caster.CurrentMana, 0.01f, "Mana should clamp at the pool max.");
        }
    }
}
