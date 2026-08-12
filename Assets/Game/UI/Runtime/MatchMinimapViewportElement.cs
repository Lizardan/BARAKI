using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class MatchMinimapViewportElement : VisualElement
    {
        static readonly Color s_stroke = new(0.92f, 0.94f, 0.98f, 0.9f);

        readonly Vector2[] _corners = new Vector2[4];
        bool _hasCorners;

        public MatchMinimapViewportElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void ClearCorners()
        {
            if (!_hasCorners)
            {
                return;
            }

            _hasCorners = false;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Draws the exact 4 ground hits as a trapezoid. Do not AABB these points —
        /// that re-inflates left/right past the real view.
        /// </summary>
        public void SetCorners(Vector2 c0, Vector2 c1, Vector2 c2, Vector2 c3)
        {
            _corners[0] = c0;
            _corners[1] = c1;
            _corners[2] = c2;
            _corners[3] = c3;
            _hasCorners = true;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!_hasCorners)
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
            painter.MoveTo(_corners[0]);
            painter.LineTo(_corners[1]);
            painter.LineTo(_corners[2]);
            painter.LineTo(_corners[3]);
            painter.ClosePath();
            painter.Stroke();
        }
    }
}
