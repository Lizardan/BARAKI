using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Straight road ribbon mesh (same construction as arc/fillet roads).</summary>
    public static class RoadStripMesh
    {
        public const float MinSegmentLength = 4f;

        public static void CreateWorld(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            float roadWidth = -1f,
            float height = -1f)
        {
            Create(parent, from, to, material, roadWidth, height, localSpace: false);
        }

        public static void CreateLocal(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            float roadWidth = -1f,
            float height = -1f)
        {
            Create(parent, from, to, material, roadWidth, height, localSpace: true);
        }

        public static Mesh BuildStraight(
            Vector3 from,
            Vector3 to,
            float roadWidth,
            float height)
        {
            from.y = 0f;
            to.y = 0f;
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 0.001f)
            {
                return new Mesh();
            }

            var tangent = delta / length;
            var halfWidth = roadWidth * 0.5f;
            var halfHeight = height * 0.5f;
            var segmentCount = Mathf.Max(1, Mathf.CeilToInt(length / MinSegmentLength));
            var vertexCount = (segmentCount + 1) * 2;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[segmentCount * 6];

            for (var i = 0; i <= segmentCount; i++)
            {
                var t = i / (float)segmentCount;
                var point = Vector3.Lerp(from, to, t);
                GetWidthAxes(point, tangent, out var innerDir, out var outerDir);

                var inner = new Vector3(
                    point.x + innerDir.x * halfWidth,
                    halfHeight,
                    point.z + innerDir.z * halfWidth);
                var outer = new Vector3(
                    point.x + outerDir.x * halfWidth,
                    halfHeight,
                    point.z + outerDir.z * halfWidth);

                vertices[i * 2] = inner;
                vertices[i * 2 + 1] = outer;
                uvs[i * 2] = RoadMeshUv.FromWorld(inner);
                uvs[i * 2 + 1] = RoadMeshUv.FromWorld(outer);
            }

            var triangleIndex = 0;
            for (var i = 0; i < segmentCount; i++)
            {
                var i0 = i * 2;
                var i1 = i * 2 + 1;
                var i2 = (i + 1) * 2;
                var i3 = (i + 1) * 2 + 1;
                RoadMeshTopology.AddUpFacingQuad(vertices, triangles, ref triangleIndex, i0, i1, i2, i3);
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

        static void Create(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            float roadWidth,
            float height,
            bool localSpace)
        {
            roadWidth = roadWidth > 0f ? roadWidth : MatchArenaGreyboxBuilder.RoadWidth;
            height = height > 0f ? height : MatchArenaGreyboxBuilder.RoadHeight;

            var fill = new GameObject("RoadStrip");
            fill.transform.SetParent(parent, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localRotation = Quaternion.identity;
            fill.transform.localScale = Vector3.one;

            var mesh = BuildStraight(from, to, roadWidth, height);
            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        static void GetWidthAxes(
            Vector3 point,
            Vector3 tangent,
            out Vector3 innerDir,
            out Vector3 outerDir)
        {
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                innerDir = Vector3.right;
                outerDir = Vector3.left;
                return;
            }

            tangent.Normalize();
            var right = Vector3.Cross(Vector3.up, tangent).normalized;
            var towardCenter = new Vector3(-point.x, 0f, -point.z);
            if (towardCenter.sqrMagnitude < 0.0001f)
            {
                innerDir = right;
                outerDir = -right;
                return;
            }

            towardCenter.Normalize();
            innerDir = Vector3.Dot(right, towardCenter) >= 0f ? right : -right;
            outerDir = -innerDir;
        }
    }
}
