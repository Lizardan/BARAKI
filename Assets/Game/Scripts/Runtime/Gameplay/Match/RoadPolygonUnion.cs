using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>2D polygon union + triangulation via Clipper2 (XZ footprints).</summary>
    public static class RoadPolygonUnion
    {
        public const double Scale = 1000.0;

        public static List<Vector2[]> Union(IReadOnlyList<Vector2[]> polygons)
        {
            var subjects = ToPaths64(polygons);
            if (subjects.Count == 0)
            {
                return new List<Vector2[]>();
            }

            return FromPaths64(Clipper.Union(subjects, FillRule.NonZero));
        }

        /// <summary>Union footprints and return triangles as 3-point polygons (CCW in XZ).</summary>
        public static List<Vector2[]> UnionToTriangles(IReadOnlyList<Vector2[]> polygons)
        {
            var subjects = ToPaths64(polygons);
            if (subjects.Count == 0)
            {
                return new List<Vector2[]>();
            }

            var unioned = Clipper.Union(subjects, FillRule.NonZero);
            if (unioned.Count == 0)
            {
                return new List<Vector2[]>();
            }

            var triangulateResult = Clipper.Triangulate(unioned, out var triPaths, useDelaunay: true);
            if (triangulateResult != TriangulateResult.success || triPaths == null)
            {
                // Fallback: ear-clip each outer path if CDT fails.
                return EarClipFallback(FromPaths64(unioned));
            }

            return FromPaths64(triPaths);
        }

        static Paths64 ToPaths64(IReadOnlyList<Vector2[]> polygons)
        {
            var subjects = new Paths64();
            for (var i = 0; i < polygons.Count; i++)
            {
                var poly = polygons[i];
                if (poly == null || poly.Length < 3)
                {
                    continue;
                }

                var path = new Path64(poly.Length);
                for (var p = 0; p < poly.Length; p++)
                {
                    path.Add(new Point64(
                        (long)System.Math.Round(poly[p].x * Scale),
                        (long)System.Math.Round(poly[p].y * Scale)));
                }

                // Opposite windings cancel under NonZero and punch holes in overlaps.
                if (!Clipper.IsPositive(path))
                {
                    path.Reverse();
                }

                subjects.Add(path);
            }

            return subjects;
        }

        static List<Vector2[]> FromPaths64(Paths64 paths)
        {
            var result = new List<Vector2[]>(paths.Count);
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (path.Count < 3)
                {
                    continue;
                }

                var poly = new Vector2[path.Count];
                for (var p = 0; p < path.Count; p++)
                {
                    poly[p] = new Vector2(
                        (float)(path[p].X / Scale),
                        (float)(path[p].Y / Scale));
                }

                result.Add(poly);
            }

            return result;
        }

        static List<Vector2[]> EarClipFallback(List<Vector2[]> polygons)
        {
            var tris = new List<Vector2[]>();
            RoadSurfaceTriangulator.Triangulate(
                polygons,
                0f,
                out var vertices,
                out var indices,
                out _);
            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                tris.Add(new[]
                {
                    new Vector2(vertices[indices[i]].x, vertices[indices[i]].z),
                    new Vector2(vertices[indices[i + 1]].x, vertices[indices[i + 1]].z),
                    new Vector2(vertices[indices[i + 2]].x, vertices[indices[i + 2]].z),
                });
            }

            return tris;
        }
    }
}
