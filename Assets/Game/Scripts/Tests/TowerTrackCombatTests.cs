using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TowerTrackCombatTests
    {
        [Test]
        public void FlamingArrows_RangedShotIgnites_NeutralShotDoesNot()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 });

            var attacker = Spawn(combat, 0, UnitRole.Ranged);
            var target = Spawn(combat, 1, UnitRole.Melee);

            combat.ResolveProjectileImpact(UnitShot(attacker, target));
            Assert.Greater(target.BurnSecondsRemaining, 0f);
            Assert.AreEqual(1 * TowerTrackRules.BurnDamagePerSecondPerLevel, target.BurnDamagePerSecond);

            // Without the track: no burn (attacker from a slot without the research).
            var neutralAttacker = Spawn(combat, 1, UnitRole.Ranged);
            var otherTarget = Spawn(combat, 0, UnitRole.Melee);
            otherTarget.WorldPosition = neutralAttacker.WorldPosition + new Vector3(6f, 0f, 0f);
            combat.ResolveProjectileImpact(UnitShot(neutralAttacker, otherTarget));
            Assert.AreEqual(0f, otherTarget.BurnSecondsRemaining);
        }

        [Test]
        public void FlamingArrows_TowerShotIgnites_MeleeRoleDoesNot()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 2, 0, 0, 0, 0, 0, 0, 0, 0 });

            var target = Spawn(combat, 1, UnitRole.Melee);
            combat.ResolveProjectileImpact(BuildingShot(0, GameIds.Buildings.TowerNw, target));
            Assert.Greater(target.BurnSecondsRemaining, 0f);
            Assert.AreEqual(2 * TowerTrackRules.BurnDamagePerSecondPerLevel, target.BurnDamagePerSecond);

            // Barracks shots never burn even with the track.
            var otherTarget = Spawn(combat, 1, UnitRole.Melee);
            combat.ResolveProjectileImpact(
                BuildingShot(0, GameIds.Buildings.BarracksCenter, otherTarget));
            Assert.AreEqual(0f, otherTarget.BurnSecondsRemaining);
        }

        [Test]
        public void FlamingArrows_BurnTicksDamage_AndExpires()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 3, 0, 0, 0, 0, 0, 0, 0, 0 });

            var attacker = Spawn(combat, 0, UnitRole.Flying);
            var target = Spawn(combat, 1, UnitRole.Melee);
            target.WorldPosition = attacker.WorldPosition + new Vector3(50f, 0f, 0f);
            combat.ResolveProjectileImpact(UnitShot(attacker, target));
            Assert.Greater(target.BurnSecondsRemaining, 0f);

            var hpBefore = target.CurrentHp;
            combat.Tick(TowerTrackRules.BurnDurationSeconds + 0.5f);

            Assert.Less(target.CurrentHp, hpBefore);
            Assert.GreaterOrEqual(hpBefore - target.CurrentHp, 12f);
            Assert.AreEqual(0f, target.BurnSecondsRemaining);
        }

        [Test]
        public void Bloodrage_KillGrantsAttackSpeed_ThenExpires()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 2, 0, 0, 0, 0, 0, 0 });

            var attacker = Spawn(combat, 0, UnitRole.Melee);
            var victim = Spawn(combat, 1, UnitRole.Melee);
            var baseInterval = combat.GetAttackIntervalSeconds(attacker);

            combat.ApplyDamage(attacker, victim, 10_000f, attacker.OwnerSlot);
            var enragedInterval = combat.GetAttackIntervalSeconds(attacker);
            Assert.Less(enragedInterval, baseInterval);
            Assert.AreEqual(
                baseInterval / (1f + TowerTrackRules.BloodrageAttackSpeedPercentByLevel[1]),
                enragedInterval,
                0.001f);

            combat.Tick(TowerTrackRules.BloodrageDurationSeconds + 0.1f);
            Assert.AreEqual(baseInterval, combat.GetAttackIntervalSeconds(attacker), 0.001f);
        }

        [Test]
        public void LastStand_LowHealthBoostsDamage_HighHealthDoesNot()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 1 });

            var attacker = Spawn(combat, 0, UnitRole.Super);
            var healthy = Spawn(combat, 1, UnitRole.Melee);
            const float raw = 10f;

            var healthyBefore = healthy.CurrentHp;
            combat.ApplyDamage(attacker, healthy, raw, attacker.OwnerSlot);
            var healthyLoss = healthyBefore - healthy.CurrentHp;

            attacker.CurrentHp = attacker.Stats.MaxHp * 0.1f;
            var wounded = Spawn(combat, 1, UnitRole.Melee);
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                attacker.UnitId,
                wounded.UnitId,
                rawDamage: raw,
                duration: 0.01f));
            var woundedLoss = wounded.Stats.MaxHp - wounded.CurrentHp;

            Assert.Greater(woundedLoss, healthyLoss,
                "attacker below the HP threshold should deal more damage");
        }

        [Test]
        public void Bulwark_MaxLevelBlocksMeleeDamage_ArmorScalesPerLevel()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[1].SetTowerTrackLevels(new[] { 0, 3, 0, 0, 0, 0, 0, 0, 0 });

            var attacker = Spawn(combat, 0, UnitRole.Melee);
            var defender = Spawn(combat, 1, UnitRole.Melee);
            const float raw = 30f;

            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                attacker.UnitId,
                defender.UnitId,
                rawDamage: raw,
                duration: 0.01f));
            var blockedLoss = defender.Stats.MaxHp - defender.CurrentHp;

            // Same raw hit without any track on the defender.
            controller.Players[1].SetTowerTrackLevels(new int[9]);
            var plainDefender = Spawn(combat, 1, UnitRole.Melee);
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                attacker.UnitId,
                plainDefender.UnitId,
                rawDamage: raw,
                duration: 0.01f));
            var plainLoss = plainDefender.Stats.MaxHp - plainDefender.CurrentHp;

            Assert.Less(blockedLoss, plainLoss, "Bulwark L3 must reduce melee damage taken");
        }

        [Test]
        public void ArcaneFocus_ScalesCasterCooldowns_OtherRolesUnaffected()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 0, 2, 0, 0, 0, 0 });

            var caster = Spawn(combat, 0, UnitRole.Caster);
            caster.AbilityCooldownRemaining = new[] { 10f };
            combat.ArmSlotCooldown(caster, 0, 10f);
            Assert.AreEqual(10f * TowerTrackRules.CasterCooldownFactorByLevel[1], caster.AbilityCooldownRemaining[0], 0.001f);

            var melee = Spawn(combat, 0, UnitRole.Melee);
            melee.AbilityCooldownRemaining = new[] { 10f };
            combat.ArmSlotCooldown(melee, 0, 10f);
            Assert.AreEqual(10f, melee.AbilityCooldownRemaining[0], 0.001f);
        }

        [Test]
        public void FieldMedics_RegeneratesRegularUnits_NotChampions()
        {
            var (controller, combat) = CreateCombat();
            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 0, 0, 0, 0, 2, 0 });

            var unit = Spawn(combat, 0, UnitRole.Melee);
            unit.CurrentHp -= 20f;
            var hero = Spawn(combat, 0, UnitRole.Hero, heroSlot: 1, isHero: true);
            hero.CurrentHp -= 20f;

            var unitBefore = unit.CurrentHp;
            var heroBefore = hero.CurrentHp;
            combat.Tick(1f);

            Assert.AreEqual(unitBefore + 2f, unit.CurrentHp, 0.05f);
            Assert.AreEqual(heroBefore, hero.CurrentHp);
        }

        [Test]
        public void BatteringRams_BuildingHitDealsMoreDamage()
        {
            var (controller, combat) = CreateCombat();
            var registry = new BuildingRegistry();
            registry.Initialize(MatchArenaGenerator.Generate(2));
            combat.SetBuildings(registry);

            var enemyBuilding = FindBuilding(registry, 1, GameIds.Buildings.TowerSw);
            var siege = Spawn(combat, 0, UnitRole.Siege);

            enemyBuilding.SetAuthoritativeHp(500f);
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                siege.UnitId,
                targetUnitId: -1,
                rawDamage: 50f,
                duration: 0.01f,
                targetBuildingInstanceId: enemyBuilding.InstanceId));
            var plainLoss = 500f - enemyBuilding.CurrentHp;
            Assert.Greater(plainLoss, 0f);

            controller.Players[0].SetTowerTrackLevels(new[] { 0, 0, 0, 1, 0, 0, 0, 0, 0 });
            enemyBuilding.SetAuthoritativeHp(500f);
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                siege.UnitId,
                targetUnitId: -1,
                rawDamage: 50f,
                duration: 0.01f,
                targetBuildingInstanceId: enemyBuilding.InstanceId));
            var boostedLoss = 500f - enemyBuilding.CurrentHp;

            Assert.Greater(boostedLoss, plainLoss, "Battering Rams must scale building damage");
            Assert.AreEqual(
                plainLoss * (1f + TowerTrackRules.VsBuildingDamagePercentPerLevel),
                boostedLoss,
                plainLoss * 0.05f);
        }

        static MatchUnitState Spawn(
            MatchCombatSystem combat,
            int ownerSlot,
            UnitRole role,
            int heroSlot = 0,
            bool isHero = false)
        {
            var stats = role switch
            {
                UnitRole.Melee => new UnitCombatStats(role, 200f, 0f, 10f, 10f, 1f, 1.5f, 4f, 8),
                UnitRole.Ranged => new UnitCombatStats(role, 70f, 0f, 6f, 8f, 1f, 8f, 3.5f, 6),
                UnitRole.Caster => new UnitCombatStats(role, 80f, 0f, 4f, 5f, 1f, 6f, 3.5f, 10, 200f),
                UnitRole.Siege => new UnitCombatStats(role, 250f, 0f, 12f, 16f, 1f, 1.5f, 3.5f, 15),
                UnitRole.Flying => new UnitCombatStats(role, 100f, 0f, 8f, 10f, 1f, 6f, 3.5f, 10),
                UnitRole.Super => new UnitCombatStats(role, 500f, 2f, 30f, 40f, 0.5f, 10f, 3.5f, 50),
                UnitRole.Hero => new UnitCombatStats(role, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80),
                _ => new UnitCombatStats(role, 300f, 0f, 10f, 12f, 1f, 2f, 3.5f, 20),
            };

            return combat.SpawnUnit(
                ownerSlot,
                GameIds.Lanes.Left,
                role,
                stats,
                distanceAlongLane: 20f,
                isHero: isHero,
                heroSlot: heroSlot);
        }

        static CombatProjectileState UnitShot(MatchUnitState attacker, MatchUnitState target) =>
            new(
                projectileId: NextId(),
                attacker.UnitId,
                target.UnitId,
                attacker.OwnerSlot,
                attacker.Role,
                GameIds.Races.Human,
                rawDamage: 5f,
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
                rawDamage: 5f,
                flightDuration: 0.01f,
                startPosition: target.WorldPosition + new Vector3(-4f, 3f, 0f),
                targetPosition: target.WorldPosition,
                isParabolic: false,
                sourceBuildingInstanceId: 900 + NextId(),
                sourceBuildingId: buildingId);

        static int _nextTestId = 1;

        static int NextId() => _nextTestId++;

        static BuildingState FindBuilding(BuildingRegistry registry, int ownerSlot, string buildingId)
        {
            foreach (var building in registry.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.BuildingId == buildingId)
                {
                    return building;
                }
            }

            Assert.Fail($"Building {buildingId} for slot {ownerSlot} not found.");
            return null;
        }

        static (MatchController controller, MatchCombatSystem combat) CreateCombat(int seed = 12345)
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, seed);
            return (controller, combat);
        }
    }
}
