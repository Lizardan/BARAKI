using System.Collections.Generic;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class MatchMinimapGeometryElement : VisualElement
    {
        const int GroundSegments = 48;
        const float RoadCasingWidth = 5f;
        const float RoadFillWidth = 3f;

        static readonly Color s_groundFill = new(0.13f, 0.16f, 0.12f, 1f);
        static readonly Color s_groundBorder = new(0.27f, 0.33f, 0.24f, 1f);
        static readonly Color s_centerFill = new(0.24f, 0.24f, 0.29f, 0.95f);
        static readonly Color s_centerStroke = new(0.5f, 0.5f, 0.56f, 0.85f);
        static readonly Color s_roadCasing = new(0.09f, 0.09f, 0.08f, 1f);
        static readonly Color s_roadFill = new(0.56f, 0.52f, 0.44f, 1f);

        MatchMinimapTopology _topology;
        float _arenaRadius = 120f;
        float _mapHalfExtent = 142f;
        float _panelWidth = 350f;
        float _panelHeight = 350f;
        float _viewYawDegrees;

        public MatchMinimapGeometryElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetDrawData(
            MatchMinimapTopology topology,
            float arenaRadius,
            float panelWidth,
            float panelHeight,
            float viewYawDegrees = 0f)
        {
            _topology = topology;
            _arenaRadius = Mathf.Max(1f, arenaRadius);
            _mapHalfExtent = MatchMinimapProjection.MapHalfExtent(_arenaRadius);
            _panelWidth = Mathf.Max(1f, panelWidth);
            _panelHeight = Mathf.Max(1f, panelHeight);
            _viewYawDegrees = viewYawDegrees;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (_topology == null)
            {
                return;
            }

            var painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            DrawGround(painter);
            DrawCenterArena(painter);
            DrawFilledRects(painter);
            DrawRoads(painter);
        }

        /// <summary>Square playable ground matching the square minimap panel.</summary>
        void DrawGround(Painter2D painter)
        {
            var corners = new List<Vector2>
            {
                MatchMinimapProjection.NormalizedToPanel(new Vector2(0f, 0f), _panelWidth, _panelHeight),
                MatchMinimapProjection.NormalizedToPanel(new Vector2(1f, 0f), _panelWidth, _panelHeight),
                MatchMinimapProjection.NormalizedToPanel(new Vector2(1f, 1f), _panelWidth, _panelHeight),
                MatchMinimapProjection.NormalizedToPanel(new Vector2(0f, 1f), _panelWidth, _panelHeight),
            };

            FillPolygon(painter, corners, s_groundFill);
            StrokePolygon(painter, corners, s_groundBorder, 2f);
        }

        void DrawCenterArena(Painter2D painter)
        {
            var radius = _topology.CenterArenaRadius;
            if (radius <= 0.01f)
            {
                return;
            }

            var center = Project(Vector2.zero);
            var radiusPx = Vector2.Distance(center, Project(new Vector2(radius, 0f)));
            if (radiusPx <= 0.5f)
            {
                return;
            }

            painter.fillColor = s_centerFill;
            painter.BeginPath();
            Circle(painter, center, radiusPx, move: true);
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = s_centerStroke;
            painter.lineWidth = 1.4f;
            painter.BeginPath();
            Circle(painter, center, radiusPx, move: true);
            painter.ClosePath();
            painter.Stroke();
        }

        void Circle(Painter2D painter, Vector2 center, float radiusPx, bool move)
        {
            for (var i = 0; i <= GroundSegments; i++)
            {
                var angle = Mathf.PI * 2f * i / GroundSegments;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusPx;
                if (i == 0 && move)
                {
                    painter.MoveTo(point);
                }
                else
                {
                    painter.LineTo(point);
                }
            }
        }

        void DrawFilledRects(Painter2D painter)
        {
            foreach (var rect in _topology.FilledRects)
            {
                var corners = GetRectCorners(rect);
                if (corners.Count < 4)
                {
                    continue;
                }

                if (rect.OwnerSlot >= 0)
                {
                    var color = MatchPlayerColors.GetSlotColor(rect.OwnerSlot);
                    FillPolygon(painter, corners, WithAlpha(color, 0.42f));
                    StrokePolygon(painter, corners, WithAlpha(color, 0.9f), 1.2f);
                }
                else
                {
                    FillPolygon(painter, corners, s_centerFill);
                    StrokePolygon(painter, corners, s_centerStroke, 1.4f);
                }
            }
        }

        void FillPolygon(Painter2D painter, List<Vector2> corners, Color fillColor)
        {
            painter.fillColor = fillColor;
            painter.BeginPath();
            painter.MoveTo(corners[0]);
            for (var i = 1; i < corners.Count; i++)
            {
                painter.LineTo(corners[i]);
            }

            painter.ClosePath();
            painter.Fill();
        }

        void StrokePolygon(Painter2D painter, List<Vector2> corners, Color strokeColor, float width)
        {
            painter.strokeColor = strokeColor;
            painter.lineWidth = width;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(corners[0]);
            for (var i = 1; i < corners.Count; i++)
            {
                painter.LineTo(corners[i]);
            }

            painter.ClosePath();
            painter.Stroke();
        }

        void DrawRoads(Painter2D painter)
        {
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            StrokeSegments(painter, s_roadCasing, RoadCasingWidth);
            StrokeSegments(painter, s_roadFill, RoadFillWidth);
        }

        void StrokeSegments(Painter2D painter, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            foreach (var segment in _topology.RoadSegments)
            {
                var a = Project(segment.A);
                var b = Project(segment.B);
                painter.BeginPath();
                painter.MoveTo(a);
                painter.LineTo(b);
                painter.Stroke();
            }
        }

        List<Vector2> GetRectCorners(MatchMinimapRect rect)
        {
            var projected = new List<Vector2>(4);
            for (var i = 0; i < 4; i++)
            {
                projected.Add(Project(rect.GetWorldCorner(i)));
            }

            return projected;
        }

        Vector2 Project(Vector2 worldXZ)
        {
            var normalized = MatchMinimapProjection.WorldToNormalizedUnclamped(
                new Vector3(worldXZ.x, 0f, worldXZ.y),
                _mapHalfExtent,
                _viewYawDegrees);
            return MatchMinimapProjection.NormalizedToPanel(normalized, _panelWidth, _panelHeight);
        }

        static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);
    }
}
