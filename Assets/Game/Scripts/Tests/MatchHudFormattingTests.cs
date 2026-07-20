using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchHudFormattingTests
    {
        [Test]
        public void FormatMatchTime_FormatsMinutesAndSeconds()
        {
            Assert.AreEqual("00:00", MatchHudFormatting.FormatMatchTime(0f));
            Assert.AreEqual("01:05", MatchHudFormatting.FormatMatchTime(65f));
            Assert.AreEqual("12:34", MatchHudFormatting.FormatMatchTime(754f));
        }

        [Test]
        public void FormatPhase_ReturnsRussianLabels()
        {
            Assert.AreEqual("Старт", MatchHudFormatting.FormatPhase(MatchPhase.Start));
            Assert.AreEqual("Ранняя", MatchHudFormatting.FormatPhase(MatchPhase.Early));
            Assert.AreEqual("Средняя", MatchHudFormatting.FormatPhase(MatchPhase.Mid));
            Assert.AreEqual("Поздняя", MatchHudFormatting.FormatPhase(MatchPhase.Late));
        }

        [Test]
        public void FormatBarracksTimer_CeilsSeconds()
        {
            Assert.AreEqual("13", MatchHudFormatting.FormatBarracksTimer(12.1f, true));
            Assert.AreEqual("1", MatchHudFormatting.FormatBarracksTimer(0.2f, true));
            Assert.AreEqual("0", MatchHudFormatting.FormatBarracksTimer(0f, true));
        }

        [Test]
        public void FormatGold_FormatsNonNegativeInteger()
        {
            Assert.AreEqual("0", MatchHudFormatting.FormatGold(0));
            Assert.AreEqual("250", MatchHudFormatting.FormatGold(250));
            Assert.AreEqual("0", MatchHudFormatting.FormatGold(-5));
        }

        [Test]
        public void FormatBountyPopup_PrefixesPlus()
        {
            Assert.AreEqual("+12", MatchHudFormatting.FormatBountyPopup(12));
        }

        [Test]
        public void FormatCommandFeedback_NotEnoughGold()
        {
            Assert.AreEqual(
                "Не хватает золота",
                MatchHudFormatting.FormatCommandFeedback(MatchCommandResult.NotEnoughGold));
        }

        [Test]
        public void FormatMatchResult_UsesOneBasedSlot()
        {
            Assert.AreEqual("Победа: игрок 1", MatchHudFormatting.FormatMatchResult(0));
        }
    }
}
