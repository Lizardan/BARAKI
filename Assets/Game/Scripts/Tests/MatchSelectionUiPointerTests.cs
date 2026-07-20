using Game.UI.Controllers;
using NUnit.Framework;
using UnityEngine;
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

        [Test]
        public void ToUiToolkitScreenPosition_FlipsYFromBottomLeftOrigin()
        {
            var bottomLeft = new Vector2(100f, 50f);
            var topLeft = MatchSelectionUiPointer.ToUiToolkitScreenPosition(bottomLeft, screenHeight: 1080f);

            Assert.AreEqual(100f, topLeft.x);
            Assert.AreEqual(1030f, topLeft.y);
        }

        [Test]
        public void IsPointerOverBlockedUi_TrueWhenPickedInsideDock()
        {
            var dock = new VisualElement { name = "BottomDock" };
            var button = new Button { name = "CommandSlot0" };
            dock.Add(button);

            Assert.IsTrue(MatchSelectionUiPointer.IsPointerOverBlockedUi(
                picked: button,
                bottomDock: dock,
                topBar: null));
        }

        [Test]
        public void IsPointerOverBlockedUi_FalseWhenPickMissesHudChrome()
        {
            var dock = new VisualElement { name = "BottomDock" };
            var unrelated = new VisualElement();

            Assert.IsFalse(MatchSelectionUiPointer.IsPointerOverBlockedUi(
                picked: null,
                bottomDock: dock,
                topBar: null));
            Assert.IsFalse(MatchSelectionUiPointer.IsPointerOverBlockedUi(
                picked: unrelated,
                bottomDock: dock,
                topBar: null));
        }

        [Test]
        public void IsPointerOverBlockedUi_TrueWhenPickedInsideDebugPanel()
        {
            var debug = new VisualElement { name = "DebugHudPanel" };
            var button = new Button { name = "DebugAddGoldButton" };
            debug.Add(button);

            Assert.IsTrue(MatchSelectionUiPointer.IsPointerOverBlockedUi(
                picked: button,
                bottomDock: null,
                topBar: null,
                debugHudPanel: debug));
        }
    }
}
