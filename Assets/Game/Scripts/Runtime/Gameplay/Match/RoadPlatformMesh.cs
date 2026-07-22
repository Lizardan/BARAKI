using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Flat arena platforms with world-space UVs for road materials.</summary>
    public static class RoadPlatformMesh
    {
        public static void CreateDisc(
            Transform parent,
            string objectName,
            float diameter,
            float height,
            Material material)
        {
            var fill = new GameObject(objectName);
            fill.transform.SetParent(parent, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localRotation = Quaternion.identity;
            fill.transform.localScale = Vector3.one;

            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildDisc(diameter, height);
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        public static void CreateRect(
            Transform parent,
            string objectName,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 size,
            Material material)
        {
            var fill = new GameObject(objectName);
            fill.transform.SetParent(parent, false);
            fill.transform.position = worldPosition;
            fill.transform.rotation = worldRotation;
            fill.transform.localScale = Vector3.one;

            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildRect(size);
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var uvs = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                uvs[i] = RoadMeshUv.FromWorld(fill.transform.TransformPoint(vertices[i]));
            }

            mesh.uv = uvs;
        }

        public static Mesh BuildDisc(float diameter, float height, int segments = 48)
        {
            var radius = diameter * 0.5f;
            var halfHeight = height * 0.5f;
            var vertexCount = segments + 1;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[segments * 3];

            vertices[0] = new Vector3(0f, halfHeight, 0f);
            uvs[0] = RoadMeshUv.FromWorld(0f, 0f);

            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(angle) * radius;
                var z = Mathf.Sin(angle) * radius;
                vertices[i + 1] = new Vector3(x, halfHeight, z);
                uvs[i + 1] = RoadMeshUv.FromWorld(x, z);
            }

            var triangleIndex = 0;
            for (var i = 0; i < segments; i++)
            {
                var next = i + 1;
                var after = i == segments - 1 ? 1 : i + 2;
                RoadMeshTopology.AddUpFacingTriangle(vertices, triangles, ref triangleIndex, 0, next, after);
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                uv = uvs,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildRect(Vector3 size)
        {
            var halfX = size.x * 0.5f;
            var halfZ = size.z * 0.5f;
            var halfY = size.y * 0.5f;
            var vertices = new[]
            {
                new Vector3(-halfX, halfY, -halfZ),
                new Vector3(halfX, halfY, -halfZ),
                new Vector3(halfX, halfY, halfZ),
                new Vector3(-halfX, halfY, halfZ),
            };
            var uvs = new[]
            {
                RoadMeshUv.FromWorld(vertices[0]),
                RoadMeshUv.FromWorld(vertices[1]),
                RoadMeshUv.FromWorld(vertices[2]),
                RoadMeshUv.FromWorld(vertices[3]),
            };
            // Strip order expected by AddUpFacingQuad:
            // 0 -- 1
            // |    |
            // 3 -- 2
            var triangles = new int[6];
            var triangleIndex = 0;
            RoadMeshTopology.AddUpFacingQuad(vertices, triangles, ref triangleIndex, 0, 1, 3, 2);

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                uv = uvs,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
