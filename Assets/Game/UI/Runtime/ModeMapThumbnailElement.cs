using System;
using System.Collections.Generic;
using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// Mode Select map schematic using the same topology painter as the in-match minimap.
    /// </summary>
    public sealed class ModeMapThumbnailElement : VisualElement
    {
        public const string SelectedEdgeClass = "mm-cam-edge--selected";
        public const float AuthoredSize = 64f;
        const string HomeMarkerClass = "mm-cam-home";

        static readonly CameraBaseScreenEdge[] s_edges =
        {
            CameraBaseScreenEdge.Top,
            CameraBaseScreenEdge.Right,
            CameraBaseScreenEdge.Bottom,
            CameraBaseScreenEdge.Left,
        };

        float _panelSize;
        readonly bool _fillHost;
        readonly MatchMinimapGeometryElement _geometry;
        readonly VisualElement _homeMarker;
        readonly Dictionary<CameraBaseScreenEdge, Button> _edgeButtons = new();
        readonly List<Vector2> _baseCenters = new();
        MatchMinimapTopology _topology;
        float _arenaRadius = MatchArenaGenerator.DefaultArenaRadius;
        float _arenaHalfPx;
        float _viewYawDegrees;
        Vector3 _homeWorld;
        CameraBaseScreenEdge _preferredEdge = CameraBaseScreenEdge.Bottom;

        public ModeMapThumbnailElement()
            : this(AuthoredSize)
        {
        }

        public ModeMapThumbnailElement(float panelSize)
            : this(panelSize, fillHost: false)
        {
        }

        public ModeMapThumbnailElement(float panelSize, bool fillHost)
        {
            _panelSize = Mathf.Max(1f, panelSize);
            _fillHost = fillHost;
            AddToClassList("mm-mode__preview");
            pickingMode = fillHost ? PickingMode.Position : PickingMode.Ignore;
            style.overflow = Overflow.Hidden;
            if (fillHost)
            {
                AddToClassList("mm-mode-dossier__map");
            }
            else
            {
                style.width = _panelSize;
                style.height = _panelSize;
                style.minWidth = _panelSize;
                style.minHeight = _panelSize;
                style.flexShrink = 0;
            }

            _geometry = new MatchMinimapGeometryElement();
            _geometry.pickingMode = PickingMode.Ignore;
            _geometry.style.position = Position.Absolute;
            _geometry.style.left = 0f;
            _geometry.style.top = 0f;
            _geometry.style.right = 0f;
            _geometry.style.bottom = 0f;
            Add(_geometry);

            _homeMarker = new VisualElement();
            _homeMarker.AddToClassList(HomeMarkerClass);
            _homeMarker.pickingMode = PickingMode.Ignore;
            _homeMarker.style.display = fillHost ? DisplayStyle.Flex : DisplayStyle.None;
            Add(_homeMarker);

            if (fillHost)
            {
                AddEdgeButton(CameraBaseScreenEdge.Top, "v", "mm-cam-edge--top", "База сверху экрана");
                AddEdgeButton(CameraBaseScreenEdge.Right, "<", "mm-cam-edge--right", "База справа экрана");
                AddEdgeButton(CameraBaseScreenEdge.Bottom, "^", "mm-cam-edge--bottom", "База снизу экрана");
                AddEdgeButton(CameraBaseScreenEdge.Left, ">", "mm-cam-edge--left", "База слева экрана");
            }

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public event Action<CameraBaseScreenEdge> EdgeClicked;

        public float PanelSize => _panelSize;

        public float ViewYawDegrees => _viewYawDegrees;

        public CameraBaseScreenEdge PreferredEdge => _preferredEdge;

        public IReadOnlyList<Vector2> BaseCenters => _baseCenters;

        public float ArenaHalfPx => _arenaHalfPx;

        public int RoadSegmentCount => _topology?.RoadSegments.Count ?? 0;

        /// <summary>Alias kept for older tests: one segment ≈ one road stroke sample pair.</summary>
        public int PolylineCount => RoadSegmentCount;

        public int StrokePointCount => RoadSegmentCount * 2;

        public Vector2 GetVisualBoundsCenter(float unusedDotHalf = 4f)
        {
            _ = unusedDotHalf;
            if (_baseCenters.Count == 0)
            {
                return new Vector2(_panelSize * 0.5f, _panelSize * 0.5f);
            }

            var min = _baseCenters[0];
            var max = _baseCenters[0];
            for (var i = 1; i < _baseCenters.Count; i++)
            {
                min = Vector2.Min(min, _baseCenters[i]);
                max = Vector2.Max(max, _baseCenters[i]);
            }

            if (_topology != null)
            {
                foreach (var segment in _topology.RoadSegments)
                {
                    var a = Project(segment.A);
                    var b = Project(segment.B);
                    min = Vector2.Min(min, Vector2.Min(a, b));
                    max = Vector2.Max(max, Vector2.Max(a, b));
                }
            }

            return (min + max) * 0.5f;
        }

        public void SetTopology(MatchMinimapTopology topology, float arenaRadius) =>
            SetTopology(topology, arenaRadius, viewYawDegrees: 0f, homeWorld: Vector3.zero);

        public void SetTopology(
            MatchMinimapTopology topology,
            float arenaRadius,
            float viewYawDegrees,
            Vector3 homeWorld)
        {
            _topology = topology;
            _arenaRadius = Mathf.Max(1f, arenaRadius);
            _viewYawDegrees = viewYawDegrees;
            _homeWorld = homeWorld;
            RefreshDraw();
        }

        public void ApplyCameraEdge(CameraBaseScreenEdge edge)
        {
            _preferredEdge = GameplayCameraSettings.ClampScreenEdge(edge);
            _viewYawDegrees = GameplayCameraSettings.ComputeYawDegreesForBaseAtScreenEdge(
                _homeWorld,
                Vector3.zero,
                _preferredEdge);
            RefreshEdgeButtons();
            RefreshDraw();
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var size = Mathf.Floor(Mathf.Min(evt.newRect.width, evt.newRect.height));
            if (size < 8f)
            {
                return;
            }

            if (Mathf.Abs(size - _panelSize) < 0.5f)
            {
                MarkDirtyRepaint();
                RefreshHomeMarker();
                return;
            }

            _panelSize = size;
            RefreshDraw();
        }

        void RefreshDraw()
        {
            RebuildDerived();
            _geometry.SetDrawData(
                _topology,
                _arenaRadius,
                _panelSize,
                _panelSize,
                _viewYawDegrees,
                drawGround: false,
                drawFilledRects: false);
            RefreshHomeMarker();
        }

        void RebuildDerived()
        {
            _baseCenters.Clear();
            _arenaHalfPx = 0f;
            if (_topology == null)
            {
                return;
            }

            var mapHalf = MatchMinimapProjection.MapHalfExtent(_arenaRadius);
            var center = Project(Vector2.zero, mapHalf);
            _arenaHalfPx = Vector2.Distance(
                center,
                Project(new Vector2(_topology.CenterArenaRadius, 0f), mapHalf));

            foreach (var rect in _topology.FilledRects)
            {
                if (rect.OwnerSlot < 0)
                {
                    continue;
                }

                _baseCenters.Add(Project(rect.Center, mapHalf));
            }
        }

        void RefreshHomeMarker()
        {
            if (_homeMarker == null || !_fillHost)
            {
                return;
            }

            var home = Project(new Vector2(_homeWorld.x, _homeWorld.z));
            _homeMarker.style.left = home.x - 5f;
            _homeMarker.style.top = home.y - 5f;
        }

        void AddEdgeButton(
            CameraBaseScreenEdge edge,
            string glyph,
            string className,
            string tooltip)
        {
            var button = new Button { name = $"CameraEdge{edge}Button", text = glyph, tooltip = tooltip };
            button.AddToClassList("mm-cam-edge");
            button.AddToClassList(className);
            button.clicked += () => EdgeClicked?.Invoke(edge);
            _edgeButtons[edge] = button;
            Add(button);
        }

        void RefreshEdgeButtons()
        {
            foreach (var edge in s_edges)
            {
                if (_edgeButtons.TryGetValue(edge, out var button))
                {
                    button.EnableInClassList(SelectedEdgeClass, edge == _preferredEdge);
                }
            }
        }

        Vector2 Project(Vector2 worldXZ) =>
            Project(worldXZ, MatchMinimapProjection.MapHalfExtent(_arenaRadius));

        Vector2 Project(Vector2 worldXZ, float mapHalfExtent)
        {
            var normalized = MatchMinimapProjection.WorldToNormalizedUnclamped(
                new Vector3(worldXZ.x, 0f, worldXZ.y),
                mapHalfExtent,
                _viewYawDegrees);
            return MatchMinimapProjection.NormalizedToPanel(normalized, _panelSize, _panelSize);
        }
    }
}
