using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class FacelessTowerTrackTests
    {
        [Test]
        public void ChitinousHide_AppliesArmorAndMaxHp()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Faceless, 0);
            player.SetTowerTrackLevels(new[] { 3, 0, 0, 0, 0, 0, 0, 0, 0 });

            var stats = new UnitCombatStats(UnitRole.Melee, 200f, 0f, 10f, 10f, 1f, 1.5f, 4f, 8);
            var modified = FacelessTowerTrackUnitRules.Apply(stats, player);

            Assert.AreEqual(3f * FacelessTowerTrackRules.ChitinousHideArmorPerLevel, modified.Armor);
            Assert.AreEqual(200f * (1f + FacelessTowerTrackRules.ChitinousHideMaxHpPercentAtMaxLevel), modified.MaxHp);
        }

        [Test]
        public void HollowBarbs_ArmorPenReducesEffectiveArmor()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 2, 0, 0, 0, 0, 0, 0, 0 });

            var attacker = Spawn(combat, 0, UnitRole.Ranged);
            var target = Spawn(combat, 1, UnitRole.Melee, new UnitCombatStats(UnitRole.Melee, 200f, 5f, 10f, 10f, 1f, 1.5f, 4f, 8));

            var hpBefore = target.CurrentHp;
            combat.ApplyDamage(attacker, target, 20f, attacker.OwnerSlot);
            var lossWithPen = hpBefore - target.CurrentHp;

            controller.Players[0].SetTowerTrackLevels(new int[9]);
            var attacker2 = Spawn(combat, 0, UnitRole.Ranged);
            var target2 = Spawn(combat, 1, UnitRole.Melee, new UnitCombatStats(UnitRole.Melee, 200f, 5f, 10f, 10f, 1f, 1.5f, 4f, 8));
            var hpBefore2 = target2.CurrentHp;
            combat.ApplyDamage(attacker2, target2, 20f, attacker2.OwnerSlot);
            var lossWithoutPen = hpBefore2 - target2.CurrentHp;

            Assert.Greater(lossWithPen, lossWithoutPen, "Hollow Barbs armor pen must increase damage");
        }

        [Test]
        public void VacuumCollapse_DeathSlowsNearbyEnemies()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 2, 0, 0, 0, 0, 0, 0 });

            var victim = Spawn(combat, 0, UnitRole.Melee);
            var enemy = Spawn(combat, 1, UnitRole.Melee);
            enemy.WorldPosition = victim.WorldPosition + new Vector3(2f, 0f, 0f);

            combat.ApplyDamage(null, victim, 1000f, 1);
            Assert.Greater(enemy.SlowRemainingSeconds, 0f);
            Assert.AreEqual(FacelessTowerTrackRules.VacuumCollapseSlowPercentByLevel[1], enemy.SlowPercent);
        }

        [Test]
        public void RitualOfTheDeep_CasterNearbyReducesIncomingDamage()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 2, 0, 0, 0, 0, 0 });

            var target = Spawn(combat, 0, UnitRole.Melee);
            var caster = Spawn(combat, 0, UnitRole.Caster);
            caster.WorldPosition = target.WorldPosition + new Vector3(2f, 0f, 0f);

            var attacker = Spawn(combat, 1, UnitRole.Melee);
            var hpBefore = target.CurrentHp;
            combat.ApplyDamage(attacker, target, 20f, attacker.OwnerSlot);
            var lossWithRitual = hpBefore - target.CurrentHp;

            // Kill caster, ritual no longer applies
            combat.ApplyDamage(null, caster, 1000f, 1);
            var target2 = Spawn(combat, 0, UnitRole.Melee);
            var hpBefore2 = target2.CurrentHp;
            combat.ApplyDamage(attacker, target2, 20f, attacker.OwnerSlot);
            var lossWithoutRitual = hpBefore2 - target2.CurrentHp;

            Assert.Greater(lossWithoutRitual, lossWithRitual, "Ritual of the Deep must reduce damage when caster is nearby");
        }

        [Test]
        public void UnnervingAim_AppliesArmorDebuff()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 0, 2, 0, 0, 0, 0 });

            var attacker = Spawn(combat, 0, UnitRole.Ranged);
            var target = Spawn(combat, 1, UnitRole.Melee);

            combat.ResolveProjectileImpact(UnitShot(attacker, target));
            Assert.Greater(target.ArmorDebuffRemainingSeconds, 0f);
            Assert.AreEqual(FacelessTowerTrackRules.UnnervingAimArmorDebuffByLevel[1], target.ArmorDebuffAmount);
        }

        [Test]
        public void FrenzyOfTheDeep_ScalesAttackSpeed()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Faceless, 0);
            player.SetTowerTrackLevels(new[] { 0, 0, 0, 0, 0, 2, 0, 0, 0 });

            var stats = new UnitCombatStats(UnitRole.Melee, 200f, 0f, 10f, 10f, 1f, 1.5f, 4f, 8);
            var modified = FacelessTowerTrackUnitRules.Apply(stats, player);

            var expectedMultiplier = 1f + FacelessTowerTrackRules.FrenzyAttackSpeedPercentByLevel[1];
            Assert.AreEqual(1f * expectedMultiplier, modified.AttackSpeed, 0.001f);
        }

        [Test]
        public void SplashOfTheDeep_ProjectileTriggersSplash()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 0, 0, 0, 2, 0, 0 });

            var attacker = Spawn(combat, 0, UnitRole.Caster);
            var target = Spawn(combat, 1, UnitRole.Melee);
            var nearby = Spawn(combat, 1, UnitRole.Melee);
            nearby.WorldPosition = target.WorldPosition + new Vector3(0.5f, 0f, 0f);

            var hpBefore = nearby.CurrentHp;
            combat.ResolveProjectileImpact(UnitShot(attacker, target));
            var loss = hpBefore - nearby.CurrentHp;

            Assert.Greater(loss, 0f, "Splash of the Deep must damage nearby enemies");
        }

        [Test]
        public void HollowBones_IncreasesMoveSpeed()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Faceless, 0);
            player.SetTowerTrackLevels(new[] { 0, 0, 0, 0, 0, 0, 0, 2, 0 });

            var stats = new UnitCombatStats(UnitRole.Siege, 250f, 0f, 12f, 16f, 1f, 1.5f, 3.5f, 15);
            var modified = FacelessTowerTrackUnitRules.Apply(stats, player);

            var expectedMultiplier = 1f + FacelessTowerTrackRules.HollowBonesMoveSpeedPercentPerLevel * 2f;
            Assert.AreEqual(3.5f * expectedMultiplier, modified.MoveSpeed, 0.001f);
        }

        [Test]
        public void VoidHardening_BuildingAttackReduced()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 2 });

            var target = Spawn(combat, 0, UnitRole.Melee);
            var hpBefore = target.CurrentHp;
            combat.ResolveProjectileImpact(BuildingShot(1, GameIds.Buildings.TowerNw, target));
            var lossWithHardening = hpBefore - target.CurrentHp;

            controller.Players[0].SetTowerTrackLevels(new int[9]);
            var target2 = Spawn(combat, 0, UnitRole.Melee);
            var hpBefore2 = target2.CurrentHp;
            combat.ResolveProjectileImpact(BuildingShot(1, GameIds.Buildings.TowerNw, target2));
            var lossWithoutHardening = hpBefore2 - target2.CurrentHp;

            Assert.Greater(lossWithoutHardening, lossWithHardening, "Void Hardening must reduce building attack damage");
        }

        static MatchUnitState Spawn(MatchCombatSystem combat, int ownerSlot, UnitRole role)
        {
            var stats = role switch
            {
                UnitRole.Melee => new UnitCombatStats(role, 200f, 0f, 10f, 10f, 1f, 1.5f, 4f, 8),
                UnitRole.Ranged => new UnitCombatStats(role, 70f, 0f, 6f, 8f, 1f, 1.5f, 3.5f, 6),
                UnitRole.Caster => new UnitCombatStats(role, 80f, 0f, 4f, 5f, 1f, 1.5f, 3.5f, 10, 200f),
                UnitRole.Siege => new UnitCombatStats(role, 250f, 0f, 12f, 16f, 1f, 1.5f, 3.5f, 15),
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
                heroSlot: 0);
        }

        static MatchUnitState Spawn(MatchCombatSystem combat, int ownerSlot, UnitRole role, UnitCombatStats stats) =>
            combat.SpawnUnit(
                ownerSlot,
                GameIds.Lanes.Left,
                role,
                stats,
                distanceAlongLane: 20f,
                isHero: false,
                heroSlot: 0);

        static CombatProjectileState UnitShot(MatchUnitState attacker, MatchUnitState target) =>
            new(
                projectileId: NextId(),
                attacker.UnitId,
                target.UnitId,
                attacker.OwnerSlot,
                attacker.Role,
                GameIds.Races.Faceless,
                rawDamage: 10f,
                flightDuration: 0.01f,
                startPosition: attacker.WorldPosition,
                targetPosition: target.WorldPosition,
                isParabolic: false);

        static CombatProjectileState BuildingShot(
            int ownerSlot,
            string buildingId,
            MatchUnitState target) =>
            new(
                projectileId: NextId(),
                attackerUnitId: -1,
                target.UnitId,
                ownerSlot,
                UnitRole.Melee,
                GameIds.Races.Human,
                rawDamage: 10f,
                flightDuration: 0.01f,
                startPosition: target.WorldPosition + new Vector3(-4f, 3f, 0f),
                targetPosition: target.WorldPosition,
                isParabolic: false,
                sourceBuildingInstanceId: 900 + NextId(),
                sourceBuildingId: buildingId);

        static int _nextTestId = 1;

        static int NextId() => _nextTestId++;

        static (MatchController controller, MatchCombatSystem combat) CreateFacelessCombat(int seed = 12345)
        {
            var controller = new MatchController();
            controller.StartMatch(new MatchConfig(
                playerCount: 2,
                raceIds: new[] { GameIds.Races.Faceless, GameIds.Races.Faceless }));
            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, seed);
            return (controller, combat);
        }
    }
}
