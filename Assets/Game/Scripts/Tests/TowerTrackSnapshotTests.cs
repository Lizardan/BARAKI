using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TowerTrackSnapshotTests
    {
        [Test]
        public void RoundTrip_PreservesTowerTrackLevels()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 2,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0,
                        Gold = 900,
                        TowerTrackLevels = new[] { 1, 2, 3, 0, 1, 0, 2, 3, 1 },
                    },
                    new MatchPlayerSnapshot
                    {
                        Slot = 1,
                        Gold = 700,
                        TowerTrackLevels = new int[TowerTrackRules.TrackCount],
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(9, restored.Players[0].TowerTrackLevels.Length);
            CollectionAssert.AreEqual(
                original.Players[0].TowerTrackLevels,
                restored.Players[0].TowerTrackLevels);
            CollectionAssert.AreEqual(
                original.Players[1].TowerTrackLevels,
                restored.Players[1].TowerTrackLevels);
        }

        [Test]
        public void Capture_RoundTrip_CarriesCompletedTrackLevels()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            var player = controller.Players[0];
            player.SetTowerTrackLevels(new[] { 1, 0, 0, 0, 2, 0, 0, 0, 3 });

            var captured = MatchSnapshotCodec.Capture(controller);
            var bytes = MatchSnapshotCodec.Serialize(captured);
            var restored = MatchSnapshotCodec.Deserialize(bytes);

            Assert.AreEqual(1, FindSlot(restored, 0).TowerTrackLevels[0]);
            Assert.AreEqual(2, FindSlot(restored, 0).TowerTrackLevels[4]);
            Assert.AreEqual(3, FindSlot(restored, 0).TowerTrackLevels[8]);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_RestoresTrackLevelsOnClientController()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.Players[0].SetTowerTrackLevels(new[] { 0, 2, 0, 0, 0, 1, 0, 0, 0 });

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            var wire = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(host));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Deserialize(wire));

            Assert.AreEqual(2, client.Players[0].GetTowerTrackLevel(1));
            Assert.AreEqual(1, client.Players[0].GetTowerTrackLevel(5));
        }

        static MatchPlayerSnapshot FindSlot(MatchSnapshot snapshot, int slot)
        {
            foreach (var player in snapshot.Players)
            {
                if (player.Slot == slot)
                {
                    return player;
                }
            }

            Assert.Fail($"Player slot {slot} missing from snapshot.");
            return default;
        }
    }
}
