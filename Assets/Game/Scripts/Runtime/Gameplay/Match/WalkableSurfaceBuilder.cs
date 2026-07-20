using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Builds <see cref="WalkableSurface"/> from greybox road meshes under <c>_SourceParts</c>.</summary>
    public static class WalkableSurfaceBuilder
    {
        public static WalkableSurface BuildFromSourceParts(Transform sourcePartsRoot)
        {
            if (sourcePartsRoot == null)
            {
                throw new System.ArgumentNullException(nameof(sourcePartsRoot));
            }

            var parts = new List<WalkablePart>(32);
            var filters = sourcePartsRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (var i = 0; i < filters.Length; i++)
            {
                if (TryBuildPart(filters[i], out var part))
                {
                    parts.Add(part);
                }
            }

            return new WalkableSurface(parts.ToArray());
        }

        /// <summary>Test helper: one part from explicit XZ triangles (A,B,C,...).</summary>
        public static WalkableSurface FromTriangles(params Vector2[] triangleVertices)
        {
            if (triangleVertices == null || triangleVertices.Length < 3)
            {
                return new WalkableSurface(System.Array.Empty<WalkablePart>());
            }

            var min = triangleVertices[0];
            var max = triangleVertices[0];
            for (var i = 1; i < triangleVertices.Length; i++)
            {
                min = Vector2.Min(min, triangleVertices[i]);
                max = Vector2.Max(max, triangleVertices[i]);
            }

            return new WalkableSurface(new[]
            {
                new WalkablePart(min, max, triangleVertices),
            });
        }

        static bool TryBuildPart(MeshFilter filter, out WalkablePart part)
        {
            part = default;
            if (filter == null)
            {
                return false;
            }

            var mesh = filter.sharedMesh;
            if (mesh == null || mesh.vertexCount < 3)
            {
                return false;
            }

            var localVerts = mesh.vertices;
            var tris = mesh.triangles;
            if (tris == null || tris.Length < 3)
            {
                return false;
            }

            var matrix = filter.transform.localToWorldMatrix;
            var projected = new Vector2[localVerts.Length];
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < localVerts.Length; i++)
            {
                var world = matrix.MultiplyPoint3x4(localVerts[i]);
                var xz = new Vector2(world.x, world.z);
                projected[i] = xz;
                min = Vector2.Min(min, xz);
                max = Vector2.Max(max, xz);
            }

            var flatTris = new List<Vector2>(tris.Length);
            for (var i = 0; i + 2 < tris.Length; i += 3)
            {
                var a = projected[tris[i]];
                var b = projected[tris[i + 1]];
                var c = projected[tris[i + 2]];
                if (TriangleAreaSq(a, b, c) < 1e-10f)
                {
                    continue;
                }

                flatTris.Add(a);
                flatTris.Add(b);
                flatTris.Add(c);
            }

            if (flatTris.Count < 3)
            {
                return false;
            }

            part = new WalkablePart(min, max, flatTris.ToArray());
            return true;
        }

        static float TriangleAreaSq(Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = b - a;
            var ac = c - a;
            var cross = ab.x * ac.y - ab.y * ac.x;
            return cross * cross;
        }
    }
}
