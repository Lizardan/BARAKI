using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match.Fog
{
    /// <summary>Builds always-clear fog zones for a local player slot from arena layout + lane graph.</summary>
    public static class FogPermanentMaskBaker
    {
        public const float BasePadPadding = 4f;
        public const float TurnAngleDegrees = 28f;
        public const float MinSegmentLength = 1.5f;

        public static FogPermanentZones Bake(
            MatchArenaLayout layout,
            LaneGraph graph,
            int localPlayerSlot,
            float centerArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius,
            float roadWidth = MatchArenaGreyboxBuilder.RoadWidth)
        {
            var zones = new FogPermanentZones
            {
                RoadHalfWidth = roadWidth * 0.5f,
            };

            if (layout?.Slots == null
                || localPlayerSlot < 0
                || localPlayerSlot >= layout.Slots.Count)
            {
                return zones;
            }

            var slot = layout.Slots[localPlayerSlot];
            zones.BasePad = CreateBasePad(slot, BasePadPadding);

            if (graph == null)
            {
                return zones;
            }

            if (graph.TryGetLane(localPlayerSlot, GameIds.Lanes.Center, out var centerLane))
            {
                var centerPoly = TruncateCenterToArenaEntry(centerLane.Path, centerArenaRadius);
                zones.AddRoadPolyline(centerPoly);
            }

            if (graph.TryGetLane(localPlayerSlot, GameIds.Lanes.Left, out var leftLane))
            {
                zones.AddRoadPolyline(TruncateFlankToFirstTurn(leftLane.Path));
            }

            if (graph.TryGetLane(localPlayerSlot, GameIds.Lanes.Right, out var rightLane))
            {
                zones.AddRoadPolyline(TruncateFlankToFirstTurn(rightLane.Path));
            }

            return zones;
        }

        public static FogOrientedRect CreateBasePad(PlayerSlotLayout slot, float padding)
        {
            var halfWidth = MatchArenaGreyboxBuilder.BaseArenaWidth * 0.5f + padding;
            var halfDepth = MatchArenaGreyboxBuilder.BaseArenaDepth * 0.5f + padding;
            var localCenter = new Vector3(0f, 0f, -MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
            var worldCenter = slot.BasePosition + slot.BaseRotation * localCenter;
            worldCenter.y = 0f;

            return new FogOrientedRect
            {
                Center = worldCenter,
                Rotation = slot.BaseRotation,
                HalfExtents = new Vector2(halfWidth, halfDepth),
            };
        }

        /// <summary>Center barracks → center-arena entry (exclusive of the arena disc interior).</summary>
        public static List<Vector3> TruncateCenterToArenaEntry(LanePath path, float centerArenaRadius)
        {
            var result = new List<Vector3>();
            if (path == null || path.WaypointCount < 2)
            {
                return result;
            }

            var radius = Mathf.Max(0.01f, centerArenaRadius);
            var radiusSq = radius * radius;
            result.Add(Flat(path.GetWaypoint(0)));

            for (var i = 0; i < path.WaypointCount - 1; i++)
            {
                var a = Flat(path.GetWaypoint(i));
                var b = Flat(path.GetWaypoint(i + 1));
                var aInside = a.sqrMagnitude <= radiusSq;
                var bInside = b.sqrMagnitude <= radiusSq;

                if (aInside)
                {
                    break;
                }

                if (bInside)
                {
                    if (TrySegmentCircleEntry(a, b, radius, out var entry))
                    {
                        result.Add(entry);
                    }

                    break;
                }

                result.Add(b);
            }

            if (result.Count < 2 && path.WaypointCount >= 2)
            {
                result.Clear();
                result.Add(Flat(path.GetWaypoint(0)));
                result.Add(Flat(path.GetWaypoint(1)));
            }

            return result;
        }

        /// <summary>Flank from barracks until the first significant turn (corner / stadium arc).</summary>
        public static List<Vector3> TruncateFlankToFirstTurn(LanePath path)
        {
            var result = new List<Vector3>();
            if (path == null || path.WaypointCount < 2)
            {
                return result;
            }

            // Closed flank loops: only walk outward until first turn, never full ring.
            var count = path.WaypointCount;
            if (path.IsClosedLoop && count >= 2)
            {
                var first = Flat(path.GetWaypoint(0));
                var last = Flat(path.GetWaypoint(count - 1));
                if ((first - last).sqrMagnitude <= 0.01f)
                {
                    count--;
                }
            }

            result.Add(Flat(path.GetWaypoint(0)));
            Vector3 outboundDir = default;
            var hasOutbound = false;
            // Hard cap so a smooth arc never paints the whole perimeter as "permanent".
            var maxLength = MatchArenaGenerator.DefaultArenaRadius * 0.75f;
            var traveled = 0f;

            for (var i = 0; i < count - 1; i++)
            {
                var a = Flat(path.GetWaypoint(i));
                var b = Flat(path.GetWaypoint(i + 1));
                var delta = b - a;
                var length = delta.magnitude;
                if (length < 0.05f)
                {
                    continue;
                }

                var dir = delta / length;
                if (!hasOutbound && length >= MinSegmentLength * 0.5f)
                {
                    outboundDir = dir;
                    hasOutbound = true;
                }

                if (hasOutbound && Vector3.Angle(outboundDir, dir) >= TurnAngleDegrees)
                {
                    break;
                }

                if (traveled + length > maxLength)
                {
                    var remain = maxLength - traveled;
                    if (remain > 0.05f)
                    {
                        result.Add(a + dir * remain);
                    }

                    break;
                }

                result.Add(b);
                traveled += length;
            }

            if (result.Count < 2)
            {
                result.Clear();
                result.Add(Flat(path.GetWaypoint(0)));
                result.Add(Flat(path.GetWaypoint(1)));
            }

            return result;
        }

        static bool TrySegmentCircleEntry(Vector3 start, Vector3 end, float radius, out Vector3 entry)
        {
            entry = default;
            if (!LaneGraphBuilder.TryGetSegmentCircleIntersections(
                    start,
                    end,
                    radius,
                    out var tEnter,
                    out _))
            {
                return false;
            }

            if (tEnter < 0f || tEnter > 1f)
            {
                return false;
            }

            entry = Vector3.Lerp(start, end, tEnter);
            entry.y = 0f;
            return true;
        }

        static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
