using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BuildingAbilityMatchControllerTests
    {
        [Test]
        public void IceRing_DamagesAndFreezesGroundEnemiesNearBase()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.SyncMainManaMax(fillToMax: true);
            var enemy = SpawnEnemy(controller, UnitRole.Melee, maxHp: 1000f);
            var basePosition = GetBasePosition(controller, ownerSlot: 0);
            enemy.WorldPosition = basePosition + new Vector3(2f, 0f, 0f);

            Assert.IsTrue(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.IceRingId,
                basePosition));

            Assert.AreEqual(1000f - BuildingAbilityRules.IceRingDamage, enemy.CurrentHp, 0.01f);
            Assert.AreEqual(BuildingAbilityRules.IceRingFreezeSeconds, enemy.FrozenRemainingSeconds, 0.01f);
            Assert.AreEqual(
                MainExtraAbilityRules.GetMainManaMax(player.MainLevel) - BuildingAbilityRules.IceRingManaCost,
                player.MainMana,
                0.01f);
            Assert.AreEqual(BuildingAbilityRules.IceRingCooldownSeconds,
                player.IceRingCooldownRemaining, 0.01f);

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.MainIceRing, casts[0].Def.AbilityId);
            Assert.AreEqual(basePosition.x, casts[0].CenterPosition.x, 0.01f);
            Assert.AreEqual(basePosition.z, casts[0].CenterPosition.z, 0.01f);
            Assert.Greater(casts[0].Radius, 0f);
        }

        [Test]
        public void IceRing_RejectsOutOfRange_SkipsFlyingAndAllies()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.SyncMainManaMax(fillToMax: true);
            var basePosition = GetBasePosition(controller, ownerSlot: 0);
            var barracksDistance = BuildingAbilityRules.GetBaseToBarracksDistance(controller.Layout, 0);
            var castRange = BuildingAbilityRules.GetIceRingCastRange(barracksDistance);

            var flying = SpawnEnemy(controller, UnitRole.Flying, maxHp: 1000f);
            var ally = SpawnAlly(controller, UnitRole.Melee, maxHp: 1000f);
            flying.WorldPosition = basePosition + new Vector3(3f, 0f, 0f);
            ally.WorldPosition = basePosition + new Vector3(-3f, 0f, 0f);

            // Out of range: no cast, no resources spent.
            var outside = basePosition + new Vector3(castRange + 10f, 0f, 0f);
            Assert.IsFalse(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.IceRingId,
                outside));
            Assert.AreEqual(1000f, flying.CurrentHp, 0.01f);
            Assert.AreEqual(MainExtraAbilityRules.GetMainManaMax(player.MainLevel), player.MainMana, 0.01f);

            // In range: ground enemy hit, flying and allies untouched.
            Assert.IsTrue(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.IceRingId,
                basePosition));
            Assert.AreEqual(1000f, flying.CurrentHp, 0.01f);
            Assert.AreEqual(1000f, ally.CurrentHp, 0.01f);
        }

        [Test]
        public void WaveOfLight_GatedByLevelTwo_HitsEnemiesAroundBase()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.SyncMainManaMax(fillToMax: true);

            // L1 pool is 100 — wave costs 150 by design.
            Assert.IsFalse(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.WaveOfLightId,
                Vector3.zero));

            player.MainLevel = 2;
            player.SyncMainManaMax(fillToMax: true);

            var basePosition = GetBasePosition(controller, ownerSlot: 0);
            var waveRadius = BuildingAbilityRules.GetWaveOfLightRadius(
                BuildingAbilityRules.GetBaseToBarracksDistance(controller.Layout, 0));
            var nearEnemy = SpawnEnemy(controller, UnitRole.Ranged, maxHp: 5000f);
            var farEnemy = SpawnEnemy(controller, UnitRole.Super, maxHp: 5000f);
            nearEnemy.WorldPosition = basePosition + new Vector3(5f, 0f, 0f);
            farEnemy.WorldPosition =
                basePosition + new Vector3(waveRadius + 20f, 0f, 0f);

            // Center argument is ignored for the wave — it always radiates from the base.
            Assert.IsTrue(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.WaveOfLightId,
                Vector3.zero));

            Assert.AreEqual(5000f - BuildingAbilityRules.WaveOfLightDamage, nearEnemy.CurrentHp, 0.01f);
            Assert.AreEqual(5000f, farEnemy.CurrentHp, 0.01f);
            Assert.AreEqual(0f, nearEnemy.FrozenRemainingSeconds, 0.01f);
            Assert.AreEqual(
                MainExtraAbilityRules.GetMainManaMax(2) - BuildingAbilityRules.WaveOfLightManaCost,
                player.MainMana,
                0.01f);
            Assert.AreEqual(BuildingAbilityRules.WaveOfLightCooldownSeconds,
                player.WaveOfLightCooldownRemaining, 0.01f);

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.MainWaveOfLight, casts[0].Def.AbilityId);
        }

        [Test]
        public void WaveOfLight_IsBlockedWhileOnCooldown()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.MainLevel = 2;
            player.SyncMainManaMax(fillToMax: true);

            Assert.IsTrue(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.WaveOfLightId,
                Vector3.zero));
            Assert.IsFalse(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.WaveOfLightId,
                Vector3.zero));

            player.WaveOfLightCooldownRemaining = 0f;
            player.MainMana = BuildingAbilityRules.WaveOfLightManaCost;
            Assert.IsTrue(controller.TryCastBuildingAbility(
                0,
                BuildingAbilityRules.WaveOfLightId,
                Vector3.zero));
        }

        [Test]
        public void Cooldowns_TickDownThroughControllerUpdate()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            BuildingAbilityRules.SetCooldown(
                player, BuildingAbilityRules.IceRingId, BuildingAbilityRules.IceRingCooldownSeconds);

            controller.Tick(BuildingAbilityRules.IceRingCooldownSeconds);

            Assert.AreEqual(0f, player.IceRingCooldownRemaining, 0.01f);
        }

        [Test]
        public void FxDefs_ExposeRussianDisplayNamesForBuildingAbilities()
        {
            Assert.AreEqual("Ледяное кольцо",
                MainExtraAbilityFxDefs.Get(AbilityIds.MainIceRing).DisplayName);
            Assert.AreEqual("Волна света",
                MainExtraAbilityFxDefs.Get(AbilityIds.MainWaveOfLight).DisplayName);
            Assert.AreSame(
                MainExtraAbilityFxDefs.GetForBuildingAbility(BuildingAbilityRules.IceRingId),
                MainExtraAbilityFxDefs.Get(AbilityIds.MainIceRing));
            Assert.IsNull(MainExtraAbilityFxDefs.GetForBuildingAbility(99));
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
        }

        static Vector3 GetBasePosition(MatchController controller, int ownerSlot) =>
            controller.Layout.Slots[ownerSlot].GetBuildingWorldPosition(GameIds.Buildings.Main);

        static MatchUnitState SpawnEnemy(
            MatchController controller,
            UnitRole role,
            float maxHp) =>
            controller.Combat.SpawnUnit(
                1,
                GameIds.Lanes.Center,
                role,
                CreateStats(role, maxHp));

        static MatchUnitState SpawnAlly(
            MatchController controller,
            UnitRole role,
            float maxHp) =>
            controller.Combat.SpawnUnit(
                0,
                GameIds.Lanes.Center,
                role,
                CreateStats(role, maxHp));

        static UnitCombatStats CreateStats(UnitRole role, float maxHp) =>
            new UnitCombatStats(
                role,
                maxHp,
                armor: 0f,
                damageMin: 1f,
                damageMax: 1f,
                attackSpeed: 1f,
                attackRange: 1f,
                moveSpeed: 1f,
                goldBounty: 10);
    }
}
