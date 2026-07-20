using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>Rectangular contour progress drawn clockwise from top-left on a command button.</summary>
    public sealed class MatchCommandRegenFrameElement : VisualElement
    {
        const float LineWidth = 3.5f;
        static readonly Color TrackColor = new(0.18f, 0.15f, 0.12f, 0.9f);
        static readonly Color FillColor = new(1f, 0.84f, 0.38f, 1f);

        float _fill01;

        public MatchCommandRegenFrameElement()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("match-command-regen-frame");
            generateVisualContent += OnGenerateVisualContent;
        }

        public float Fill01
        {
            get => _fill01;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_fill01, clamped))
                {
                    return;
                }

                _fill01 = clamped;
                MarkDirtyRepaint();
            }
        }

        public void SetRegenerating(bool regenerating, float fill01)
        {
            Fill01 = fill01;
            style.display = regenerating ? DisplayStyle.Flex : DisplayStyle.None;
            if (regenerating)
            {
                MarkDirtyRepaint();
            }
        }

        void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width < 4f || rect.height < 4f)
            {
                return;
            }

            var inset = LineWidth * 0.5f;
            var x0 = rect.xMin + inset;
            var y0 = rect.yMin + inset;
            var x1 = rect.xMax - inset;
            var y1 = rect.yMax - inset;
            var w = x1 - x0;
            var h = y1 - y0;
            if (w < 1f || h < 1f)
            {
                return;
            }

            var painter = ctx.painter2D;
            painter.lineCap = LineCap.Butt;
            painter.lineJoin = LineJoin.Miter;
            painter.lineWidth = LineWidth;

            painter.strokeColor = TrackColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x0, y0));
            painter.LineTo(new Vector2(x1, y0));
            painter.LineTo(new Vector2(x1, y1));
            painter.LineTo(new Vector2(x0, y1));
            painter.ClosePath();
            painter.Stroke();

            if (_fill01 <= 0.001f)
            {
                return;
            }

            var perimeter = (w + h) * 2f;
            var drawLength = perimeter * Mathf.Clamp01(_fill01);
            painter.strokeColor = FillColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x0, y0));

            // Clockwise: top → right → bottom → left.
            DrawSegment(painter, ref drawLength, new Vector2(x0, y0), new Vector2(x1, y0));
            DrawSegment(painter, ref drawLength, new Vector2(x1, y0), new Vector2(x1, y1));
            DrawSegment(painter, ref drawLength, new Vector2(x1, y1), new Vector2(x0, y1));
            DrawSegment(painter, ref drawLength, new Vector2(x0, y1), new Vector2(x0, y0));
            painter.Stroke();
        }

        static void DrawSegment(Painter2D painter, ref float remaining, Vector2 from, Vector2 to)
        {
            if (remaining <= 0f)
            {
                return;
            }

            var delta = to - from;
            var segLen = delta.magnitude;
            if (segLen <= 0.0001f)
            {
                return;
            }

            if (remaining >= segLen)
            {
                painter.LineTo(to);
                remaining -= segLen;
                return;
            }

            painter.LineTo(from + delta * (remaining / segLen));
            remaining = 0f;
        }
    }
}
