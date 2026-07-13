using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchSnapshotCodecTests
    {
        [Test]
        public void RoundTrip_PreservesPlayersAndUnits()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 2,
                MatchTimeSeconds = 12.5f,
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, Gold = 500, IsEliminated = false },
                    new MatchPlayerSnapshot { Slot = 1, Gold = 480, IsEliminated = false },
                },
                Buildings = new[]
                {
                    new MatchBuildingSnapshot
                    {
                        OwnerSlot = 0,
                        BuildingId = "BUILDING_MAIN",
                        Health = 1000f,
                        IsRuins = false,
                    },
                },
                Units = new[]
                {
                    new MatchUnitSnapshot
                    {
                        UnitId = 7,
                        OwnerSlot = 1,
                        UnitDefId = "Melee",
                        PosX = 3.5f,
                        PosZ = -2f,
                        Health = 40f,
                        IsAlive = true,
                    },
                },
            };

            var bytes = MatchSnapshotCodec.Serialize(original);
            var restored = MatchSnapshotCodec.Deserialize(bytes);

            Assert.AreEqual(original.PlayerCount, restored.PlayerCount);
            Assert.AreEqual(original.Phase, restored.Phase);
            Assert.AreEqual(original.MatchTimeSeconds, restored.MatchTimeSeconds);
            Assert.AreEqual(2, restored.Players.Length);
            Assert.AreEqual(500, restored.Players[0].Gold);
            Assert.AreEqual("BUILDING_MAIN", restored.Buildings[0].BuildingId);
            Assert.AreEqual(7, restored.Units[0].UnitId);
            Assert.AreEqual(3.5f, restored.Units[0].PosX);
        }

        [Test]
        public void ShouldTickSimulation_OfflineAndServerOnly()
        {
            Assert.IsTrue(MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Offline));
            Assert.IsTrue(MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Server));
            Assert.IsFalse(MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Client));
        }
    }
}
