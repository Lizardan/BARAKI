using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Ear-clip triangulation for simple (no-hole) XZ polygons.</summary>
    public static class RoadSurfaceTriangulator
    {
        public static void Triangulate(
            IReadOnlyList<Vector2[]> polygons,
            float y,
            out Vector3[] vertices,
            out int[] triangles,
            out Vector2[] uvs)
        {
            var verts = new List<Vector3>(256);
            var tris = new List<int>(512);
            for (var i = 0; i < polygons.Count; i++)
            {
                AppendPolygon(polygons[i], y, verts, tris);
            }

            vertices = verts.ToArray();
            triangles = tris.ToArray();
            uvs = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                uvs[i] = RoadMeshUv.FromWorld(vertices[i]);
            }
        }

        static void AppendPolygon(Vector2[] polygon, float y, List<Vector3> verts, List<int> tris)
        {
            if (polygon == null || polygon.Length < 3)
            {
                return;
            }

            var points = new List<Vector2>(polygon.Length);
            for (var i = 0; i < polygon.Length; i++)
            {
                if (points.Count > 0 && (points[^1] - polygon[i]).sqrMagnitude < 1e-8f)
                {
                    continue;
                }

                points.Add(polygon[i]);
            }

            if (points.Count >= 3 && (points[0] - points[^1]).sqrMagnitude < 1e-8f)
            {
                points.RemoveAt(points.Count - 1);
            }

            if (points.Count < 3)
            {
                return;
            }

            if (SignedArea(points) < 0f)
            {
                points.Reverse();
            }

            var indices = new List<int>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                indices.Add(i);
            }

            var baseVertex = verts.Count;
            for (var i = 0; i < points.Count; i++)
            {
                verts.Add(new Vector3(points[i].x, y, points[i].y));
            }

            var guard = points.Count * points.Count;
            while (indices.Count > 3 && guard-- > 0)
            {
                var earFound = false;
                for (var i = 0; i < indices.Count; i++)
                {
                    var prev = indices[(i - 1 + indices.Count) % indices.Count];
                    var curr = indices[i];
                    var next = indices[(i + 1) % indices.Count];
                    if (!IsEar(points, indices, prev, curr, next))
                    {
                        continue;
                    }

                    tris.Add(baseVertex + prev);
                    tris.Add(baseVertex + curr);
                    tris.Add(baseVertex + next);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    break;
                }
            }

            if (indices.Count == 3)
            {
                tris.Add(baseVertex + indices[0]);
                tris.Add(baseVertex + indices[1]);
                tris.Add(baseVertex + indices[2]);
            }
        }

        static float SignedArea(List<Vector2> points)
        {
            var area = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                area += (points[j].x * points[i].y) - (points[i].x * points[j].y);
            }

            return area * 0.5f;
        }

        static bool IsEar(List<Vector2> points, List<int> indices, int prev, int curr, int next)
        {
            var a = points[prev];
            var b = points[curr];
            var c = points[next];
            if (Cross(a, b, c) <= 1e-6f)
            {
                return false;
            }

            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                if (idx == prev || idx == curr || idx == next)
                {
                    continue;
                }

                if (PointInTriangle(points[idx], a, b, c))
                {
                    return false;
                }
            }

            return true;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var c1 = Cross(a, b, p);
            var c2 = Cross(b, c, p);
            var c3 = Cross(c, a, p);
            var hasNeg = c1 < 0f || c2 < 0f || c3 < 0f;
            var hasPos = c1 > 0f || c2 > 0f || c3 > 0f;
            return !(hasNeg && hasPos);
        }
    }
}
