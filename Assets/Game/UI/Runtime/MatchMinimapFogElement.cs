using Game.Gameplay.Match.Fog;
using Game.Gameplay.Match.Selection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// Soft fog overlay on the minimap, sampling the same density field as world FoW.
    /// </summary>
    public sealed class MatchMinimapFogElement : VisualElement
    {
        Texture _overlay;
        float _fogAreaSize = 280f;
        float _mapHalfExtent = 142f;
        float _panelWidth = 350f;
        float _panelHeight = 350f;
        float _viewYawDegrees;
        bool _visible;

        public MatchMinimapFogElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void ClearFog()
        {
            if (!_visible && _overlay == null)
            {
                return;
            }

            _visible = false;
            _overlay = null;
            MarkDirtyRepaint();
        }

        public void SetFogData(
            Texture overlay,
            float fogAreaSize,
            float arenaRadius,
            float panelWidth,
            float panelHeight,
            float viewYawDegrees)
        {
            _overlay = overlay;
            _fogAreaSize = Mathf.Max(1f, fogAreaSize);
            _mapHalfExtent = MatchMinimapProjection.MapHalfExtent(arenaRadius);
            _panelWidth = Mathf.Max(1f, panelWidth);
            _panelHeight = Mathf.Max(1f, panelHeight);
            _viewYawDegrees = viewYawDegrees;
            _visible = overlay != null;
            // Density RT contents change every simulation tick.
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!_visible || _overlay == null)
            {
                return;
            }

            var mesh = context.Allocate(4, 6, _overlay);
            var tint = Color.white;

            // Map square corners (normalized 0..1) → panel → world → fog UV.
            WriteCorner(mesh, new Vector2(0f, 0f), tint);
            WriteCorner(mesh, new Vector2(1f, 0f), tint);
            WriteCorner(mesh, new Vector2(1f, 1f), tint);
            WriteCorner(mesh, new Vector2(0f, 1f), tint);

            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(3);
        }

        void WriteCorner(MeshWriteData mesh, Vector2 normalized, Color tint)
        {
            var panel = MatchMinimapProjection.NormalizedToPanel(normalized, _panelWidth, _panelHeight);
            var world = MatchMinimapProjection.NormalizedToWorld(
                normalized,
                _mapHalfExtent,
                _viewYawDegrees);
            var fogUv = FogSimulation.WorldToUv(world, _fogAreaSize);

            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(panel.x, panel.y, Vertex.nearZ),
                tint = tint,
                uv = fogUv,
            });
        }
    }
}
