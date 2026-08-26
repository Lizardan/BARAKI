using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BuildingAbilityRulesTests
    {
        [Test]
        public void Ids_And_Slots_AreStable()
        {
            Assert.AreEqual(1, BuildingAbilityRules.IceRingId);
            Assert.AreEqual(2, BuildingAbilityRules.WaveOfLightId);
            Assert.IsTrue(BuildingAbilityRules.IsValidId(BuildingAbilityRules.IceRingId));
            Assert.IsTrue(BuildingAbilityRules.IsValidId(BuildingAbilityRules.WaveOfLightId));
            Assert.IsFalse(BuildingAbilityRules.IsValidId(0));
            Assert.IsFalse(BuildingAbilityRules.IsValidId(3));

            // Main panel: 9 = Ice Ring, 10 = Wave of Light, 11 = Divine Blessing (last).
            Assert.AreEqual(9, BuildingAbilityRules.IceRingSlotIndex);
            Assert.AreEqual(10, BuildingAbilityRules.WaveOfLightSlotIndex);
            Assert.AreEqual(11, MainExtraAbilityRules.CommandSlotIndex);
        }

        [Test]
        public void Level_Gate_FollowsBuildingLevel()
        {
            Assert.IsTrue(BuildingAbilityRules.IsUnlocked(BuildingAbilityRules.IceRingId, 1));
            Assert.IsFalse(BuildingAbilityRules.IsUnlocked(BuildingAbilityRules.WaveOfLightId, 1));
            Assert.IsTrue(BuildingAbilityRules.IsUnlocked(BuildingAbilityRules.WaveOfLightId, 2));
            Assert.IsTrue(BuildingAbilityRules.IsUnlocked(BuildingAbilityRules.WaveOfLightId, 3));
        }

        [Test]
        public void Tuning_MatchesSpec()
        {
            Assert.AreEqual(50f, BuildingAbilityRules.GetManaCost(BuildingAbilityRules.IceRingId));
            Assert.AreEqual(150f, BuildingAbilityRules.GetManaCost(BuildingAbilityRules.WaveOfLightId));
            Assert.AreEqual(60f, BuildingAbilityRules.GetCooldownSeconds(BuildingAbilityRules.IceRingId));
            Assert.AreEqual(180f, BuildingAbilityRules.GetCooldownSeconds(BuildingAbilityRules.WaveOfLightId));
            Assert.AreEqual(1, BuildingAbilityRules.GetRequiredMainLevel(BuildingAbilityRules.IceRingId));
            Assert.AreEqual(2, BuildingAbilityRules.GetRequiredMainLevel(BuildingAbilityRules.WaveOfLightId));
            // Ice ring AoE = mage Frost circle.
            Assert.AreEqual(CasterSpellRules.FrostRadius, BuildingAbilityRules.IceRingRadius);
            Assert.AreEqual(3f, BuildingAbilityRules.GetFreezeSeconds(BuildingAbilityRules.IceRingId));
            Assert.AreEqual(0f, BuildingAbilityRules.GetFreezeSeconds(BuildingAbilityRules.WaveOfLightId));
        }

        [Test]
        public void CanCast_ChecksMana_Cooldown_AndLevel()
        {
            var player = new MatchPlayerState(0, "RACE_HUMAN", 100)
            {
                MainMana = 200f,
                WaveOfLightCooldownRemaining = 5f,
            };

            // L2 pool is full but wave is on cooldown.
            player.MainLevel = 2;
            Assert.IsFalse(BuildingAbilityRules.CanCast(player, BuildingAbilityRules.WaveOfLightId));

            player.WaveOfLightCooldownRemaining = 0f;
            Assert.IsTrue(BuildingAbilityRules.CanCast(player, BuildingAbilityRules.WaveOfLightId));

            // Ice ring on cooldown + not enough mana at L1 pool.
            player.MainLevel = 1;
            player.MainMana = 40f;
            player.IceRingCooldownRemaining = 10f;
            Assert.IsFalse(BuildingAbilityRules.CanCast(player, BuildingAbilityRules.IceRingId));

            player.MainMana = 50f;
            Assert.IsFalse(
                BuildingAbilityRules.CanCast(player, BuildingAbilityRules.IceRingId),
                "cooldown still ticking");

            player.IceRingCooldownRemaining = 0f;
            Assert.IsTrue(BuildingAbilityRules.CanCast(player, BuildingAbilityRules.IceRingId));

            // Wave gated by building level even with resources.
            Assert.IsFalse(BuildingAbilityRules.CanCast(player, BuildingAbilityRules.WaveOfLightId));
        }

        [Test]
        public void Cooldown_Helpers_Roundtrip()
        {
            var player = new MatchPlayerState(0, "RACE_HUMAN", 0);
            Assert.AreEqual(0f, BuildingAbilityRules.GetCooldownRemaining(player, BuildingAbilityRules.IceRingId));

            BuildingAbilityRules.SetCooldown(player, BuildingAbilityRules.WaveOfLightId, 90f);
            Assert.AreEqual(90f, BuildingAbilityRules.GetCooldownRemaining(player, BuildingAbilityRules.WaveOfLightId));

            BuildingAbilityRules.TickCooldowns(player, 30f);
            Assert.AreEqual(60f, BuildingAbilityRules.GetCooldownRemaining(player, BuildingAbilityRules.WaveOfLightId));

            BuildingAbilityRules.TickCooldowns(player, 120f);
            Assert.AreEqual(0f, BuildingAbilityRules.GetCooldownRemaining(player, BuildingAbilityRules.WaveOfLightId));
        }

        [Test]
        public void Range_Math_UsesBarracksDistanceFactors()
        {
            const float distance = 12.5f;
            Assert.AreEqual(25f, BuildingAbilityRules.GetIceRingCastRange(distance));
            Assert.AreEqual(62.5f, BuildingAbilityRules.GetWaveOfLightRadius(distance));
        }

        [Test]
        public void IsInCastRange_IsHorizontal()
        {
            var basePosition = new Vector3(0f, 0.15f, 0f);
            Assert.IsTrue(BuildingAbilityRules.IsInCastRange(basePosition, new Vector3(20f, 5f, 15f), 25f));
            Assert.IsFalse(BuildingAbilityRules.IsInCastRange(basePosition, new Vector3(21f, 0f, 15f), 25f));
            // Border counts as inside.
            Assert.IsTrue(BuildingAbilityRules.IsInCastRange(basePosition, new Vector3(25f, 0f, 0f), 25f));
        }

        [Test]
        public void AffectsUnit_FiltersFlying_Allies_AndDead()
        {
            var enemyGround = TestUnit(ownerSlot: 1, role: UnitRole.Melee, hp: 100f);
            var enemyFlying = TestUnit(ownerSlot: 1, role: UnitRole.Flying, hp: 100f);
            var enemyHero = TestUnit(ownerSlot: 1, role: UnitRole.Hero, hp: 100f);
            var deadEnemy = TestUnit(ownerSlot: 1, role: UnitRole.Super, hp: 0f);
            var ally = TestUnit(ownerSlot: 0, role: UnitRole.Melee, hp: 100f);

            Assert.IsTrue(BuildingAbilityRules.AffectsUnit(enemyGround, casterOwnerSlot: 0));
            Assert.IsTrue(BuildingAbilityRules.AffectsUnit(enemyHero, casterOwnerSlot: 0));
            Assert.IsFalse(BuildingAbilityRules.AffectsUnit(enemyFlying, casterOwnerSlot: 0));
            Assert.IsFalse(BuildingAbilityRules.AffectsUnit(deadEnemy, casterOwnerSlot: 0));
            Assert.IsFalse(BuildingAbilityRules.AffectsUnit(ally, casterOwnerSlot: 0));
            Assert.IsFalse(BuildingAbilityRules.AffectsUnit(null, casterOwnerSlot: 0));
        }

        static MatchUnitState TestUnit(int ownerSlot, UnitRole role, float hp) =>
            new MatchUnitState(
                unitId: 1,
                ownerSlot: ownerSlot,
                laneId: "LANE_CENTER",
                role: role,
                stats: default,
                currentHp: hp,
                worldPosition: Vector3.zero);
    }
}
