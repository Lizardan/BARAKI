using Game.Core;
using Game.Editor;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    public sealed class BarracksManualCallControllerTests
    {
        [Test]
        public void TryManualCallUnit_SpendsGoldAndCharge_SpawnsOnLane()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 200;

            var barracksBuilding = FindBarracks(controller, 0);
            Assert.IsNotNull(barracksBuilding);

            var beforeUnits = controller.Combat.Units.Count;
            Assert.IsTrue(controller.TryManualCallUnit(0, barracksBuilding.InstanceId, UnitRole.Melee));
            Assert.AreEqual(150, controller.Players[0].Gold);
            Assert.AreEqual(beforeUnits + 1, controller.Combat.Units.Count);

            var barracks = controller.WaveScheduler.GetBarracks(0, barracksBuilding.BuildingId);
            Assert.AreEqual(1, barracks.CallCharges.GetCharges(UnitRole.Melee));
        }

        [Test]
        public void TryManualCallUnit_WithMatchingBonusPick_SpawnsBonusUnit()
        {
            var controller = CreateControllerWithCatalogs();
            controller.Players[0].BonusPickSlot = BonusPickRules.NoneSlot;
            controller.Players[0].BonusPickSlot2 = BonusKitRules.BonusSlotForRole(UnitRole.Melee);
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 200;

            var barracksBuilding = FindBarracks(controller, 0);
            Assert.IsTrue(controller.TryManualCallUnit(0, barracksBuilding.InstanceId, UnitRole.Melee));

            var spawned = FindOwnedMelee(controller, 0);
            Assert.IsNotNull(spawned);
            Assert.AreEqual(1, spawned.BonusSlot);
            Assert.AreEqual(2f, spawned.Stats.AttackRange, 0.01f, "Melee BONUS range should be 2.");
            Assert.Less(spawned.Stats.MaxHp, 120f, "Melee BONUS HP should be below base 120.");
        }

        [Test]
        public void TryManualCallUnit_WithOtherRoleBonus_SpawnsBaseUnit()
        {
            var controller = CreateControllerWithCatalogs();
            Assert.IsNotNull(controller.CombatCatalog);
            Assert.IsNotNull(controller.UnitVisualCatalog);
            controller.Players[0].BonusPickSlot = BonusPickRules.NoneSlot;
            controller.Players[0].BonusPickSlot2 = BonusKitRules.BonusSlotForRole(UnitRole.Caster);
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 200;

            var barracksBuilding = FindBarracks(controller, 0);
            Assert.IsTrue(controller.TryManualCallUnit(0, barracksBuilding.InstanceId, UnitRole.Melee));

            MatchUnitState spawned = FindOwnedMelee(controller, 0);
            Assert.IsNotNull(spawned);
            Assert.AreEqual(0, spawned.BonusSlot);
            Assert.AreEqual(1.5f, spawned.Stats.AttackRange, 0.01f);
        }

        [Test]
        public void TryManualCallUnit_FacelessBonusPick_FlowsSlotResolvesBonusPrefab()
        {
            // FACELESS-014 lifted the gate: a Faceless bonus pick flows its slot and resolves the
            // race's own BONUS prefab. The Faceless BONUS prefab is a placeholder clone of the
            // base model (stats mirror the plain melee) carrying the blue-flame marker — never a
            // red capsule or a Human-statted unit.
            Assume.That(BonusKitRules.HasBonusKit(GameIds.Races.Faceless),
                "Faceless bonus kit gate (FACELESS-014) must be lifted.");

            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(RaceContentBuilder.CatalogPath);
            Assume.That(raceCatalog != null, "RaceCatalog missing.");
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(
                UnitVisualPrefabBuilder.CatalogPath);
            Assume.That(visualCatalog != null, "UnitVisualCatalog missing.");

            var controller = new MatchController();
            controller.CombatCatalog = new RaceCatalogCombatCatalog(raceCatalog);
            controller.UnitVisualCatalog = visualCatalog;
            controller.StartMatch(new MatchConfig(2, new[] { GameIds.Races.Faceless, GameIds.Races.Faceless }));
            controller.Players[0].BonusPickSlot = BonusPickRules.NoneSlot;
            controller.Players[0].BonusPickSlot2 = BonusKitRules.BonusSlotForRole(UnitRole.Melee);
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 200;

            var barracksBuilding = FindBarracks(controller, 0);
            Assert.IsTrue(controller.TryManualCallUnit(0, barracksBuilding.InstanceId, UnitRole.Melee));

            var spawned = FindOwnedMelee(controller, 0);
            Assert.IsNotNull(spawned);
            // Slot flows through so the presenter can attach the bonus variant marker.
            Assert.AreEqual(1, spawned.BonusSlot);
            // The Faceless BONUS prefab is a placeholder clone of the base model: stats stay at
            // the plain base melee values, but they now come from the race's own prefab.
            Assert.AreEqual(1.5f, spawned.Stats.AttackRange, 0.01f, "Faceless BONUS melee keeps the base range.");
            Assert.AreEqual(120f, spawned.Stats.MaxHp, 0.01f, "Faceless BONUS melee keeps the base HP.");
        }

        static MatchUnitState FindOwnedMelee(MatchController controller, int ownerSlot)
        {
            MatchUnitState spawned = null;
            for (var i = 0; i < controller.Combat.Units.Count; i++)
            {
                var unit = controller.Combat.Units[i];
                if (unit.OwnerSlot == ownerSlot && unit.Role == UnitRole.Melee)
                {
                    spawned = unit;
                }
            }

            return spawned;
        }

        static MatchController CreateControllerWithCatalogs()
        {
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(RaceContentBuilder.CatalogPath);
            Assume.That(raceCatalog != null, "RaceCatalog missing.");
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(
                UnitVisualPrefabBuilder.CatalogPath);
            Assume.That(visualCatalog != null, "UnitVisualCatalog missing.");

            var controller = new MatchController();
            controller.CombatCatalog = new RaceCatalogCombatCatalog(raceCatalog);
            controller.UnitVisualCatalog = visualCatalog;
            controller.StartMatch(MatchConfig.MvpDefault(2));
            return controller;
        }

        static BuildingState FindBarracks(MatchController controller, int ownerSlot)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && BuildingRules.IsBarracks(building.BuildingId))
                {
                    return building;
                }
            }

            return null;
        }
    }
}
