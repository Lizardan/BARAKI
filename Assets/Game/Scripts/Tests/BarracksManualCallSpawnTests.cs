using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BarracksManualCallSpawnTests
    {
        [Test]
        public void TryManualCallUnit_SpawnsAtBarracksLaneClearance_NotAtBuildingCenter()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 200;

            var barracksBuilding = FindBarracks(controller, 0, GameIds.Buildings.BarracksCenter);
            Assert.IsNotNull(barracksBuilding);

            Assert.IsTrue(controller.TryManualCallUnit(0, barracksBuilding.InstanceId, UnitRole.Melee));
            var unit = controller.Combat.Units[controller.Combat.Units.Count - 1];

            var toBarracks = unit.WorldPosition - barracksBuilding.WorldPosition;
            toBarracks.y = 0f;
            // Outside barracks mesh (half footprint), same band as auto-wave front spawn.
            Assert.Greater(
                toBarracks.magnitude,
                CombatFormationRules.BarracksFootprintExtent * 0.5f);
            Assert.AreEqual(
                CombatFormationRules.BarracksSpawnForwardClearance,
                unit.MarchSpawnDistance,
                0.01f);
        }

        static BuildingState FindBarracks(MatchController controller, int ownerSlot, string buildingId)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.BuildingId == buildingId)
                {
                    return building;
                }
            }

            return null;
        }
    }
}
