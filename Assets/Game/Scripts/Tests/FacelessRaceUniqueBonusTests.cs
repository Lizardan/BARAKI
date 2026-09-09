using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Faceless race uniques — slots 11–12 (FACELESS-013).</summary>
    public sealed class FacelessRaceUniqueBonusTests
    {
        [Test]
        public void Faceless_PicksRaceUniqueSlots11And12_Succeed()
        {
            var controller = CreateEarlyFacelessMatch();

            Assert.IsTrue(controller.TrySetBonusPick(0, BonusKitRules.RaceUnique1Slot));
            Assert.IsTrue(controller.TrySetBonusPick(1, BonusKitRules.RaceUnique2Slot));
        }

        [Test]
        public void ShadowOfTheVoid_SpawnAfterPick_CarriesEvadeFlag()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique1Slot;

            var owner = Spawn(combat, 0, UnitRole.Melee);
            var enemy = Spawn(combat, 1, UnitRole.Melee);

            Assert.IsTrue(owner.ShadowEvadeActive, "owner unit spawned after pick 11 must be flagged");
            Assert.IsFalse(enemy.ShadowEvadeActive, "unit without the pick must not be flagged");
        }

        [Test]
        public void ShadowOfTheVoid_HumanPickSlot11_NotFlagged()
        {
            var (controller, combat) = CreateCombat(new[] { GameIds.Races.Human, GameIds.Races.Faceless });
            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique1Slot;
            controller.Players[1].BonusPickSlot = BonusKitRules.RaceUnique1Slot;

            Assert.IsFalse(Spawn(combat, 0, UnitRole.Melee).ShadowEvadeActive,
                "a Human picking slot 11 must not inherit the Faceless unique");
            Assert.IsTrue(Spawn(combat, 1, UnitRole.Melee).ShadowEvadeActive);
        }

        [Test]
        public void ShadowOfTheVoid_ReplacementPolicy_PrePickUnitsNotFlagged()
        {
            var (controller, combat) = CreateFacelessCombat();

            var beforePick = Spawn(combat, 0, UnitRole.Melee);
            Assert.IsFalse(beforePick.ShadowEvadeActive, "unit spawned before the pick must not be flagged");

            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique1Slot;
            var afterPick = Spawn(combat, 0, UnitRole.Melee);
            Assert.IsTrue(afterPick.ShadowEvadeActive);

            Assert.IsFalse(beforePick.ShadowEvadeActive, "the flag must be fixed at spawn, not retro-applied");
        }

        [Test]
        public void ShadowOfTheVoid_FlagsHeroesAndTitan()
        {
            var (controller, combat) = CreateFacelessCombat();
            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique1Slot;

            var hero = Spawn(combat, 0, UnitRole.Hero);
            var titan = Spawn(combat, 0, UnitRole.Titan);

            Assert.IsTrue(hero.ShadowEvadeActive);
            Assert.IsTrue(titan.ShadowEvadeActive);
        }

        [Test]
        public void ShadowOfTheVoid_EvadeRoll_BlocksAllDamage()
        {
            var seed = FindSeedThatProcs(FacelessBonusUnitRules.ShadowEvadeChance);
            var (controller, combat) = CreateFacelessCombat(seed);
            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique1Slot;

            var attacker = Spawn(combat, 1, UnitRole.Melee);
            var target = Spawn(combat, 0, UnitRole.Melee);
            target.CurrentHp = target.Stats.MaxHp;

            var dealt = combat.ApplyDamage(attacker, target, 1000f, attacker.OwnerSlot);

            Assert.AreEqual(0f, dealt, "Shadow of the Void must fully avoid the attack");
            Assert.AreEqual(target.Stats.MaxHp, target.CurrentHp, "flagged target HP is unchanged");
        }

        [Test]
        public void ShadowOfTheVoid_NoFlag_EvenOnProcSeed_TakesDamage()
        {
            var seed = FindSeedThatProcs(FacelessBonusUnitRules.ShadowEvadeChance);
            var (controller, combat) = CreateFacelessCombat(seed);
            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique2Slot; // Void Bastion, not Shadow

            var attacker = Spawn(combat, 1, UnitRole.Melee);
            var target = Spawn(combat, 0, UnitRole.Melee);

            var dealt = combat.ApplyDamage(attacker, target, 100f, attacker.OwnerSlot);

            Assert.Greater(dealt, 0f, "a unit without the Shadow flag must always take damage");
        }

        [Test]
        public void VoidBastion_MeleeStrikeAgainstOwnerBuilding_Misses()
        {
            var seed = FindSeedThatProcs(FacelessBonusUnitRules.VoidBastionMissChance);
            var (controller, combat) = CreateFacelessCombat(seed);
            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique2Slot;

            var registry = new BuildingRegistry();
            registry.Initialize(MatchArenaGenerator.Generate(2));
            combat.SetBuildings(registry);
            var building = FindBuilding(registry, 0, GameIds.Buildings.TowerSw);
            building.SetAuthoritativeHp(500f);

            var siege = Spawn(combat, 1, UnitRole.Siege);
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                siege.UnitId,
                targetUnitId: -1,
                rawDamage: 50f,
                duration: 0.01f,
                targetBuildingInstanceId: building.InstanceId));

            Assert.AreEqual(500f, building.CurrentHp, "a missed strike must deal no building damage");

            // Control: the same owner without the pick takes damage on the same strike.
            building.SetAuthoritativeHp(500f);
            controller.Players[0].BonusPickSlot = BonusPickRules.NoneSlot;
            combat.ResolveMeleeImpact(new CombatMeleeStrikeState(
                siege.UnitId,
                targetUnitId: -1,
                rawDamage: 50f,
                duration: 0.01f,
                targetBuildingInstanceId: building.InstanceId));

            Assert.Less(building.CurrentHp, 500f, "without Void Bastion the strike must damage the building");
        }

        [Test]
        public void VoidBastion_ProjectileAgainstOwnerBuilding_Misses()
        {
            var seed = FindSeedThatProcs(FacelessBonusUnitRules.VoidBastionMissChance);
            var (controller, combat) = CreateFacelessCombat(seed);
            controller.Players[0].BonusPickSlot = BonusKitRules.RaceUnique2Slot;

            var registry = new BuildingRegistry();
            registry.Initialize(MatchArenaGenerator.Generate(2));
            combat.SetBuildings(registry);
            var building = FindBuilding(registry, 0, GameIds.Buildings.TowerSw);
            building.SetAuthoritativeHp(500f);

            var attacker = Spawn(combat, 1, UnitRole.Ranged);
            combat.ResolveProjectileImpact(new CombatProjectileState(
                projectileId: NextId(),
                attacker.UnitId,
                targetUnitId: 0,
                attacker.OwnerSlot,
                UnitRole.Ranged,
                GameIds.Races.Faceless,
                rawDamage: 50f,
                flightDuration: 0.01f,
                startPosition: attacker.WorldPosition,
                targetPosition: building.WorldPosition,
                isParabolic: false,
                targetBuildingInstanceId: building.InstanceId));

            Assert.AreEqual(500f, building.CurrentHp, "a missed projectile must deal no building damage");

            building.SetAuthoritativeHp(500f);
            controller.Players[0].BonusPickSlot = BonusPickRules.NoneSlot;
            combat.ResolveProjectileImpact(new CombatProjectileState(
                projectileId: NextId(),
                attacker.UnitId,
                targetUnitId: 0,
                attacker.OwnerSlot,
                UnitRole.Ranged,
                GameIds.Races.Faceless,
                rawDamage: 50f,
                flightDuration: 0.01f,
                startPosition: attacker.WorldPosition,
                targetPosition: building.WorldPosition,
                isParabolic: false,
                targetBuildingInstanceId: building.InstanceId));

            Assert.Less(building.CurrentHp, 500f, "without Void Bastion the projectile must damage the building");
        }

        [Test]
        public void MarchDiscipline_RaceGatedToHuman_Slot11Only()
        {
            var faceless = new MatchPlayerState(0, GameIds.Races.Faceless, 1000)
            {
                BonusPickSlot = BonusKitRules.RaceUnique1Slot,
            };
            var human = new MatchPlayerState(1, GameIds.Races.Human, 1000)
            {
                BonusPickSlot = BonusKitRules.RaceUnique1Slot,
            };

            Assert.AreEqual(
                4f,
                HumanBonusUnitRules.ApplyMarchDiscipline(faceless, 4f),
                0.001f,
                "March Discipline must never apply to Faceless troops");
            Assert.AreEqual(
                4f * HumanBonusUnitRules.MarchDisciplineMultiplier,
                HumanBonusUnitRules.ApplyMarchDiscipline(human, 4f),
                0.001f);
        }

        [Test]
        public void StoneMasonry_RaceGatedToHuman_BuildingHpUnchanged()
        {
            var controller = CreateEarlyFacelessMatch();
            BuildingState main = null;
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == 0 && building.BuildingId == GameIds.Buildings.Main)
                {
                    main = building;
                }
            }

            Assert.IsNotNull(main);
            var baseMaxHp = main.MaxHp;

            Assert.IsTrue(controller.TrySetBonusPick(0, BonusKitRules.RaceUnique2Slot));
            Assert.AreEqual(
                baseMaxHp,
                main.MaxHp,
                0.01f,
                "Stone Masonry must never scale Faceless buildings");
        }

        static int FindSeedThatProcs(float chance)
        {
            for (var seed = 0; seed < 10_000; seed++)
            {
                if (BonusKitRules.RollProc(new System.Random(seed), chance))
                {
                    return seed;
                }
            }

            Assert.Fail("No seed produced a proc");
            return -1;
        }

        static MatchUnitState Spawn(MatchCombatSystem combat, int ownerSlot, UnitRole role)
        {
            var stats = new UnitCombatStats(
                role,
                120f, 0f, 8f, 10f, 1f, 1.5f, 4f, 8);
            return combat.SpawnUnit(
                ownerSlot,
                GameIds.Lanes.Left,
                role,
                stats,
                distanceAlongLane: 20f,
                isHero: role == UnitRole.Hero,
                bonusSlot: 0);
        }

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

        static MatchController CreateEarlyFacelessMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(new MatchConfig(
                playerCount: 2,
                raceIds: new[] { GameIds.Races.Faceless, GameIds.Races.Faceless }));
            controller.BeginEarlyPhase();
            return controller;
        }

        static (MatchController controller, MatchCombatSystem combat) CreateFacelessCombat(int seed = 12345) =>
            CreateCombat(new[] { GameIds.Races.Faceless, GameIds.Races.Faceless }, seed);

        static (MatchController controller, MatchCombatSystem combat) CreateCombat(
            string[] raceIds,
            int seed = 12345)
        {
            var controller = new MatchController();
            controller.StartMatch(new MatchConfig(playerCount: 2, raceIds: raceIds));
            var combat = new MatchCombatSystem();
            combat.Reset(controller.Players, controller.Graph, seed);
            return (controller, combat);
        }

        static int _nextTestId = 1;

        static int NextId() => _nextTestId++;
    }
}