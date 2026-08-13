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
        const float AlignTolerancePx = 2.5f;

        [Test]
        public void BuildPreview_N2_HasCenterAndFlankGeometry()
        {
            var preview = RequireThumbnail(2);
            Assert.GreaterOrEqual(preview.RoadSegmentCount, 4);
            Assert.AreEqual(2, preview.BaseCenters.Count);
            Assert.Greater(preview.ArenaHalfPx, 0f);
            Assert.GreaterOrEqual(preview.StrokePointCount, 8);
        }

        [Test]
        public void BuildPreview_N2_BasesAreVertical()
        {
            var bases = RequireThumbnail(2).BaseCenters;
            Assert.AreEqual(2, bases.Count);
            Assert.AreEqual(bases[0].x, bases[1].x, AlignTolerancePx);
            Assert.AreEqual(PreviewCenter, bases[0].x, AlignTolerancePx);
            Assert.Greater(Mathf.Abs(bases[0].y - bases[1].y), 16f);
        }

        [Test]
        public void BuildPreview_N4_HasPerimeterAndSpokes()
        {
            var preview = RequireThumbnail(4);
            Assert.GreaterOrEqual(preview.RoadSegmentCount, 20);
            Assert.AreEqual(4, preview.BaseCenters.Count);
            Assert.GreaterOrEqual(preview.StrokePointCount, 40);
        }

        [Test]
        public void BuildPreview_N2_KeepsCornerArcSamples()
        {
            var preview = RequireThumbnail(2);
            Assert.GreaterOrEqual(preview.StrokePointCount, 20);
        }

        [Test]
        public void BuildPreview_N4_KeepsCornerArcSamples()
        {
            var preview = RequireThumbnail(4);
            Assert.GreaterOrEqual(preview.StrokePointCount, 40);
        }

        [Test]
        public void BuildPreview_N3_HasRingAndShortExits()
        {
            var preview = RequireThumbnail(3);
            Assert.GreaterOrEqual(preview.RoadSegmentCount, 12);
            Assert.AreEqual(3, preview.BaseCenters.Count);
            Assert.GreaterOrEqual(preview.StrokePointCount, 24);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void BuildPreview_ArenaIsSquare(int playerCount)
        {
            var preview = RequireThumbnail(playerCount);
            Assert.Greater(preview.ArenaHalfPx, 0f);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void BuildPreview_VisualBoundsAreCentered(int playerCount)
        {
            var center = RequireThumbnail(playerCount).GetVisualBoundsCenter();
            Assert.AreEqual(PreviewCenter, center.x, AlignTolerancePx);
            Assert.AreEqual(PreviewCenter, center.y, AlignTolerancePx);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void BuildPreview_DotsFitInsideSquare(int playerCount)
        {
            var preview = RequireThumbnail(playerCount);
            foreach (var c in preview.BaseCenters)
            {
                Assert.GreaterOrEqual(c.x, 0f);
                Assert.GreaterOrEqual(c.y, 0f);
                Assert.LessOrEqual(c.x, ModeMapThumbnailBuilder.PreviewSize);
                Assert.LessOrEqual(c.y, ModeMapThumbnailBuilder.PreviewSize);
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
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
        public void BuildPreview_N5_PlacesFiveBasesOnRing()
        {
            var preview = RequireThumbnail(5);
            Assert.AreEqual(5, preview.BaseCenters.Count);
            Assert.GreaterOrEqual(preview.RoadSegmentCount, 16);
            Assert.GreaterOrEqual(preview.StrokePointCount, 32);
        }

        [Test]
        public void BuildModeButton_DisablesNonMvpModes()
        {
            var duel = ModeMapThumbnailBuilder.BuildModeButton(2);
            var five = ModeMapThumbnailBuilder.BuildModeButton(5);
            Assert.IsTrue(duel.enabledSelf);
            Assert.IsFalse(five.enabledSelf);
            Assert.IsTrue(five.ClassListContains("mm-mode--disabled"));
        }

        static ModeMapThumbnailElement RequireThumbnail(int playerCount)
        {
            var preview = ModeMapThumbnailBuilder.BuildPreview(playerCount) as ModeMapThumbnailElement;
            Assert.IsNotNull(preview);
            return preview;
        }
    }
}
