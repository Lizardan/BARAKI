using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Extruded road ribbon along an arbitrary centerline (smooth arcs for duel flanks).</summary>
    public static class RoadRibbonMesh
    {
        public static Mesh Build(IReadOnlyList<Vector3> centerline, float roadWidth, float height)
        {
            if (centerline == null || centerline.Count < 2)
            {
                return new Mesh();
            }

            var halfWidth = roadWidth * 0.5f;
            var halfHeight = height * 0.5f;
            var vertexCount = centerline.Count * 2;
            var vertices = new Vector3[vertexCount];
            var triangles = new int[(centerline.Count - 1) * 6];

            for (var i = 0; i < centerline.Count; i++)
            {
                var point = centerline[i];
                point.y = 0f;
                var tangent = ResolveTangent(centerline, i);
                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                vertices[i * 2] = new Vector3(
                    point.x + right.x * halfWidth,
                    halfHeight,
                    point.z + right.z * halfWidth);
                vertices[i * 2 + 1] = new Vector3(
                    point.x - right.x * halfWidth,
                    halfHeight,
                    point.z - right.z * halfWidth);
            }

            var triangleIndex = 0;
            for (var i = 0; i < centerline.Count - 1; i++)
            {
                var i0 = i * 2;
                var i1 = i * 2 + 1;
                var i2 = (i + 1) * 2;
                var i3 = (i + 1) * 2 + 1;
                triangles[triangleIndex++] = i0;
                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i2;
                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i3;
                triangles[triangleIndex++] = i2;
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Create(
            Transform parent,
            string name,
            IReadOnlyList<Vector3> centerline,
            float roadHeight,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Build(centerline, MatchArenaGreyboxBuilder.RoadWidth, roadHeight);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        static Vector3 ResolveTangent(IReadOnlyList<Vector3> centerline, int index)
        {
            Vector3 tangent;
            if (index == 0)
            {
                tangent = centerline[1] - centerline[0];
            }
            else if (index == centerline.Count - 1)
            {
                tangent = centerline[index] - centerline[index - 1];
            }
            else
            {
                tangent = centerline[index + 1] - centerline[index - 1];
            }

            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                return Vector3.right;
            }

            return tangent.normalized;
        }
    }
}
