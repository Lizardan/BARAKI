using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>Top-down schematic map previews for Mode Select tiles (matches live road topology).</summary>
    public static class ModeMapThumbnailBuilder
    {
        const float PreviewSize = 64f;
        const float Padding = 7f;
        const float HalfSize = MatchArenaGenerator.DefaultArenaRadius;

        /// <summary>Min drawn chord length in preview px (corner arcs scale to ~0.7px otherwise).</summary>
        const float MinSegmentPx = 2.4f;

        public static VisualElement BuildPreview(int playerCount)
        {
            var root = new VisualElement();
            root.AddToClassList("mm-mode__preview");
            root.pickingMode = PickingMode.Ignore;

            AddArena(root, playerCount == 4
                ? N4RoadReferenceSpec.CenterArenaHalfSize
                : N2RoadReferenceSpec.CenterArenaHalfSize);

            if (playerCount == 2)
            {
                BuildDuel(root);
            }
            else if (playerCount == 4)
            {
                BuildSquare(root);
            }
            else
            {
                BuildRing(root, Mathf.Clamp(playerCount, 3, 8));
            }

            return root;
        }

        public static Button BuildModeButton(int playerCount)
        {
            var button = new Button { name = $"Mode_N{playerCount}" };
            button.AddToClassList("mm-mode");
            button.Add(BuildPreview(playerCount));

            var label = new Label(playerCount.ToString());
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

        static void BuildDuel(VisualElement root)
        {
            AddWorldSegment(root, new Vector3(-HalfSize, 0f, 0f), new Vector3(HalfSize, 0f, 0f));
            AddWorldPolyline(root, DuelPathBuilder.SampleStadiumHalf(northSide: true, HalfSize));
            AddWorldPolyline(root, DuelPathBuilder.SampleStadiumHalf(northSide: false, HalfSize));
            AddBase(root, new Vector3(-HalfSize, 0f, 0f));
            AddBase(root, new Vector3(HalfSize, 0f, 0f));
        }

        static void BuildSquare(VisualElement root)
        {
            AddWorldPath(root, N4RoadCenterlineBuilder.BuildSharedFlankRing(HalfSize));

            var spokeEnd = N4RoadReferenceSpec.CenterArenaHalfSize;
            AddWorldSegment(root, new Vector3(0f, 0f, HalfSize), new Vector3(0f, 0f, spokeEnd));
            AddWorldSegment(root, new Vector3(HalfSize, 0f, 0f), new Vector3(spokeEnd, 0f, 0f));
            AddWorldSegment(root, new Vector3(0f, 0f, -HalfSize), new Vector3(0f, 0f, -spokeEnd));
            AddWorldSegment(root, new Vector3(-HalfSize, 0f, 0f), new Vector3(-spokeEnd, 0f, 0f));

            AddBase(root, new Vector3(0f, 0f, HalfSize));
            AddBase(root, new Vector3(HalfSize, 0f, 0f));
            AddBase(root, new Vector3(0f, 0f, -HalfSize));
            AddBase(root, new Vector3(-HalfSize, 0f, 0f));
        }

        static void BuildRing(VisualElement root, int playerCount)
        {
            const int ringSegments = 32;
            var ring = new List<Vector3>(ringSegments + 1);
            for (var i = 0; i <= ringSegments; i++)
            {
                var t = i / (float)ringSegments;
                var angle = t * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Cos(angle) * HalfSize, 0f, Mathf.Sin(angle) * HalfSize));
            }

            AddWorldPolyline(root, ring);

            var spokeInner = N2RoadReferenceSpec.CenterArenaHalfSize;
            for (var i = 0; i < playerCount; i++)
            {
                var angle = 2f * Mathf.PI * i / playerCount;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                AddWorldSegment(root, dir * HalfSize, dir * spokeInner);
                AddBase(root, dir * HalfSize);
            }
        }

        static void AddArena(VisualElement root, float halfExtent)
        {
            var arena = new VisualElement();
            arena.AddToClassList("mm-mode-arena");
            var size = WorldLengthToPreview(halfExtent * 2f);
            var center = PreviewCenter - size * 0.5f;
            arena.style.left = center;
            arena.style.top = center;
            arena.style.width = size;
            arena.style.height = size;
            root.Add(arena);
        }

        static void AddBase(VisualElement root, Vector3 world)
        {
            var p = WorldToPreview(world);
            var dot = new VisualElement();
            dot.AddToClassList("mm-mode-dot");
            dot.style.left = p.x - 4f;
            dot.style.top = p.y - 4f;
            root.Add(dot);
        }

        static void AddWorldPath(VisualElement root, LanePath path)
        {
            if (path == null || path.WaypointCount < 2)
            {
                return;
            }

            var points = new List<Vector3>(path.WaypointCount);
            for (var i = 0; i < path.WaypointCount; i++)
            {
                points.Add(path.GetWaypoint(i));
            }

            AddWorldPolyline(root, points);
        }

        static void AddWorldPolyline(VisualElement root, IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            // Merge micro-chords (corner arcs) so each VisualElement lane is visible at 64px.
            var cursor = WorldToPreview(points[0]);
            for (var i = 1; i < points.Count; i++)
            {
                var next = WorldToPreview(points[i]);
                var remaining = points.Count - 1 - i;
                if (Vector2.Distance(cursor, next) < MinSegmentPx && remaining > 0)
                {
                    continue;
                }

                AddPreviewSegment(root, cursor, next);
                cursor = next;
            }
        }

        static void AddWorldSegment(VisualElement root, Vector3 worldA, Vector3 worldB) =>
            AddPreviewSegment(root, WorldToPreview(worldA), WorldToPreview(worldB));

        static void AddPreviewSegment(VisualElement root, Vector2 a, Vector2 b)
        {
            var delta = b - a;
            var length = delta.magnitude;
            if (length < 0.35f)
            {
                return;
            }

            var lane = new VisualElement();
            lane.AddToClassList("mm-mode-lane");
            lane.style.left = (a.x + b.x) * 0.5f - length * 0.5f;
            lane.style.top = (a.y + b.y) * 0.5f - 1f;
            lane.style.width = length;
            lane.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg)));
            root.Add(lane);
        }

        static float PreviewCenter => PreviewSize * 0.5f;

        static float WorldScale => (PreviewSize - Padding * 2f) / (HalfSize * 2f);

        static float WorldLengthToPreview(float worldLength) => worldLength * WorldScale;

        static Vector2 WorldToPreview(Vector3 world) =>
            new(
                PreviewCenter + world.x * WorldScale,
                PreviewCenter - world.z * WorldScale);
    }
}
