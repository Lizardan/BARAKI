using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BuildingRegistryTests
    {
        [Test]
        public void Initialize_N4_CreatesEightBuildingsPerPlayer()
        {
            var layout = MatchArenaGenerator.Generate(4);
            var registry = new BuildingRegistry();
            registry.Initialize(layout);

            Assert.AreEqual(32, registry.Buildings.Count);

            for (var slot = 0; slot < 4; slot++)
            {
                Assert.AreEqual(8, registry.CountIntactBuildings(slot));
            }
        }

        [Test]
        public void TryApplyDamage_MarksBuildingRuinsAndRaisesEvent()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var registry = new BuildingRegistry();
            registry.Initialize(layout);

            BuildingDestroyedEvent? destroyedEvent = null;
            registry.BuildingDestroyed += e => destroyedEvent = e;

            var building = registry.Buildings[0];
            Assert.IsTrue(registry.TryApplyDamage(building.InstanceId, 99999f, attackerOwnerSlot: 1));

            Assert.IsTrue(building.IsRuins);
            Assert.IsNotNull(destroyedEvent);
            Assert.AreEqual(building.BuildingId, destroyedEvent.Value.BuildingId);
        }

        [Test]
        public void AreAllBuildingsRuined_ReturnsTrueWhenEveryBuildingDestroyed()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var registry = new BuildingRegistry();
            registry.Initialize(layout);

            foreach (var building in registry.Buildings)
            {
                if (building.OwnerSlot == 1)
                {
                    registry.TryApplyDamage(building.InstanceId, 99999f, 0);
                }
            }

            Assert.IsTrue(registry.AreAllBuildingsRuined(1));
            Assert.IsFalse(registry.AreAllBuildingsRuined(0));
        }
    }
}
