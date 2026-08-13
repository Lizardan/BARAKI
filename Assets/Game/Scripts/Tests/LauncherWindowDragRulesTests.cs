using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class LauncherWindowDragRulesTests
    {
        [Test]
        public void DragThreshold_IsSmallEnoughForClicks()
        {
            Assert.Greater(LauncherWindowDragRules.DragThresholdPixels, 0f);
            Assert.LessOrEqual(LauncherWindowDragRules.DragThresholdPixels, 12f);
        }

        [Test]
        public void ShouldBlockDragArm_TextAndClose()
        {
            Assert.IsTrue(LauncherWindowDragRules.ShouldBlockDragArm(isTextInput: true, isCloseControl: false));
            Assert.IsTrue(LauncherWindowDragRules.ShouldBlockDragArm(isTextInput: false, isCloseControl: true));
            Assert.IsFalse(LauncherWindowDragRules.ShouldBlockDragArm(isTextInput: false, isCloseControl: false));
        }

        [Test]
        public void ShouldBeginDrag_RespectsThreshold()
        {
            var press = new Vector2(100f, 100f);
            Assert.IsFalse(
                LauncherWindowDragRules.ShouldBeginDrag(press, press + new Vector2(3f, 0f)));
            Assert.IsTrue(
                LauncherWindowDragRules.ShouldBeginDrag(press, press + new Vector2(6f, 0f)));
            Assert.IsTrue(
                LauncherWindowDragRules.ShouldBeginDrag(press, press + new Vector2(0f, 10f)));
        }

        [Test]
        public void OffsetWindowPosition_AddsDelta()
        {
            var next = LauncherWindowDragRules.OffsetWindowPosition(
                new Vector2Int(40, 80),
                new Vector2Int(12, -5));
            Assert.AreEqual(new Vector2Int(52, 75), next);
        }
    }
}
