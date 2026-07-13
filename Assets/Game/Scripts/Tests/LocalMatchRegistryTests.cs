using Game.Core;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LocalMatchRegistryTests
    {
        [SetUp]
        public void SetUp() => LocalMatchRegistry.Clear();

        [TearDown]
        public void TearDown() => LocalMatchRegistry.Clear();

        [Test]
        public void Create_ThenJoin_AssignsSecondSlot()
        {
            var created = LocalMatchRegistry.Create(2, "Host");
            Assert.AreEqual(0, LocalMatchRegistry.LocalPlayerSlot);

            var joined = LocalMatchRegistry.Join(created.RoomCode, "Guest");
            Assert.AreSame(created, joined);
            Assert.AreEqual(1, LocalMatchRegistry.LocalPlayerSlot);
            Assert.IsTrue(joined.GetSlot(1).IsOccupied);
        }

        [Test]
        public void Join_UnknownCode_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                LocalMatchRegistry.Join("ZZZZ", "Guest"));
        }

        [Test]
        public void LocalDevBackend_RejectsUnselectableMode()
        {
            var backend = new LocalDevSessionBackend();
            Assert.Throws<System.InvalidOperationException>(() =>
                backend.CreateAsync(new CreateMatchRequest(3, "Host")).GetAwaiter().GetResult());
        }
    }
}
