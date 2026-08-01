using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// Anti-aliased Mode Select map schematic via Painter2D.
    /// Geometry is authored in a 64×64 space, then re-fitted into <see cref="contentRect"/>
    /// on every paint so the schematic stays centered on the black preview square.
    /// </summary>
    public sealed class ModeMapThumbnailElement : VisualElement
    {
        public const float AuthoredSize = 64f;
        const float PaintMargin = 5f;

        static readonly Color s_laneStroke = new(184f / 255f, 169f / 255f, 130f / 255f, 0.9f);
        static readonly Color s_arenaFill = new(125f / 255f, 117f / 255f, 104f / 255f, 0.4f);
        static readonly Color s_arenaStroke = new(184f / 255f, 169f / 255f, 130f / 255f, 1f);
        static readonly Color s_baseFill = new(125f / 255f, 117f / 255f, 104f / 255f, 1f);

        readonly List<List<Vector2>> _polylines = new();
        readonly List<Vector2> _bases = new();
        float _arenaHalf;
        float _dotHalf = 4f;
        float _laneWidth = 2f;

        public ModeMapThumbnailElement()
        {
            AddToClassList("mm-mode__preview");
            pickingMode = PickingMode.Ignore;
            style.width = AuthoredSize;
            style.height = AuthoredSize;
            style.minWidth = AuthoredSize;
            style.minHeight = AuthoredSize;
            style.flexShrink = 0;
            generateVisualContent += OnGenerateVisualContent;
        }

        public IReadOnlyList<Vector2> BaseCenters => _bases;

        public float ArenaHalfPx => _arenaHalf;

        public int PolylineCount => _polylines.Count;

        /// <summary>Center of roads + base-dot AABB in authored preview pixels.</summary>
        public Vector2 GetVisualBoundsCenter(float dotHalf = 4f)
        {
            if (!TryGetAuthoredBounds(dotHalf, _laneWidth * 0.5f, out var min, out var max))
            {
                return new Vector2(AuthoredSize * 0.5f, AuthoredSize * 0.5f);
            }

            return (min + max) * 0.5f;
        }

        /// <summary>Total polyline vertices (used by tests as a stand-in for path richness).</summary>
        public int StrokePointCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _polylines.Count; i++)
                {
                    count += _polylines[i].Count;
                }

                return count;
            }
        }

        public void SetGeometry(
            IReadOnlyList<IReadOnlyList<Vector2>> polylines,
            IReadOnlyList<Vector2> bases,
            float arenaHalfPx,
            float dotHalfPx = 4f,
            float laneWidthPx = 2f)
        {
            _polylines.Clear();
            _bases.Clear();

            if (polylines != null)
            {
                for (var i = 0; i < polylines.Count; i++)
                {
                    var src = polylines[i];
                    if (src == null || src.Count < 2)
                    {
                        continue;
                    }

                    _polylines.Add(new List<Vector2>(src));
                }
            }

            if (bases != null)
            {
                for (var i = 0; i < bases.Count; i++)
                {
                    _bases.Add(bases[i]);
                }
            }

            _arenaHalf = Mathf.Max(4f, arenaHalfPx);
            _dotHalf = Mathf.Max(2f, dotHalfPx);
            _laneWidth = Mathf.Max(1f, laneWidthPx);
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            var rect = contentRect;
            if (rect.width < 1f || rect.height < 1f)
            {
                rect = new Rect(0f, 0f, AuthoredSize, AuthoredSize);
            }

            var map = BuildPaintMap(rect);
            DrawArena(painter, map);
            DrawRoads(painter, map);
            DrawBases(painter, map);
        }

        PaintMap BuildPaintMap(Rect rect)
        {
            var strokePad = _laneWidth * 0.5f;
            if (!TryGetAuthoredBounds(_dotHalf, strokePad, out var min, out var max))
            {
                min = Vector2.zero;
                max = new Vector2(AuthoredSize, AuthoredSize);
            }

            // Include center arena in bounds so the whole schematic shares one center.
            var authoredCenter = new Vector2(AuthoredSize * 0.5f, AuthoredSize * 0.5f);
            min = Vector2.Min(min, authoredCenter - new Vector2(_arenaHalf, _arenaHalf));
            max = Vector2.Max(max, authoredCenter + new Vector2(_arenaHalf, _arenaHalf));

            var size = max - min;
            size.x = Mathf.Max(size.x, 1f);
            size.y = Mathf.Max(size.y, 1f);

            var inner = new Rect(
                rect.x + PaintMargin,
                rect.y + PaintMargin,
                Mathf.Max(1f, rect.width - PaintMargin * 2f),
                Mathf.Max(1f, rect.height - PaintMargin * 2f));

            var scale = Mathf.Min(inner.width / size.x, inner.height / size.y);
            var srcCenter = (min + max) * 0.5f;
            var dstCenter = inner.center;

            return new PaintMap(srcCenter, dstCenter, scale);
        }

        bool TryGetAuthoredBounds(float dotHalf, float strokePad, out Vector2 min, out Vector2 max)
        {
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            var any = false;

            for (var i = 0; i < _polylines.Count; i++)
            {
                var poly = _polylines[i];
                for (var p = 0; p < poly.Count; p++)
                {
                    var pt = poly[p];
                    minX = Mathf.Min(minX, pt.x - strokePad);
                    maxX = Mathf.Max(maxX, pt.x + strokePad);
                    minY = Mathf.Min(minY, pt.y - strokePad);
                    maxY = Mathf.Max(maxY, pt.y + strokePad);
                    any = true;
                }
            }

            for (var i = 0; i < _bases.Count; i++)
            {
                var b = _bases[i];
                minX = Mathf.Min(minX, b.x - dotHalf);
                maxX = Mathf.Max(maxX, b.x + dotHalf);
                minY = Mathf.Min(minY, b.y - dotHalf);
                maxY = Mathf.Max(maxY, b.y + dotHalf);
                any = true;
            }

            min = new Vector2(minX, minY);
            max = new Vector2(maxX, maxY);
            return any;
        }

        void DrawArena(Painter2D painter, PaintMap map)
        {
            var c = map.Map(new Vector2(AuthoredSize * 0.5f, AuthoredSize * 0.5f));
            var half = _arenaHalf * map.Scale;
            var p0 = new Vector2(c.x - half, c.y - half);
            var p1 = new Vector2(c.x + half, c.y - half);
            var p2 = new Vector2(c.x + half, c.y + half);
            var p3 = new Vector2(c.x - half, c.y + half);

            painter.fillColor = s_arenaFill;
            painter.BeginPath();
            painter.MoveTo(p0);
            painter.LineTo(p1);
            painter.LineTo(p2);
            painter.LineTo(p3);
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = s_arenaStroke;
            painter.lineWidth = Mathf.Max(1f, 1.5f * map.Scale);
            painter.lineJoin = LineJoin.Miter;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            painter.MoveTo(p0);
            painter.LineTo(p1);
            painter.LineTo(p2);
            painter.LineTo(p3);
            painter.ClosePath();
            painter.Stroke();
        }

        void DrawRoads(Painter2D painter, PaintMap map)
        {
            painter.strokeColor = s_laneStroke;
            painter.lineWidth = Mathf.Max(1f, _laneWidth * map.Scale);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            for (var i = 0; i < _polylines.Count; i++)
            {
                var poly = _polylines[i];
                if (poly.Count < 2)
                {
                    continue;
                }

                painter.BeginPath();
                painter.MoveTo(map.Map(poly[0]));
                for (var p = 1; p < poly.Count; p++)
                {
                    painter.LineTo(map.Map(poly[p]));
                }

                painter.Stroke();
            }
        }

        void DrawBases(Painter2D painter, PaintMap map)
        {
            painter.fillColor = s_baseFill;
            var h = _dotHalf * map.Scale;
            for (var i = 0; i < _bases.Count; i++)
            {
                var c = map.Map(_bases[i]);
                painter.BeginPath();
                painter.MoveTo(new Vector2(c.x - h, c.y - h));
                painter.LineTo(new Vector2(c.x + h, c.y - h));
                painter.LineTo(new Vector2(c.x + h, c.y + h));
                painter.LineTo(new Vector2(c.x - h, c.y + h));
                painter.ClosePath();
                painter.Fill();
            }
        }

        readonly struct PaintMap
        {
            readonly Vector2 _srcCenter;
            readonly Vector2 _dstCenter;
            public readonly float Scale;

            public PaintMap(Vector2 srcCenter, Vector2 dstCenter, float scale)
            {
                _srcCenter = srcCenter;
                _dstCenter = dstCenter;
                Scale = scale;
            }

            public Vector2 Map(Vector2 authored) =>
                _dstCenter + (authored - _srcCenter) * Scale;
        }
    }
}
