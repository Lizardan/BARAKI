using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class MatchMinimapViewportElement : VisualElement
    {
        static readonly Color s_stroke = new(0.92f, 0.94f, 0.98f, 0.9f);

        readonly List<Vector2> _polygon = new(8);

        public MatchMinimapViewportElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void ClearPolygon()
        {
            if (_polygon.Count == 0)
            {
                return;
            }

            _polygon.Clear();
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Closed frustum polygon already clipped to the map square.
        /// Variable vertex count: a quad becomes 3..8 verts at the map edge.
        /// </summary>
        public void SetPolygon(IReadOnlyList<Vector2> vertices)
        {
            _polygon.Clear();
            if (vertices == null || vertices.Count < 3)
            {
                MarkDirtyRepaint();
                return;
            }

            for (var i = 0; i < vertices.Count; i++)
            {
                _polygon.Add(vertices[i]);
            }

            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (_polygon.Count < 3)
            {
                return;
            }

            var painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            painter.strokeColor = s_stroke;
            painter.lineWidth = 1.5f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(_polygon[0]);
            for (var i = 1; i < _polygon.Count; i++)
            {
                painter.LineTo(_polygon[i]);
            }

            painter.ClosePath();
            painter.Stroke();
        }
    }
}
