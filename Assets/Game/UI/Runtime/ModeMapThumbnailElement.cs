using System.Collections.Generic;
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
        public const float AuthoredSize = 64f;

        readonly MatchMinimapGeometryElement _geometry;
        readonly List<Vector2> _baseCenters = new();
        MatchMinimapTopology _topology;
        float _arenaRadius = MatchArenaGenerator.DefaultArenaRadius;
        float _arenaHalfPx;

        public ModeMapThumbnailElement()
        {
            AddToClassList("mm-mode__preview");
            pickingMode = PickingMode.Ignore;
            style.width = AuthoredSize;
            style.height = AuthoredSize;
            style.minWidth = AuthoredSize;
            style.minHeight = AuthoredSize;
            style.flexShrink = 0;
            style.overflow = Overflow.Hidden;

            _geometry = new MatchMinimapGeometryElement();
            _geometry.pickingMode = PickingMode.Ignore;
            _geometry.style.position = Position.Absolute;
            _geometry.style.left = 0f;
            _geometry.style.top = 0f;
            _geometry.style.right = 0f;
            _geometry.style.bottom = 0f;
            Add(_geometry);
        }

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
                return new Vector2(AuthoredSize * 0.5f, AuthoredSize * 0.5f);
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

        public void SetTopology(MatchMinimapTopology topology, float arenaRadius)
        {
            _topology = topology;
            _arenaRadius = Mathf.Max(1f, arenaRadius);
            RebuildDerived();
            _geometry.SetDrawData(
                _topology,
                _arenaRadius,
                AuthoredSize,
                AuthoredSize,
                viewYawDegrees: 0f,
                drawGround: false,
                drawFilledRects: false);
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

        Vector2 Project(Vector2 worldXZ) =>
            Project(worldXZ, MatchMinimapProjection.MapHalfExtent(_arenaRadius));

        static Vector2 Project(Vector2 worldXZ, float mapHalfExtent)
        {
            var normalized = MatchMinimapProjection.WorldToNormalizedUnclamped(
                new Vector3(worldXZ.x, 0f, worldXZ.y),
                mapHalfExtent);
            return MatchMinimapProjection.NormalizedToPanel(normalized, AuthoredSize, AuthoredSize);
        }
    }
}
