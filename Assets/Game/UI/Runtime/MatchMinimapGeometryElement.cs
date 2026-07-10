using System.Collections.Generic;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class MatchMinimapGeometryElement : VisualElement
    {
        static readonly Color s_centerFill = new(0.16f, 0.15f, 0.22f, 0.95f);
        static readonly Color s_baseFillNeutral = new(0.22f, 0.2f, 0.28f, 0.85f);
        static readonly Color s_roadStroke = new(0.35f, 0.37f, 0.43f, 1f);

        MatchMinimapTopology _topology;
        float _arenaRadius = 120f;
        float _panelWidth = 350f;
        float _panelHeight = 350f;

        public MatchMinimapGeometryElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetDrawData(
            MatchMinimapTopology topology,
            float arenaRadius,
            float panelWidth,
            float panelHeight)
        {
            _topology = topology;
            _arenaRadius = Mathf.Max(1f, arenaRadius);
            _panelWidth = Mathf.Max(1f, panelWidth);
            _panelHeight = Mathf.Max(1f, panelHeight);
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

            foreach (var rect in _topology.FilledRects)
            {
                var fill = rect.OwnerSlot >= 0
                    ? WithAlpha(MatchPlayerColors.GetSlotColor(rect.OwnerSlot), 0.35f)
                    : s_centerFill;
                FillRect(painter, rect, fill);
            }

            painter.strokeColor = s_roadStroke;
            painter.lineWidth = 2f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

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

        void FillRect(Painter2D painter, MatchMinimapRect rect, Color fillColor)
        {
            var corners = GetRectCorners(rect);
            if (corners.Count < 4)
            {
                return;
            }

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

        List<Vector2> GetRectCorners(MatchMinimapRect rect)
        {
            var rad = rect.RotationDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            var hx = rect.HalfExtents.x;
            var hy = rect.HalfExtents.y;
            var localCorners = new[]
            {
                new Vector2(-hx, -hy),
                new Vector2(hx, -hy),
                new Vector2(hx, hy),
                new Vector2(-hx, hy),
            };

            var projected = new List<Vector2>(4);
            foreach (var local in localCorners)
            {
                var rotated = new Vector2(
                    local.x * cos - local.y * sin,
                    local.x * sin + local.y * cos);
                projected.Add(Project(rect.Center + rotated));
            }

            return projected;
        }

        Vector2 Project(Vector2 worldXZ)
        {
            var normalized = MatchMinimapProjection.WorldToNormalized(
                new Vector3(worldXZ.x, 0f, worldXZ.y),
                _arenaRadius);
            return MatchMinimapProjection.NormalizedToPanel(normalized, _panelWidth, _panelHeight);
        }

        static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);
    }
}
