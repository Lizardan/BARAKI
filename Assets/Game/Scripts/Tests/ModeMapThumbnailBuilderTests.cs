using Game.UI;
using Game.UI.Controllers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests
{
    public sealed class ModeMapThumbnailBuilderTests
    {
        const float PreviewCenter = 32f;
        const float AlignTolerancePx = 1.25f;

        [Test]
        public void BuildPreview_N2_HasCenterAndFlankGeometry()
        {
            var preview = RequireThumbnail(2);
            Assert.GreaterOrEqual(preview.PolylineCount, 3);
            Assert.AreEqual(2, preview.BaseCenters.Count);
            Assert.Greater(preview.ArenaHalfPx, 0f);
            Assert.GreaterOrEqual(preview.StrokePointCount, 20);
        }

        [Test]
        public void BuildPreview_N2_BasesAreVertical()
        {
            var bases = RequireThumbnail(2).BaseCenters;
            Assert.AreEqual(2, bases.Count);
            Assert.AreEqual(bases[0].x, bases[1].x, AlignTolerancePx);
            Assert.AreEqual(PreviewCenter, bases[0].x, AlignTolerancePx);
            Assert.Greater(Mathf.Abs(bases[0].y - bases[1].y), 20f);
        }

        [Test]
        public void BuildPreview_N4_HasPerimeterAndSpokes()
        {
            var preview = RequireThumbnail(4);
            Assert.GreaterOrEqual(preview.PolylineCount, 5);
            Assert.AreEqual(4, preview.BaseCenters.Count);
            Assert.GreaterOrEqual(preview.StrokePointCount, 24);
        }

        [Test]
        public void BuildPreview_N2_KeepsCornerArcSamples()
        {
            var preview = RequireThumbnail(2);
            Assert.GreaterOrEqual(preview.StrokePointCount, 24);
        }

        [Test]
        public void BuildPreview_N4_KeepsCornerArcSamples()
        {
            var preview = RequireThumbnail(4);
            Assert.GreaterOrEqual(preview.StrokePointCount, 32);
        }

        [Test]
        public void BuildPreview_N3_HasRingAndShortExits()
        {
            var preview = RequireThumbnail(3);
            Assert.GreaterOrEqual(preview.PolylineCount, 4);
            Assert.AreEqual(3, preview.BaseCenters.Count);
            Assert.GreaterOrEqual(preview.StrokePointCount, 12);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(8)]
        public void BuildPreview_ArenaIsSquare(int playerCount)
        {
            var preview = RequireThumbnail(playerCount);
            Assert.Greater(preview.ArenaHalfPx, 0f);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(8)]
        public void BuildPreview_VisualBoundsAreCentered(int playerCount)
        {
            var center = RequireThumbnail(playerCount).GetVisualBoundsCenter();
            Assert.AreEqual(PreviewCenter, center.x, AlignTolerancePx);
            Assert.AreEqual(PreviewCenter, center.y, AlignTolerancePx);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(8)]
        public void BuildPreview_DotsFitInsideSquare(int playerCount)
        {
            var preview = RequireThumbnail(playerCount);
            var half = 4f;
            foreach (var c in preview.BaseCenters)
            {
                Assert.GreaterOrEqual(c.x - half, -0.5f);
                Assert.GreaterOrEqual(c.y - half, -0.5f);
                Assert.LessOrEqual(c.x + half, ModeMapThumbnailBuilder.PreviewSize + 0.5f);
                Assert.LessOrEqual(c.y + half, ModeMapThumbnailBuilder.PreviewSize + 0.5f);
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(8)]
        public void BuildPreview_HasBaseExactlyAtBottomCenter(int playerCount)
        {
            var bases = RequireThumbnail(playerCount).BaseCenters;
            Assert.AreEqual(playerCount, bases.Count);

            var bottom = bases[0];
            foreach (var dot in bases)
            {
                if (dot.y > bottom.y)
                {
                    bottom = dot;
                }
            }

            Assert.AreEqual(PreviewCenter, bottom.x, AlignTolerancePx);
        }

        [Test]
        public void BuildPreview_N8_PlacesEightBasesOnRing()
        {
            var preview = RequireThumbnail(8);
            Assert.AreEqual(8, preview.BaseCenters.Count);
            Assert.GreaterOrEqual(preview.PolylineCount, 9);
            Assert.GreaterOrEqual(preview.StrokePointCount, 24);
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

        static ModeMapThumbnailElement RequireThumbnail(int playerCount)
        {
            var preview = ModeMapThumbnailBuilder.BuildPreview(playerCount) as ModeMapThumbnailElement;
            Assert.IsNotNull(preview);
            return preview;
        }
    }
}
