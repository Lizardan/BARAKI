using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Mode Select map thumbnails from live road centerlines, stroked with Painter2D for crisp AA.
    /// </summary>
    public static class ModeMapThumbnailBuilder
    {
        public const float PreviewSize = 64f;

        const float EdgeMargin = 5f;
        const float DotSize = 8f;
        const float LaneThickness = 2f;
        const float HalfSize = MatchArenaGenerator.DefaultArenaRadius;

        /// <summary>
        /// World-space tolerance for collapsing straight samples.
        /// Must stay below corner-arc sagitta (~0.21 at R=25 / 15°) so fillets survive.
        /// </summary>
        const float CollinearMaxDistWorld = 0.12f;

        static readonly float DuelPreviewYaw = 0f;
        static readonly float RingPreviewYaw = 0f;

        public static VisualElement BuildPreview(int playerCount)
        {
            var n = Mathf.Clamp(playerCount, 2, 8);
            if (n == 2)
            {
                return BuildDuel();
            }

            if (n == 4)
            {
                return BuildSquare();
            }

            return BuildRing(n);
        }

        public static Button BuildModeButton(int playerCount)
        {
            var button = new Button { name = $"Mode_N{playerCount}" };
            button.AddToClassList("mm-mode");
            button.style.flexShrink = 0;
            button.Add(BuildPreview(playerCount));

            var label = new Label(MatchModeRules.GetModeTitle(playerCount));
            label.AddToClassList("mm-mode__label");
            button.Add(label);

            var selectable = MatchModeRules.IsModeSelectable(playerCount);
            button.SetEnabled(selectable);
            if (!selectable)
            {
                button.AddToClassList("mm-mode--disabled");
            }

            return button;
        }

        static ModeMapThumbnailElement BuildDuel()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var polylines = new List<IReadOnlyList<Vector3>>
            {
                DuelPathBuilder.SampleStadiumHalf(northSide: true, HalfSize),
                DuelPathBuilder.SampleStadiumHalf(northSide: false, HalfSize),
                new[]
                {
                    MatchArenaGenerator.RotateAuthoredToLayout(new Vector3(-HalfSize, 0f, 0f)),
                    MatchArenaGenerator.RotateAuthoredToLayout(new Vector3(HalfSize, 0f, 0f)),
                },
            };
            var bases = new[]
            {
                layout.Slots[0].BasePosition,
                layout.Slots[1].BasePosition,
            };
            return CreateThumbnail(polylines, bases, DuelPreviewYaw, N2RoadReferenceSpec.CenterArenaHalfSize);
        }

        static ModeMapThumbnailElement BuildSquare()
        {
            var ring = N4RoadCenterlineBuilder.BuildSharedFlankRing(HalfSize);
            var polylines = new List<IReadOnlyList<Vector3>> { PathToList(ring) };

            var spokeEnd = N4RoadReferenceSpec.CenterArenaHalfSize;
            polylines.Add(new[] { new Vector3(0f, 0f, HalfSize), new Vector3(0f, 0f, spokeEnd) });
            polylines.Add(new[] { new Vector3(HalfSize, 0f, 0f), new Vector3(spokeEnd, 0f, 0f) });
            polylines.Add(new[] { new Vector3(0f, 0f, -HalfSize), new Vector3(0f, 0f, -spokeEnd) });
            polylines.Add(new[] { new Vector3(-HalfSize, 0f, 0f), new Vector3(-spokeEnd, 0f, 0f) });

            var layout = MatchArenaGenerator.Generate(4);
            var bases = new Vector3[4];
            for (var i = 0; i < 4; i++)
            {
                bases[i] = layout.Slots[i].BasePosition;
            }

            return CreateThumbnail(polylines, bases, yaw: 0f, N4RoadReferenceSpec.CenterArenaHalfSize);
        }

        static ModeMapThumbnailElement BuildRing(int playerCount)
        {
            var ringPath = PerimeterRingPathBuilder.BuildSharedFlankRing(HalfSize, playerCount);
            var polylines = new List<IReadOnlyList<Vector3>> { PathToList(ringPath) };

            var spokeInner = N4RoadReferenceSpec.CenterArenaHalfSize;
            var exitLen = CircularRingRoadGeometry.ExitStraightLength;
            var bases = new Vector3[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                var angle = MatchArenaGenerator.FirstPlayerAngleRadians + 2f * Mathf.PI * i / playerCount;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = new Vector3(-radial.z, 0f, radial.x);
                bases[i] = radial * HalfSize;
                polylines.Add(new[] { bases[i], radial * spokeInner });
                // Short L/R exits before the ring curve.
                polylines.Add(new[] { bases[i], bases[i] + tangent * exitLen });
                polylines.Add(new[] { bases[i], bases[i] - tangent * exitLen });
            }

            return CreateThumbnail(polylines, bases, RingPreviewYaw, N4RoadReferenceSpec.CenterArenaHalfSize);
        }

        static ModeMapThumbnailElement CreateThumbnail(
            List<IReadOnlyList<Vector3>> worldPolylines,
            IReadOnlyList<Vector3> worldBases,
            float yaw,
            float arenaHalfWorld)
        {
            var fit = ComputeFit(worldPolylines, worldBases, yaw);
            var previewPolys = new List<IReadOnlyList<Vector2>>(worldPolylines.Count);
            foreach (var poly in worldPolylines)
            {
                var preview = ToPreviewPolyline(poly, fit);
                if (preview.Count >= 2)
                {
                    previewPolys.Add(preview);
                }
            }

            var previewBases = new List<Vector2>(worldBases.Count);
            for (var i = 0; i < worldBases.Count; i++)
            {
                previewBases.Add(fit.ToPreview(worldBases[i]));
            }

            // Fit the full visual (roads + stroke + base dots) with equal padding.
            var visualScale = FitVisualInSquare(
                previewPolys,
                previewBases,
                DotSize * 0.5f,
                LaneThickness * 0.5f,
                EdgeMargin);
            var arenaHalf = Mathf.Max(4f, arenaHalfWorld * fit.Scale * visualScale);
            var element = new ModeMapThumbnailElement();
            element.SetGeometry(previewPolys, previewBases, arenaHalf, DotSize * 0.5f, LaneThickness);
            return element;
        }

        /// <summary>
        /// Scale + translate so roads and base dots share equal margin to all four sides.
        /// </summary>
        static float FitVisualInSquare(
            List<IReadOnlyList<Vector2>> polylines,
            List<Vector2> bases,
            float dotHalf,
            float strokePad,
            float margin)
        {
            if (!TryGetVisualBounds(polylines, bases, dotHalf, strokePad, out var minX, out var maxX, out var minY, out var maxY))
            {
                return 1f;
            }

            var width = Mathf.Max(1f, maxX - minX);
            var height = Mathf.Max(1f, maxY - minY);
            var target = PreviewSize - margin * 2f;
            var scale = Mathf.Min(target / width, target / height);
            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var previewCenter = new Vector2(PreviewSize * 0.5f, PreviewSize * 0.5f);

            for (var i = 0; i < polylines.Count; i++)
            {
                var src = polylines[i];
                var shifted = new List<Vector2>(src.Count);
                for (var p = 0; p < src.Count; p++)
                {
                    shifted.Add(previewCenter + (src[p] - center) * scale);
                }

                polylines[i] = shifted;
            }

            for (var i = 0; i < bases.Count; i++)
            {
                bases[i] = previewCenter + (bases[i] - center) * scale;
            }

            return scale;
        }

        static bool TryGetVisualBounds(
            List<IReadOnlyList<Vector2>> polylines,
            List<Vector2> bases,
            float dotHalf,
            float strokePad,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            var localMinX = float.PositiveInfinity;
            var localMaxX = float.NegativeInfinity;
            var localMinY = float.PositiveInfinity;
            var localMaxY = float.NegativeInfinity;

            for (var i = 0; i < polylines.Count; i++)
            {
                var poly = polylines[i];
                for (var p = 0; p < poly.Count; p++)
                {
                    var pt = poly[p];
                    localMinX = Mathf.Min(localMinX, pt.x - strokePad);
                    localMaxX = Mathf.Max(localMaxX, pt.x + strokePad);
                    localMinY = Mathf.Min(localMinY, pt.y - strokePad);
                    localMaxY = Mathf.Max(localMaxY, pt.y + strokePad);
                }
            }

            for (var i = 0; i < bases.Count; i++)
            {
                var b = bases[i];
                localMinX = Mathf.Min(localMinX, b.x - dotHalf);
                localMaxX = Mathf.Max(localMaxX, b.x + dotHalf);
                localMinY = Mathf.Min(localMinY, b.y - dotHalf);
                localMaxY = Mathf.Max(localMaxY, b.y + dotHalf);
            }

            minX = localMinX;
            maxX = localMaxX;
            minY = localMinY;
            maxY = localMaxY;
            return !float.IsInfinity(localMinX);
        }

        static List<Vector2> ToPreviewPolyline(IReadOnlyList<Vector3> worldPoints, FitTransform fit)
        {
            if (worldPoints == null || worldPoints.Count < 2)
            {
                return new List<Vector2>();
            }

            var flat = new List<Vector2>(worldPoints.Count);
            for (var i = 0; i < worldPoints.Count; i++)
            {
                var p = RotateYaw(worldPoints[i], fit.Yaw);
                flat.Add(new Vector2(p.x, p.z));
            }

            // Collapse exact straights; keep full arc samples for smooth Painter2D strokes.
            flat = MergeCollinear(flat, CollinearMaxDistWorld);

            var preview = new List<Vector2>(flat.Count);
            for (var i = 0; i < flat.Count; i++)
            {
                preview.Add(fit.ToPreviewXZ(flat[i]));
            }

            return preview;
        }

        static FitTransform ComputeFit(
            List<IReadOnlyList<Vector3>> polylines,
            IReadOnlyList<Vector3> bases,
            float yaw)
        {
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;

            void Encapsulate(Vector3 world)
            {
                var p = RotateYaw(world, yaw);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }

            foreach (var poly in polylines)
            {
                if (poly == null)
                {
                    continue;
                }

                for (var i = 0; i < poly.Count; i++)
                {
                    Encapsulate(poly[i]);
                }
            }

            for (var i = 0; i < bases.Count; i++)
            {
                Encapsulate(bases[i]);
            }

            var width = Mathf.Max(1f, maxX - minX);
            var height = Mathf.Max(1f, maxZ - minZ);
            var extent = Mathf.Max(width, height);
            var usable = PreviewSize - EdgeMargin * 2f - DotSize;
            var scale = usable / extent;
            var centerX = (minX + maxX) * 0.5f;
            var centerZ = (minZ + maxZ) * 0.5f;

            return new FitTransform(yaw, scale, centerX, centerZ);
        }

        static List<Vector2> MergeCollinear(List<Vector2> points, float maxDist)
        {
            if (points.Count < 3)
            {
                return points;
            }

            var result = new List<Vector2>(points.Count) { points[0] };
            for (var i = 1; i < points.Count - 1; i++)
            {
                var a = result[^1];
                var b = points[i];
                var c = points[i + 1];
                if (IsCollinear(a, b, c, maxDist))
                {
                    continue;
                }

                result.Add(b);
            }

            result.Add(points[^1]);
            return result;
        }

        static bool IsCollinear(Vector2 a, Vector2 b, Vector2 c, float maxDist)
        {
            var ac = c - a;
            var lenSq = ac.sqrMagnitude;
            if (lenSq < 1e-6f)
            {
                return true;
            }

            var t = Vector2.Dot(b - a, ac) / lenSq;
            var proj = a + ac * Mathf.Clamp01(t);
            return Vector2.Distance(b, proj) <= maxDist;
        }

        static List<Vector3> PathToList(LanePath path)
        {
            var points = new List<Vector3>(path.WaypointCount);
            for (var i = 0; i < path.WaypointCount; i++)
            {
                points.Add(path.GetWaypoint(i));
            }

            return points;
        }

        static Vector3 RotateYaw(Vector3 world, float yaw)
        {
            if (Mathf.Abs(yaw) < 1e-6f)
            {
                return world;
            }

            var cos = Mathf.Cos(yaw);
            var sin = Mathf.Sin(yaw);
            return new Vector3(
                world.x * cos - world.z * sin,
                world.y,
                world.x * sin + world.z * cos);
        }

        readonly struct FitTransform
        {
            public readonly float Yaw;
            public readonly float Scale;
            readonly float _originX;
            readonly float _originZ;

            public FitTransform(float yaw, float scale, float originX, float originZ)
            {
                Yaw = yaw;
                Scale = scale;
                _originX = originX;
                _originZ = originZ;
            }

            public Vector2 ToPreview(Vector3 world)
            {
                var p = RotateYaw(world, Yaw);
                return ToPreviewXZ(new Vector2(p.x, p.z));
            }

            public Vector2 ToPreviewXZ(Vector2 xz) =>
                new(
                    PreviewSize * 0.5f + (xz.x - _originX) * Scale,
                    PreviewSize * 0.5f - (xz.y - _originZ) * Scale);
        }
    }
}
