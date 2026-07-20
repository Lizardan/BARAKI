using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchCommandTooltipRulesTests
    {
        [Test]
        public void GetTooltipTopLeft_PlacesBoxAboveButtonWithGap()
        {
            var button = new Rect(100f, 200f, 64f, 64f);
            var size = new Vector2(120f, 40f);
            var pos = MatchCommandTooltipRules.GetTooltipTopLeft(button, size, panelWidth: 800f);

            Assert.AreEqual(button.center.x - 60f, pos.x, 0.01f);
            Assert.AreEqual(
                button.yMin - MatchCommandTooltipRules.GapAboveButton - size.y,
                pos.y,
                0.01f);
        }
    }
}
