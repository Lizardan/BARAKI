using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>XZ footprint helpers for road surface union.</summary>
    public static class RoadFootprintShapes
    {
        public static Vector2[] OrientedStrip(Vector3 from, Vector3 to, float roadWidth)
        {
            from.y = 0f;
            to.y = 0f;
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 0.001f)
            {
                return System.Array.Empty<Vector2>();
            }

            var tangent = delta / length;
            var right = Vector3.Cross(Vector3.up, tangent).normalized;
            var half = roadWidth * 0.5f;
            var a = from - right * half;
            var b = from + right * half;
            var c = to + right * half;
            var d = to - right * half;
            return new[]
            {
                new Vector2(a.x, a.z),
                new Vector2(b.x, b.z),
                new Vector2(c.x, c.z),
                new Vector2(d.x, d.z),
            };
        }

        /// <summary>
        /// Continuous road footprint along a centerline polyline (no inner-radius seams between segments).
        /// Contour = right edge forward, then left edge reverse — same winding as <see cref="FromRibbonMesh"/>.
        /// </summary>
        public static Vector2[] CenteredPolylineStrip(
            IReadOnlyList<Vector3> centerline,
            float roadWidth,
            Vector3? startTangent = null,
            Vector3? endTangent = null)
        {
            if (centerline == null || centerline.Count < 2 || roadWidth <= 0.001f)
            {
                return System.Array.Empty<Vector2>();
            }

            var half = roadWidth * 0.5f;
            var count = centerline.Count;
            var rightEdge = new Vector2[count];
            var leftEdge = new Vector2[count];

            for (var i = 0; i < count; i++)
            {
                var point = centerline[i];
                point.y = 0f;
                var tangent = ResolvePolylineTangent(centerline, i, startTangent, endTangent);
                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                var rightPoint = point + right * half;
                var leftPoint = point - right * half;
                rightEdge[i] = new Vector2(rightPoint.x, rightPoint.z);
                leftEdge[i] = new Vector2(leftPoint.x, leftPoint.z);
            }

            var points = new Vector2[count * 2];
            for (var i = 0; i < count; i++)
            {
                points[i] = rightEdge[i];
            }

            for (var i = 0; i < count; i++)
            {
                points[count + i] = leftEdge[count - 1 - i];
            }

            return points;
        }

        static Vector3 ResolvePolylineTangent(
            IReadOnlyList<Vector3> centerline,
            int index,
            Vector3? startTangent,
            Vector3? endTangent)
        {
            if (index == 0 && startTangent.HasValue)
            {
                var pinned = startTangent.Value;
                pinned.y = 0f;
                if (pinned.sqrMagnitude > 0.0001f)
                {
                    return pinned.normalized;
                }
            }

            if (index == centerline.Count - 1 && endTangent.HasValue)
            {
                var pinned = endTangent.Value;
                pinned.y = 0f;
                if (pinned.sqrMagnitude > 0.0001f)
                {
                    return pinned.normalized;
                }
            }

            var prev = centerline[Mathf.Max(0, index - 1)];
            var next = centerline[Mathf.Min(centerline.Count - 1, index + 1)];
            prev.y = 0f;
            next.y = 0f;
            var tangent = next - prev;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                if (index > 0)
                {
                    var fallback = centerline[index] - centerline[index - 1];
                    fallback.y = 0f;
                    if (fallback.sqrMagnitude > 0.0001f)
                    {
                        return fallback.normalized;
                    }
                }

                return Vector3.right;
            }

            return tangent.normalized;
        }

        public static Vector2[] Disc(float radius, int segments = 48)
        {
            var points = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            return points;
        }

        public static Vector2[] OrientedRect(Vector3 worldCenter, Quaternion worldRotation, Vector3 size)
        {
            var halfX = size.x * 0.5f;
            var halfZ = size.z * 0.5f;
            var locals = new[]
            {
                new Vector3(-halfX, 0f, -halfZ),
                new Vector3(halfX, 0f, -halfZ),
                new Vector3(halfX, 0f, halfZ),
                new Vector3(-halfX, 0f, halfZ),
            };
            var points = new Vector2[4];
            for (var i = 0; i < 4; i++)
            {
                var world = worldCenter + worldRotation * locals[i];
                points[i] = new Vector2(world.x, world.z);
            }

            return points;
        }

        /// <summary>
        /// Ribbon mesh verts are paired (inner, outer) along the centerline.
        /// Closed contour = outer forward + inner reverse.
        /// </summary>
        public static Vector2[] FromRibbonMesh(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount < 4)
            {
                return System.Array.Empty<Vector2>();
            }

            var vertices = mesh.vertices;
            var pairCount = vertices.Length / 2;
            if (pairCount < 2)
            {
                return System.Array.Empty<Vector2>();
            }

            var points = new Vector2[pairCount * 2];
            for (var i = 0; i < pairCount; i++)
            {
                var outer = vertices[i * 2 + 1];
                points[i] = new Vector2(outer.x, outer.z);
            }

            for (var i = 0; i < pairCount; i++)
            {
                var inner = vertices[(pairCount - 1 - i) * 2];
                points[pairCount + i] = new Vector2(inner.x, inner.z);
            }

            return points;
        }

        public static bool PointInAnyPolygon(Vector2 point, IReadOnlyList<Vector2[]> polygons)
        {
            for (var i = 0; i < polygons.Count; i++)
            {
                if (PointInPolygon(point, polygons[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
            {
                return false;
            }

            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                var intersect = ((pi.y > point.y) != (pj.y > point.y))
                    && (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + float.Epsilon) + pi.x);
                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
