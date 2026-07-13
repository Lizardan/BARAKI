using Game.UI.Controllers;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Game.Tests
{
    public sealed class ModeMapThumbnailBuilderTests
    {
        [Test]
        public void BuildPreview_N2_HasCenterAndFlankGeometry()
        {
            var preview = ModeMapThumbnailBuilder.BuildPreview(2);
            Assert.GreaterOrEqual(CountClass(preview, "mm-mode-lane"), 8);
            Assert.AreEqual(2, CountClass(preview, "mm-mode-dot"));
            Assert.AreEqual(1, CountClass(preview, "mm-mode-arena"));
        }

        [Test]
        public void BuildPreview_N4_HasPerimeterAndSpokes()
        {
            var preview = ModeMapThumbnailBuilder.BuildPreview(4);
            Assert.GreaterOrEqual(CountClass(preview, "mm-mode-lane"), 12);
            Assert.AreEqual(4, CountClass(preview, "mm-mode-dot"));
        }

        [Test]
        public void BuildPreview_N8_PlacesEightBasesOnRing()
        {
            var preview = ModeMapThumbnailBuilder.BuildPreview(8);
            Assert.AreEqual(8, CountClass(preview, "mm-mode-dot"));
            Assert.GreaterOrEqual(CountClass(preview, "mm-mode-lane"), 16);
        }

        [Test]
        public void BuildPreview_N2_DrawsCornerChords()
        {
            var preview = ModeMapThumbnailBuilder.BuildPreview(2);
            // Stadium has two flanks × (exit straights + corner arcs + mid straight) → many lanes.
            Assert.GreaterOrEqual(CountClass(preview, "mm-mode-lane"), 16);
        }

        [Test]
        public void BuildPreview_N4_DrawsCornerChords()
        {
            var preview = ModeMapThumbnailBuilder.BuildPreview(4);
            // Square ring includes 4 corner arcs that must survive preview decimation.
            Assert.GreaterOrEqual(CountClass(preview, "mm-mode-lane"), 20);
        }

        [Test]
        public void BuildModeButton_DisablesNonMvpModes()
        {
            var duel = ModeMapThumbnailBuilder.BuildModeButton(2);
            var eight = ModeMapThumbnailBuilder.BuildModeButton(8);
            Assert.IsTrue(duel.enabledSelf);
            Assert.IsFalse(eight.enabledSelf);
            Assert.IsTrue(eight.ClassListContains("mm-mode--disabled"));
        }

        static int CountClass(VisualElement root, string className)
        {
            var count = 0;
            CountClassRecursive(root, className, ref count);
            return count;
        }

        static void CountClassRecursive(VisualElement element, string className, ref int count)
        {
            if (element.ClassListContains(className))
            {
                count++;
            }

            for (var i = 0; i < element.childCount; i++)
            {
                CountClassRecursive(element[i], className, ref count);
            }
        }
    }
}
