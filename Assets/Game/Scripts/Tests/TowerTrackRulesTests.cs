using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TowerTrackRulesTests
    {
        [Test]
        public void TrackIds_NineUniqueKnown()
        {
            Assert.AreEqual(9, TowerTrackRules.TrackCount);
            Assert.AreEqual(9, TowerTrackRules.TrackIds.Length);
            var seen = new HashSet<string>();
            foreach (var id in TowerTrackRules.TrackIds)
            {
                StringAssert.StartsWith("UPG_TOWER_HUMAN_", id);
                Assert.IsTrue(seen.Add(id), $"Duplicate tower track id {id}");
            }

            StringAssert.Contains("FLAMING_ARROWS", TowerTrackRules.TrackIds[0]);
        }

        [Test]
        public void LegacyTowerUpgradeIds_AreScrapped()
        {
            Assert.IsFalse(TowerTrackRules.IsTowerTrack("UPG_TOWER_HUMAN_STEEL_TEMPER"));
            Assert.IsFalse(TowerTrackRules.IsTowerTrack("UPG_TOWER_HUMAN_HOLD_THE_LINE"));
            Assert.IsFalse(TowerTrackRules.IsTowerTrack("UPG_TOWER_HUMAN_BALLISTA_OVERDRAW"));
            Assert.IsFalse(TowerTrackRules.IsTowerTrack("UPG_TOWER_HUMAN_ARCANE_RELAY"));
        }

        [Test]
        public void Economy_UsesGddLevels()
        {
            Assert.AreEqual(3, MatchEconomyRules.MaxTowerTrackLevel);
            CollectionAssert.AreEqual(new[] { 500, 800, 1200 }, MatchEconomyRules.TowerTrackCosts);
            CollectionAssert.AreEqual(
                new[] { 45f, 90f, 135f },
                MatchEconomyRules.TowerTrackDurationsSeconds);
        }

        [Test]
        public void TryGetTowerTrackUpgrade_SequentialPerLevel()
        {
            foreach (var trackId in TowerTrackRules.TrackIds)
            {
                Assert.IsTrue(MatchEconomyRules.TryGetTowerTrackUpgrade(trackId, 0, out var cost1, out var time1));
                Assert.AreEqual(500, cost1);
                Assert.AreEqual(45f, time1);

                Assert.IsTrue(MatchEconomyRules.TryGetTowerTrackUpgrade(trackId, 1, out var cost2, out var time2));
                Assert.AreEqual(800, cost2);
                Assert.AreEqual(90f, time2);

                Assert.IsTrue(MatchEconomyRules.TryGetTowerTrackUpgrade(trackId, 2, out var cost3, out var time3));
                Assert.AreEqual(1200, cost3);
                Assert.AreEqual(135f, time3);

                Assert.IsFalse(MatchEconomyRules.TryGetTowerTrackUpgrade(trackId, 3, out _, out _));
                Assert.IsFalse(MatchEconomyRules.TryGetTowerTrackUpgrade(trackId, -1, out _, out _));
            }
        }

        [Test]
        public void TryGetTowerTrackUpgrade_RejectsUnknownId() =>
            Assert.IsFalse(MatchEconomyRules.TryGetTowerTrackUpgrade("UPG_MAIN_MAGIC", 0, out _, out _));

        [Test]
        public void RoleMatches_FollowsGddRoleSets()
        {
            Assert.IsTrue(TowerTrackRules.RoleMatches(0, UnitRole.Ranged));
            Assert.IsTrue(TowerTrackRules.RoleMatches(0, UnitRole.Flying));
            Assert.IsFalse(TowerTrackRules.RoleMatches(0, UnitRole.Melee));

            Assert.IsTrue(TowerTrackRules.RoleMatches(1, UnitRole.Melee));
            Assert.IsTrue(TowerTrackRules.RoleMatches(1, UnitRole.Siege));

            Assert.IsTrue(TowerTrackRules.RoleMatches(2, UnitRole.Flying));
            Assert.IsTrue(TowerTrackRules.RoleMatches(3, UnitRole.Super));
            Assert.IsTrue(TowerTrackRules.RoleMatches(4, UnitRole.Caster));
            Assert.IsTrue(TowerTrackRules.RoleMatches(5, UnitRole.Ranged));
            Assert.IsTrue(TowerTrackRules.RoleMatches(6, UnitRole.Caster));

            // Field Medics / Last Stand affect every regular unit.
            for (var role = 0; role <= (int)UnitRole.Titan; role++)
            {
                Assert.IsTrue(TowerTrackRules.RoleMatches(7, (UnitRole)role), $"track 7 role {(UnitRole)role}");
            }
        }

        [Test]
        public void GetLevel_ClampsAndHandlesMissing()
        {
            var levels = new[] { 1, 2, 3, 0, 0, 0, 0, 0, 0 };
            Assert.AreEqual(3, TowerTrackRules.GetLevel(levels, 2));
            Assert.AreEqual(1, TowerTrackRules.GetLevel(levels, GameIds.Upgrades.TowerHumanFlamingArrows));
            Assert.AreEqual(0, TowerTrackRules.GetLevel(null, 0));
            Assert.AreEqual(0, TowerTrackRules.GetLevel(levels, 42));
            Assert.AreEqual(0, TowerTrackRules.GetLevel(levels, "unknown"));
        }

        [Test]
        public void PlayerState_TrackLevels_DefaultZeroAndClamped()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Human, 100);
            for (var i = 0; i < TowerTrackRules.TrackCount; i++)
            {
                Assert.AreEqual(0, player.GetTowerTrackLevel(i));
            }

            player.SetTowerTrackLevels(new[] { -4, 1, 99 });
            Assert.AreEqual(0, player.GetTowerTrackLevel(0));
            Assert.AreEqual(1, player.GetTowerTrackLevel(1));
            Assert.AreEqual(MatchEconomyRules.MaxTowerTrackLevel, player.GetTowerTrackLevel(2));
        }
    }
}
