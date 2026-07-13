using Game.Core;

using Game.UI;

using NUnit.Framework;



namespace Game.Tests

{

    public sealed class MatchInspectorFormattingTests

    {

        [Test]

        public void FormatBuildingName_Main_IsLocalized()

        {

            Assert.AreEqual("Главное", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.Main));

        }



        [Test]

        public void FormatBuildingName_Barracks_DropsSideSuffix()

        {

            Assert.AreEqual("Казармы", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.BarracksLeft));

            Assert.AreEqual("Казармы", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.BarracksCenter));

            Assert.AreEqual("Казармы", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.BarracksRight));

        }



        [Test]

        public void FormatBuildingName_Towers_AreGeneric()

        {

            Assert.AreEqual("Башня", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.TowerNw));

            Assert.AreEqual("Башня", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.TowerNe));

            Assert.AreEqual("Башня", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.TowerSw));

            Assert.AreEqual("Башня", MatchInspectorFormatting.FormatBuildingName(GameIds.Buildings.TowerSe));

        }



        [Test]

        public void FormatHp_RoundsToWholeNumbers()

        {

            Assert.AreEqual("1200/2000", MatchInspectorFormatting.FormatHp(1200f, 2000f));

        }



        [Test]

        public void FormatOwnerLabel_UsesOneBasedSlot()

        {

            Assert.AreEqual("Игрок 2", MatchInspectorFormatting.FormatOwnerLabel(1));

        }



        [Test]

        public void IsResearchBuilding_MainAndTowers_ReturnTrue()

        {

            Assert.IsTrue(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.Main));

            Assert.IsTrue(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.TowerNw));

            Assert.IsTrue(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.TowerNe));

            Assert.IsTrue(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.TowerSw));

            Assert.IsTrue(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.TowerSe));

        }



        [Test]

        public void IsResearchBuilding_BarracksAndUnknown_ReturnFalse()

        {

            Assert.IsFalse(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.BarracksLeft));

            Assert.IsFalse(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.BarracksCenter));

            Assert.IsFalse(MatchInspectorFormatting.IsResearchBuilding(GameIds.Buildings.BarracksRight));

            Assert.IsFalse(MatchInspectorFormatting.IsResearchBuilding(null));

            Assert.IsFalse(MatchInspectorFormatting.IsResearchBuilding(string.Empty));

        }

    }

}

