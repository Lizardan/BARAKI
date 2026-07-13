using Game.UI.Controllers;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Game.Tests
{
    public sealed class MatchSelectionUiPointerTests
    {
        [Test]
        public void IsDescendantOf_ReturnsTrueForSelfAndChildren()
        {
            var parent = new VisualElement();
            var child = new VisualElement();
            parent.Add(child);

            Assert.IsTrue(MatchSelectionUiPointer.IsDescendantOf(parent, parent));
            Assert.IsTrue(MatchSelectionUiPointer.IsDescendantOf(parent, child));
        }

        [Test]
        public void IsDescendantOf_ReturnsFalseForUnrelatedOrNullAncestor()
        {
            var a = new VisualElement();
            var b = new VisualElement();

            Assert.IsFalse(MatchSelectionUiPointer.IsDescendantOf(null, a));
            Assert.IsFalse(MatchSelectionUiPointer.IsDescendantOf(a, b));
        }
    }
}
