using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Single outer fillet where a road turns 90° (perimeter corner, base T, spoke × perimeter).</summary>
    public static class RoadFilletArc
    {
        public const int RoadArcSegments = 24;
        public const int PathArcSegments = 10;

        public static void AppendPathWaypoints(
            List<Vector3> points,
            Vector3 corner,
            Vector3 inDir,
            Vector3 outDir,
            float arcRadius,
            float laneHeight = N4PerimeterLaneGeometry.LaneHeight)
        {
            inDir = Flatten(inDir);
            outDir = Flatten(outDir);
            if (inDir.sqrMagnitude < 0.0001f || outDir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            inDir.Normalize();
            outDir.Normalize();

            var entry = corner - inDir * arcRadius;
            var exit = corner + outDir * arcRadius;
            var center = entry + outDir * arcRadius;
            var samples = SampleArc(entry, exit, center, arcRadius, PathArcSegments);
            var startIndex = 0;
            if (points.Count > 0 && samples.Count > 0)
            {
                var last = points[^1];
                last.y = 0f;
                var first = samples[0];
                first.y = 0f;
                if (Vector3.Distance(last, first) < 0.05f)
                {
                    startIndex = 1;
                }
            }

            for (var i = startIndex; i < samples.Count; i++)
            {
                var sample = samples[i];
                points.Add(new Vector3(sample.x, laneHeight, sample.z));
            }
        }

        /// <summary>Builds one rounded outer turn. Travel arrives along <paramref name="inDir"/>, leaves along <paramref name="outDir"/>.</summary>
        public static Mesh BuildMesh(
            Vector3 corner,
            Vector3 inDir,
            Vector3 outDir,
            float arcRadius,
            float roadWidth,
            float height)
        {
            inDir = Flatten(inDir);
            outDir = Flatten(outDir);
            if (inDir.sqrMagnitude < 0.0001f || outDir.sqrMagnitude < 0.0001f)
            {
                return new Mesh();
            }

            inDir.Normalize();
            outDir.Normalize();

            var entry = corner - inDir * arcRadius;
            var exit = corner + outDir * arcRadius;
            var center = entry + outDir * arcRadius;
            var halfWidth = roadWidth * 0.5f;
            var halfHeight = height * 0.5f;
            var centerline = SampleArc(entry, exit, center, arcRadius, RoadArcSegments);
            var vertexCount = centerline.Count * 2;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[(centerline.Count - 1) * 6];

            for (var i = 0; i < centerline.Count; i++)
            {
                var point = centerline[i];
                point.y = 0f;
                GetWidthAxes(
                    point,
                    i == 0 ? inDir : null,
                    i == centerline.Count - 1 ? outDir : null,
                    i > 0 ? centerline[i - 1] : centerline[i + 1],
                    i < centerline.Count - 1 ? centerline[i + 1] : centerline[i - 1],
                    out var innerDir,
                    out var outerDir);

                vertices[i * 2] = new Vector3(
                    point.x + innerDir.x * halfWidth,
                    halfHeight,
                    point.z + innerDir.z * halfWidth);
                vertices[i * 2 + 1] = new Vector3(
                    point.x + outerDir.x * halfWidth,
                    halfHeight,
                    point.z + outerDir.z * halfWidth);
                uvs[i * 2] = RoadMeshUv.FromWorld(vertices[i * 2]);
                uvs[i * 2 + 1] = RoadMeshUv.FromWorld(vertices[i * 2 + 1]);
            }

            var triangleIndex = 0;
            for (var i = 0; i < centerline.Count - 1; i++)
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

        public static void CreateFillet(
            Transform parent,
            Vector3 corner,
            Vector3 inDir,
            Vector3 outDir,
            float arcRadius,
            float roadHeight,
            Material material)
        {
            var fill = new GameObject("RoadFilletArc");
            fill.transform.SetParent(parent, false);

            var mesh = BuildMesh(
                corner,
                inDir,
                outDir,
                arcRadius,
                MatchArenaGreyboxBuilder.RoadWidth,
                roadHeight);
            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        public static void CreateFilletLocal(
            Transform parent,
            Vector3 localCorner,
            Vector3 localInDir,
            Vector3 localOutDir,
            float arcRadius,
            float roadHeight,
            Material material)
        {
            var fill = new GameObject("RoadFilletArc");
            fill.transform.SetParent(parent, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localRotation = Quaternion.identity;

            var mesh = BuildMesh(
                localCorner,
                localInDir,
                localOutDir,
                arcRadius,
                MatchArenaGreyboxBuilder.RoadWidth,
                roadHeight);
            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        static List<Vector3> SampleArc(
            Vector3 entry,
            Vector3 exit,
            Vector3 center,
            float radius,
            int segments)
        {
            var startAngle = Mathf.Atan2(entry.z - center.z, entry.x - center.x);
            var endAngle = Mathf.Atan2(exit.z - center.z, exit.x - center.x);
            var sweep = Mathf.DeltaAngle(startAngle * Mathf.Rad2Deg, endAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            var samples = new List<Vector3>(segments + 1);

            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = startAngle + sweep * t;
                samples.Add(new Vector3(
                    center.x + radius * Mathf.Cos(angle),
                    0f,
                    center.z + radius * Mathf.Sin(angle)));
            }

            return samples;
        }

        static void GetWidthAxes(
            Vector3 point,
            Vector3? pinnedInDir,
            Vector3? pinnedOutDir,
            Vector3 previous,
            Vector3 next,
            out Vector3 innerDir,
            out Vector3 outerDir)
        {
            Vector3 tangent;
            if (pinnedInDir.HasValue)
            {
                tangent = pinnedInDir.Value;
            }
            else if (pinnedOutDir.HasValue)
            {
                tangent = pinnedOutDir.Value;
            }
            else
            {
                tangent = next - previous;
                tangent.y = 0f;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    innerDir = Vector3.right;
                    outerDir = Vector3.left;
                    return;
                }

                tangent.Normalize();
            }

            var right = Vector3.Cross(Vector3.up, tangent).normalized;
            innerDir = right;
            outerDir = -right;
        }

        static Vector3 Flatten(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
