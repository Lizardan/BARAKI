using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Consistent up-facing triangle winding for flat road meshes.</summary>
    public static class RoadMeshTopology
    {
        public static void AddUpFacingTriangle(
            Vector3[] vertices,
            int[] triangles,
            ref int triangleIndex,
            int i0,
            int i1,
            int i2)
        {
            if (TriangleFacesUp(vertices[i0], vertices[i1], vertices[i2]))
            {
                triangles[triangleIndex++] = i0;
                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i2;
                return;
            }

            triangles[triangleIndex++] = i0;
            triangles[triangleIndex++] = i2;
            triangles[triangleIndex++] = i1;
        }

        public static void AddUpFacingQuad(
            Vector3[] vertices,
            int[] triangles,
            ref int triangleIndex,
            int i0,
            int i1,
            int i2,
            int i3)
        {
            if (TriangleFacesUp(vertices[i0], vertices[i1], vertices[i2]))
            {
                triangles[triangleIndex++] = i0;
                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i2;
                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i3;
                triangles[triangleIndex++] = i2;
                return;
            }

            triangles[triangleIndex++] = i0;
            triangles[triangleIndex++] = i2;
            triangles[triangleIndex++] = i1;
            triangles[triangleIndex++] = i1;
            triangles[triangleIndex++] = i2;
            triangles[triangleIndex++] = i3;
        }

        static bool TriangleFacesUp(Vector3 a, Vector3 b, Vector3 c) =>
            Vector3.Cross(b - a, c - a).y >= 0f;
    }
}
