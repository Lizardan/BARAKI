using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class DivineBlessingMatchControllerTests
    {
        [Test]
        public void TryStartDivineBlessing_RequiresMainLevelTwoAndSpendsGold()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.Gold = 1000;
            var main = FindMain(controller);

            Assert.IsFalse(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.DivineBlessing));

            player.MainLevel = 2;
            player.SyncMainManaMax(fillToMax: true);
            Assert.IsTrue(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.DivineBlessing));
            Assert.AreEqual(0, player.Gold);
            Assert.IsFalse(player.DivineBlessingComplete);

            controller.Tick(45f);
            Assert.IsTrue(player.DivineBlessingComplete);
            Assert.IsFalse(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.DivineBlessing));
        }

        [Test]
        public void TryPickMainExtraAbility_OnceAfterBlessing()
        {
            var controller = CreateEarlyMatch();
            var player = UnlockGates(controller.Players[0]);
            player.DivineBlessingComplete = true;

            Assert.IsTrue(controller.TryPickMainExtraAbility(0, 1));
            Assert.AreEqual(1, player.MainExtraAbilityId);
            Assert.IsFalse(controller.TryPickMainExtraAbility(0, 1));
            Assert.IsFalse(controller.TryPickMainExtraAbility(0, 2));
        }

        [Test]
        public void TryPickMainExtraAbility_RejectsLockedStub()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.DivineBlessingComplete = true;
            player.MagicLevel = 0;

            Assert.IsFalse(controller.TryPickMainExtraAbility(0, 1));
            Assert.AreEqual(MainExtraAbilityRules.None, player.MainExtraAbilityId);
            Assert.IsFalse(controller.TryPickMainExtraAbility(0, 3));
        }

        [Test]
        public void TryCastMainExtraAbility_BuildingSmiteDamagesEnemyBuilding()
        {
            var controller = CreateEarlyMatch();
            var player = UnlockGates(controller.Players[0]);
            player.DivineBlessingComplete = true;
            player.MainLevel = 2;
            player.SyncMainManaMax(fillToMax: true);
            Assert.IsTrue(controller.TryPickMainExtraAbility(0, MainExtraAbilityRules.BuildingSmiteId));

            var enemyBarracks = FindEnemyBarracks(controller);
            var hpBefore = enemyBarracks.CurrentHp;
            Assert.IsTrue(controller.TryCastMainExtraAbility(0, enemyBarracks.InstanceId, 0));
            var expectedHp = Mathf.Max(0f, hpBefore - MainExtraAbilityRules.BuildingSmiteDamage);
            Assert.AreEqual(expectedHp, enemyBarracks.CurrentHp, 0.01f);
            Assert.AreEqual(0f, player.MainMana, 0.01f);
            Assert.AreEqual(MainExtraAbilityRules.CooldownSeconds, player.MainExtraAbilityCooldownRemaining, 0.01f);
            Assert.IsFalse(controller.TryCastMainExtraAbility(0, enemyBarracks.InstanceId, 0));

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.MainBuildingSmite, casts[0].Def.AbilityId);
            Assert.AreEqual(AbilityFxColors.DivineSmite, casts[0].Def.Fx.Color);
        }

        [Test]
        public void TryCastMainExtraAbility_UnitSmiteKillsEnemyUnit()
        {
            var controller = CreateEarlyMatch();
            var player = UnlockGates(controller.Players[0]);
            player.DivineBlessingComplete = true;
            player.MainLevel = 2;
            player.SyncMainManaMax(fillToMax: true);
            Assert.IsTrue(controller.TryPickMainExtraAbility(0, MainExtraAbilityRules.UnitSmiteId));

            var enemy = SpawnEnemyMelee(controller);
            Assert.IsTrue(controller.TryCastMainExtraAbility(0, 0, enemy.UnitId));
            Assert.IsNull(controller.Combat.GetUnit(enemy.UnitId));
            Assert.AreEqual(MainExtraAbilityRules.CooldownSeconds, player.MainExtraAbilityCooldownRemaining, 0.01f);

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.MainUnitSmite, casts[0].Def.AbilityId);
            Assert.AreEqual(enemy.UnitId, casts[0].TargetUnitId);
            Assert.AreEqual(AbilityFxColors.DivineSmite, casts[0].Def.Fx.Color);
        }

        [Test]
        public void MainLevelUp_RaisesMainHpAndMana()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            var main = FindMain(controller);
            Assert.AreEqual(2000f, main.MaxHp, 0.01f);
            Assert.AreEqual(100f, player.MainManaMax, 0.01f);

            player.Gold = 10000;
            Assert.IsTrue(controller.TryStartResearch(0, main.InstanceId, GameIds.Upgrades.MainBuildingLevel));
            controller.Tick(120f);

            Assert.AreEqual(2, player.MainLevel);
            Assert.AreEqual(2500f, main.MaxHp, 0.01f);
            Assert.AreEqual(200f, player.MainManaMax, 0.01f);
            Assert.GreaterOrEqual(main.CurrentHp, 2500f - 0.01f);
        }

        [Test]
        public void BarracksLevelUp_RaisesBarracksHp()
        {
            var controller = CreateEarlyMatch();
            var barracks = FindOwnBarracks(controller);
            Assert.AreEqual(800f, barracks.MaxHp, 0.01f);

            controller.Players[0].Gold = 10000;
            Assert.IsTrue(controller.TryStartResearch(0, barracks.InstanceId, GameIds.Upgrades.BarracksLevel));
            controller.Tick(3f);

            Assert.AreEqual(1100f, barracks.MaxHp, 0.01f);
            Assert.GreaterOrEqual(barracks.CurrentHp, 1100f - 0.01f);
        }

        static MatchPlayerState UnlockGates(MatchPlayerState player)
        {
            player.MainLevel = 2;
            player.MeleeDamageLevel = 7;
            player.RangedDamageLevel = 7;
            player.HpArmorLevel = 7;
            player.MagicLevel = 2;
            player.SyncMainManaMax(fillToMax: true);
            return player;
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
        }

        static BuildingState FindMain(MatchController controller)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == 0 && building.BuildingId == GameIds.Buildings.Main)
                {
                    return building;
                }
            }

            Assert.Fail("Main building not found.");
            return null;
        }

        static BuildingState FindOwnBarracks(MatchController controller)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == 0 && BuildingRules.IsBarracks(building.BuildingId))
                {
                    return building;
                }
            }

            Assert.Fail("Own barracks not found.");
            return null;
        }

        static BuildingState FindEnemyBarracks(MatchController controller)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == 1 && BuildingRules.IsBarracks(building.BuildingId))
                {
                    return building;
                }
            }

            Assert.Fail("Enemy barracks not found.");
            return null;
        }

        static MatchUnitState SpawnEnemyMelee(MatchController controller)
        {
            var stats = new UnitCombatStats(
                Game.Gameplay.Data.UnitRole.Melee,
                100f,
                0f,
                1f,
                1f,
                1f,
                1f,
                1f,
                10,
                0f);
            return controller.Combat.SpawnUnit(
                1,
                GameIds.Lanes.Center,
                Game.Gameplay.Data.UnitRole.Melee,
                stats);
        }
    }
}
