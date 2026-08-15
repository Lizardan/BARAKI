using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;

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
            var player = controller.Players[0];
            player.MainLevel = 2;
            player.MagicLevel = 1;
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
    }
}
