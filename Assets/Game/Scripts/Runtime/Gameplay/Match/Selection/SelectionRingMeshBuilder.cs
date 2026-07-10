using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class SelectionRingMeshBuilder
    {
        public const float DefaultRingWidth = 0.2f;
        public const int DefaultSegments = 40;

        public static Mesh BuildAnnulus(
            float outerRadius,
            float ringWidth = DefaultRingWidth,
            int segments = DefaultSegments)
        {
            outerRadius = Mathf.Max(outerRadius, ringWidth + 0.01f);
            var innerRadius = Mathf.Max(outerRadius - ringWidth, 0.01f);
            segments = Mathf.Max(segments, 3);

            var vertexCount = segments * 2;
            var vertices = new Vector3[vertexCount];
            var triangles = new int[segments * 6];

            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);

                var outerIndex = i * 2;
                var innerIndex = outerIndex + 1;
                vertices[outerIndex] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
                vertices[innerIndex] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);

                var nextOuter = ((i + 1) % segments) * 2;
                var nextInner = nextOuter + 1;

                var triangleIndex = i * 6;
                triangles[triangleIndex] = outerIndex;
                triangles[triangleIndex + 1] = nextOuter;
                triangles[triangleIndex + 2] = innerIndex;
                triangles[triangleIndex + 3] = innerIndex;
                triangles[triangleIndex + 4] = nextOuter;
                triangles[triangleIndex + 5] = nextInner;
            }

            var mesh = new Mesh
            {
                name = "SelectionGroundRing",
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
