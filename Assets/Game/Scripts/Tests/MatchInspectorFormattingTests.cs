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
        public void FormatHp_RoundsToWholeNumbers()
        {
            Assert.AreEqual("1200/2000", MatchInspectorFormatting.FormatHp(1200f, 2000f));
        }

        [Test]
        public void FormatOwnerLabel_UsesOneBasedSlot()
        {
            Assert.AreEqual("Игрок 2", MatchInspectorFormatting.FormatOwnerLabel(1));
        }
    }
}
