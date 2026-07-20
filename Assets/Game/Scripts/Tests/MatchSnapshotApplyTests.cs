using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchSnapshotApplyTests
    {
        [Test]
        public void Capture_IncludesWinnerSlotWhenMatchEnded()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.EndMatch(1);

            var snapshot = MatchSnapshotCodec.Capture(controller);
            Assert.AreEqual(1, snapshot.WinnerSlot);
            Assert.AreEqual((int)MatchPhase.End, snapshot.Phase);
        }

        [Test]
        public void RoundTrip_PreservesWinnerSlot()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = (int)MatchPhase.End,
                MatchTimeSeconds = 90f,
                WinnerSlot = 0,
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, Gold = 100, IsEliminated = false },
                    new MatchPlayerSnapshot { Slot = 1, Gold = 0, IsEliminated = true },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));
            Assert.AreEqual(0, restored.WinnerSlot);
            Assert.AreEqual(100, restored.Players[0].Gold);
            Assert.IsTrue(restored.Players[1].IsEliminated);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesGoldAndEndsMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 500;

            var ended = false;
            controller.MatchEnded += _ => ended = true;

            var snapshot = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = (int)MatchPhase.End,
                MatchTimeSeconds = 42f,
                WinnerSlot = 1,
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, Gold = 777, IsEliminated = true },
                    new MatchPlayerSnapshot { Slot = 1, Gold = 200, IsEliminated = false },
                },
            };

            controller.ApplyAuthoritativeSnapshot(snapshot);

            Assert.AreEqual(777, controller.Players[0].Gold);
            Assert.IsTrue(controller.Players[0].IsEliminated);
            Assert.AreEqual(MatchPhase.End, controller.Phase);
            Assert.AreEqual(1, controller.WinnerSlot);
            Assert.AreEqual(42f, controller.MatchTimeSeconds);
            Assert.IsTrue(ended);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesBuildingHpAndRuins()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            var main = FindBuilding(host, 1, GameIds.Buildings.Main);
            main.SetAuthoritativeHp(0f);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            var clientMain = FindBuilding(client, 1, GameIds.Buildings.Main);
            Assert.IsTrue(clientMain.IsRuins);
            Assert.AreEqual(0f, clientMain.CurrentHp);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesUnitsIntoCombat()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 1f, 1.5f, 4f, 1);
            var unit = host.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, 5f);
            unit.WorldPosition = new Vector3(12f, 0.15f, 3f);
            unit.FacingDirection = Vector3.right;

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            Assert.AreEqual(1, client.Combat.Units.Count);
            var restored = client.Combat.GetUnit(unit.UnitId);
            Assert.IsNotNull(restored);
            Assert.AreEqual(12f, restored.WorldPosition.x, 0.01f);
            Assert.AreEqual(3f, restored.WorldPosition.z, 0.01f);
            Assert.AreEqual(GameIds.Lanes.Center, restored.LaneId);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesPassiveGoldAndResearch()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.Players[0].PassiveGoldLevel = 2;
            host.Players[0].MainLevel = 2;
            var main = FindBuilding(host, 0, GameIds.Buildings.Main);
            Assert.IsTrue(host.Research.TryEnqueue(new BuildingResearchState(
                main.InstanceId,
                0,
                GameIds.Buildings.Main,
                GameIds.Upgrades.MainPassiveGold,
                costPaid: 200,
                durationSeconds: 25f)));
            host.Research.TryGetActive(main.InstanceId, out var active);
            active.RemainingSeconds = 9f;

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            Assert.AreEqual(2, client.Players[0].PassiveGoldLevel);
            Assert.AreEqual(2, client.Players[0].MainLevel);
            Assert.IsTrue(client.Research.TryGetActive(main.InstanceId, out var clientResearch));
            Assert.AreEqual(GameIds.Upgrades.MainPassiveGold, clientResearch.UpgradeId);
            Assert.AreEqual(9f, clientResearch.RemainingSeconds, 0.01f);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesBarracksLevel()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.WaveScheduler.SetBarracksLevel(0, GameIds.Buildings.BarracksLeft, 3);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            var barracks = client.WaveScheduler.GetBarracks(0, GameIds.Buildings.BarracksLeft);
            Assert.AreEqual(3, barracks.Level);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_RetargetsCenterOpponentSlot()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(4));
            Assert.IsTrue(host.Graph.TryGetLane(0, GameIds.Lanes.Center, out var hostLane));
            var oldOpponent = hostLane.OpponentSlot;
            var next = CenterMarchRetargetRules.ResolveNextAliveClockwise(oldOpponent, host.Players, 0);
            Assert.IsNotNull(next);

            host.Players[oldOpponent].IsEliminated = true;
            CenterLaneRetarget.Apply(oldOpponent, host.Players, host.Layout, host.Graph, host.Combat);
            Assert.IsTrue(host.Graph.TryGetLane(0, GameIds.Lanes.Center, out hostLane));
            Assert.AreEqual(next.Value, hostLane.OpponentSlot);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(4));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            Assert.IsTrue(client.Graph.TryGetLane(0, GameIds.Lanes.Center, out var clientLane));
            Assert.AreEqual(next.Value, clientLane.OpponentSlot);
            var newMain = client.Layout.Slots[next.Value].GetBuildingWorldPosition(GameIds.Buildings.Main);
            newMain.y = 0f;
            var end = clientLane.Path.End;
            end.y = 0f;
            Assert.Less(Vector3.Distance(end, newMain), 0.5f);
        }

        [Test]
        public void ResolveLocalGold_PrefersSnapshotOnClient()
        {
            Assert.AreEqual(
                333,
                MatchHudGoldRules.ResolveLocalGold(
                    localSlot: 0,
                    controllerGold: 500,
                    snapshotGold: 333,
                    useSnapshot: true));
            Assert.AreEqual(
                500,
                MatchHudGoldRules.ResolveLocalGold(
                    localSlot: 0,
                    controllerGold: 500,
                    snapshotGold: 333,
                    useSnapshot: false));
        }

        static BuildingState FindBuilding(MatchController controller, int ownerSlot, string buildingId)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.BuildingId == buildingId)
                {
                    return building;
                }
            }

            Assert.Fail($"Building {buildingId} not found for slot {ownerSlot}");
            return null;
        }
    }
}
