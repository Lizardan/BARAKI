using System;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Immutable XZ walkable area baked from <c>_SourceParts</c> (roads, arenas, arcs).
    /// </summary>
    public sealed class WalkableSurface
    {
        readonly WalkablePart[] _parts;

        public WalkableSurface(WalkablePart[] parts)
        {
            _parts = parts ?? throw new ArgumentNullException(nameof(parts));
        }

        public int PartCount => _parts.Length;

        public bool Contains(Vector3 worldPosition)
        {
            var point = new Vector2(worldPosition.x, worldPosition.z);
            for (var i = 0; i < _parts.Length; i++)
            {
                if (_parts[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Keeps Y from input; clamps XZ onto the nearest walkable point if outside.</summary>
        public Vector3 Clamp(Vector3 worldPosition)
        {
            if (Contains(worldPosition))
            {
                return worldPosition;
            }

            var point = new Vector2(worldPosition.x, worldPosition.z);
            var best = point;
            var bestDistSq = float.MaxValue;
            for (var i = 0; i < _parts.Length; i++)
            {
                if (!_parts[i].TryClosestPoint(point, out var candidate, out var distSq))
                {
                    continue;
                }

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = candidate;
                }
            }

            return new Vector3(best.x, worldPosition.y, best.y);
        }
    }

    /// <summary>One SourceParts mesh projected to XZ triangles with an AABB.</summary>
    public readonly struct WalkablePart
    {
        public WalkablePart(Vector2 min, Vector2 max, Vector2[] triangleVertices)
        {
            Min = min;
            Max = max;
            TriangleVertices = triangleVertices ?? Array.Empty<Vector2>();
        }

        public Vector2 Min { get; }
        public Vector2 Max { get; }
        /// <summary>Flat list: 3 vertices per triangle (A,B,C,A,B,C,...).</summary>
        public Vector2[] TriangleVertices { get; }

        public bool Contains(Vector2 point)
        {
            if (point.x < Min.x || point.x > Max.x || point.y < Min.y || point.y > Max.y)
            {
                return false;
            }

            var verts = TriangleVertices;
            for (var i = 0; i + 2 < verts.Length; i += 3)
            {
                if (WalkableSurfaceRules.PointInTriangle(point, verts[i], verts[i + 1], verts[i + 2]))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryClosestPoint(Vector2 point, out Vector2 closest, out float distSq)
        {
            closest = default;
            distSq = float.MaxValue;
            var verts = TriangleVertices;
            if (verts.Length < 3)
            {
                return false;
            }

            for (var i = 0; i + 2 < verts.Length; i += 3)
            {
                var a = verts[i];
                var b = verts[i + 1];
                var c = verts[i + 2];
                WalkableSurfaceRules.ClosestPointOnTriangle(point, a, b, c, out var candidate, out var d);
                if (d < distSq)
                {
                    distSq = d;
                    closest = candidate;
                }
            }

            return distSq < float.MaxValue;
        }
    }

    public static class WalkableSurfaceRules
    {
        const float AreaEpsilon = 1e-8f;

        public static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var v0x = c.x - a.x;
            var v0y = c.y - a.y;
            var v1x = b.x - a.x;
            var v1y = b.y - a.y;
            var v2x = p.x - a.x;
            var v2y = p.y - a.y;

            var dot00 = v0x * v0x + v0y * v0y;
            var dot01 = v0x * v1x + v0y * v1y;
            var dot02 = v0x * v2x + v0y * v2y;
            var dot11 = v1x * v1x + v1y * v1y;
            var dot12 = v1x * v2x + v1y * v2y;

            var denom = dot00 * dot11 - dot01 * dot01;
            if (Mathf.Abs(denom) <= AreaEpsilon)
            {
                return false;
            }

            var inv = 1f / denom;
            var u = (dot11 * dot02 - dot01 * dot12) * inv;
            var v = (dot00 * dot12 - dot01 * dot02) * inv;
            return u >= -1e-4f && v >= -1e-4f && u + v <= 1f + 1e-4f;
        }

        public static void ClosestPointOnTriangle(
            Vector2 p,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            out Vector2 closest,
            out float distSq)
        {
            if (PointInTriangle(p, a, b, c))
            {
                closest = p;
                distSq = 0f;
                return;
            }

            ClosestPointOnSegment(p, a, b, out var ab, out var abDist);
            ClosestPointOnSegment(p, b, c, out var bc, out var bcDist);
            ClosestPointOnSegment(p, c, a, out var ca, out var caDist);

            if (abDist <= bcDist && abDist <= caDist)
            {
                closest = ab;
                distSq = abDist;
            }
            else if (bcDist <= caDist)
            {
                closest = bc;
                distSq = bcDist;
            }
            else
            {
                closest = ca;
                distSq = caDist;
            }
        }

        public static void ClosestPointOnSegment(
            Vector2 p,
            Vector2 a,
            Vector2 b,
            out Vector2 closest,
            out float distSq)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < AreaEpsilon)
            {
                closest = a;
                distSq = (p - a).sqrMagnitude;
                return;
            }

            var t = Vector2.Dot(p - a, ab) / lengthSq;
            t = Mathf.Clamp01(t);
            closest = a + ab * t;
            distSq = (p - closest).sqrMagnitude;
        }
    }
}
