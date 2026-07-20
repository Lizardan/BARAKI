using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

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
        public void TryManualCallUnit_NotEnoughGold_Fails()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 10;

            var barracksBuilding = FindBarracks(controller, 0);
            Assert.IsFalse(controller.TryManualCallUnit(0, barracksBuilding.InstanceId, UnitRole.Melee));
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
