using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Builds a single seamless <c>RoadSurface</c> mesh from unioned road footprints.</summary>
    public static class RoadSurfaceMeshBuilder
    {
        public const string ObjectName = "RoadSurface";

        public static Mesh BuildMesh(IReadOnlyList<Vector2[]> footprints, float height)
        {
            var triangles2d = RoadPolygonUnion.UnionToTriangles(footprints);
            var y = height * 0.5f;
            var vertices = new List<Vector3>(triangles2d.Count * 3);
            var triangles = new List<int>(triangles2d.Count * 3);
            var uvs = new List<Vector2>(triangles2d.Count * 3);

            for (var i = 0; i < triangles2d.Count; i++)
            {
                var tri = triangles2d[i];
                if (tri == null || tri.Length < 3)
                {
                    continue;
                }

                var a = new Vector3(tri[0].x, y, tri[0].y);
                var b = new Vector3(tri[1].x, y, tri[1].y);
                var c = new Vector3(tri[2].x, y, tri[2].y);
                if (Vector3.Cross(b - a, c - a).y < 0f)
                {
                    (b, c) = (c, b);
                }

                var i0 = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                uvs.Add(RoadMeshUv.FromWorld(a));
                uvs.Add(RoadMeshUv.FromWorld(b));
                uvs.Add(RoadMeshUv.FromWorld(c));
                triangles.Add(i0);
                triangles.Add(i0 + 1);
                triangles.Add(i0 + 2);
            }

            var mesh = new Mesh
            {
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray(),
                uv = uvs.ToArray(),
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Transform Create(Transform parent, IReadOnlyList<Vector2[]> footprints, Material material)
        {
            var fill = new GameObject(ObjectName);
            fill.transform.SetParent(parent, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localRotation = Quaternion.identity;
            fill.transform.localScale = Vector3.one;

            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildMesh(footprints, MatchArenaGreyboxBuilder.RoadHeight);
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return fill.transform;
        }
    }
}
