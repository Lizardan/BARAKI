using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    public sealed class RaceContentTests
    {
        private RaceCatalog _catalog;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Editor.RaceContentBuilder.EnsureContent();
            _catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(Game.Editor.RaceContentBuilder.CatalogPath);
        }

        [Test]
        public void RaceCatalog_Exists()
        {
            Assert.IsNotNull(_catalog);
            Assert.GreaterOrEqual(_catalog.Races.Count, 2);
        }

        [Test]
        public void HumanRace_HasSixUnitsAndThreeHeroes()
        {
            var human = _catalog.GetRace(GameIds.Races.Human);
            Assert.IsNotNull(human);
            Assert.IsNotNull(human.Melee);
            Assert.IsNotNull(human.Ranged);
            Assert.IsNotNull(human.Caster);
            Assert.IsNotNull(human.Siege);
            Assert.IsNotNull(human.Flying);
            Assert.IsNotNull(human.Super);
            Assert.AreEqual(3, human.Heroes.Count);
            Assert.AreEqual(GameIds.Units.HumanMelee, human.Melee.Id);
        }

        [Test]
        public void BugRace_HasSixUnitsAndThreeHeroes()
        {
            var bug = _catalog.GetRace(GameIds.Races.Bug);
            Assert.IsNotNull(bug);
            Assert.AreEqual(GameIds.Units.BugSuper, bug.Super.Id);
            Assert.AreEqual(3, bug.Heroes.Count);
        }

        [Test]
        public void SquadCompositions_L1ThroughL4_MatchGddTotals()
        {
            Assert.AreEqual(4, _catalog.GetSquad(1).TotalUnits);
            Assert.AreEqual(7, _catalog.GetSquad(2).TotalUnits);
            Assert.AreEqual(10, _catalog.GetSquad(3).TotalUnits);
            Assert.AreEqual(14, _catalog.GetSquad(4).TotalUnits);
        }

        [Test]
        public void HumanCaster_HasMaxMana()
        {
            var human = _catalog.GetRace(GameIds.Races.Human);
            Assert.AreEqual(200f, human.Caster.MaxMana, 0.01f);
            Assert.IsTrue(UnitCombatStats.FromDefinition(human.Caster).HasMana);
        }

        [Test]
        public void BugCaster_HasMaxMana()
        {
            var bug = _catalog.GetRace(GameIds.Races.Bug);
            Assert.AreEqual(200f, bug.Caster.MaxMana, 0.01f);
        }

        [Test]
        public void StatTracks_HaveNineLevelsAndGddEffect()
        {
            var melee = _catalog.GetStatTrack(GameIds.Upgrades.MeleeDamage);
            Assert.IsNotNull(melee);
            Assert.AreEqual(9, melee.MaxLevel);
            Assert.AreEqual(0.03f, melee.EffectPerLevel, 0.0001f);
            Assert.AreEqual(9, melee.CostsGold.Length);
        }

        [Test]
        public void AllUnitDefinitions_ShareBaselineMoveSpeedInAssets()
        {
            foreach (var raceId in new[] { GameIds.Races.Human, GameIds.Races.Bug })
            {
                var race = _catalog.GetRace(raceId);
                foreach (var unit in new[] { race.Melee, race.Ranged, race.Caster, race.Siege, race.Flying, race.Super })
                {
                    Assert.AreEqual(RaceMarchSpeedRules.BaseMarchSpeed, unit.MoveSpeed, 0.001f, unit.Id);
                    Assert.AreEqual(0f, unit.MarchSpeedOverride, 0.001f, unit.Id);
                }
            }
        }

        [Test]
        public void RaceMarchSpeed_HumanBase_BugFrenzyBoost()
        {
            var human = _catalog.GetRace(GameIds.Races.Human);
            var bug = _catalog.GetRace(GameIds.Races.Bug);
            Assert.AreEqual(RaceMarchSpeedRules.BaseMarchSpeed, RaceMarchSpeedRules.GetMarchSpeed(human), 0.001f);
            var bugExpected = RaceMarchSpeedRules.BaseMarchSpeed * RaceMarchSpeedRules.BugFrenzyMoveMultiplier;
            Assert.AreEqual(bugExpected, RaceMarchSpeedRules.GetMarchSpeed(bug), 0.001f);
        }
    }
}
