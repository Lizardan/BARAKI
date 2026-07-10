using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchSelectionTests
    {
        [Test]
        public void SelectUnit_FiresChangedOnce()
        {
            var selection = new Game.Gameplay.Match.Selection.MatchSelection();
            var count = 0;
            Game.Gameplay.Match.Selection.MatchPickTarget? last = null;
            selection.Changed += target =>
            {
                count++;
                last = target;
            };

            selection.SelectUnit(7);
            selection.SelectUnit(7);

            Assert.AreEqual(1, count);
            Assert.IsTrue(last.Value.IsUnit);
            Assert.AreEqual(7, last.Value.EntityId);
        }

        [Test]
        public void Clear_ResetsTarget()
        {
            var selection = new Game.Gameplay.Match.Selection.MatchSelection();
            selection.SelectBuilding(3);
            selection.Clear();

            Assert.IsFalse(selection.Current.HasTarget);
        }
    }
}
