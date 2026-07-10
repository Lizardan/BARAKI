using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Quarter-circle corner rounding for N=4 perimeter roads and flank paths.</summary>
    public static class PerimeterCornerArc
    {
        public const float CornerArcRadius = N4RoadReferenceSpec.PerimeterCornerCenterlineRadius;
        public const int PathArcSegments = 12;
        public const int RoadArcSegments = 24;

        public static void AppendPathWaypoints(
            List<Vector3> points,
            Vector3 corner,
            bool turnClockwise,
            float laneHeight = 0.15f,
            float arcRadius = -1f)
        {
            var samples = GetCanonicalArcSamples(corner, PathArcSegments, arcRadius);
            if (!turnClockwise)
            {
                samples.Reverse();
            }

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

        public static List<Vector3> GetCanonicalArcSamples(Vector3 corner, int segments, float arcRadius = -1f)
        {
            var radius = ResolveArcRadius(arcRadius);
            GetClockwiseEndpoints(corner, out var entry, out var exit, radius);
            return SampleArcBetween(entry, exit, GetArcCenter(corner, radius), segments, includeStart: true, radius);
        }

        public static Mesh BuildCornerRoadMesh(
            Vector3 corner,
            bool turnClockwise,
            float height,
            float arcRadius = -1f)
        {
            var radius = ResolveArcRadius(arcRadius);
            var halfWidth = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;
            var halfHeight = height * 0.5f;
            var centerline = GetCanonicalArcSamples(corner, RoadArcSegments, radius);
            GetClockwiseEndpoints(corner, out var entry, out var exit, radius);
            var perimeterHalfSize = Mathf.Max(Mathf.Abs(corner.x), Mathf.Abs(corner.z));
            var vertexCount = centerline.Count * 2;
            var vertices = new Vector3[vertexCount];
            var triangles = new int[(centerline.Count - 1) * 6];

            for (var i = 0; i < centerline.Count; i++)
            {
                var point = centerline[i];
                point.y = 0f;
                GetRoadWidthAxes(
                    point,
                    corner,
                    perimeterHalfSize,
                    i == 0 ? entry : null,
                    i == centerline.Count - 1 ? exit : null,
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

        public static void GetClockwiseEndpoints(Vector3 corner, out Vector3 entry, out Vector3 exit, float arcRadius = -1f)
        {
            var radius = ResolveArcRadius(arcRadius);
            var halfX = Mathf.Abs(corner.x);
            var halfZ = Mathf.Abs(corner.z);

            if (corner.x >= 0f && corner.z >= 0f)
            {
                entry = new Vector3(halfX - radius, 0f, halfZ);
                exit = new Vector3(halfX, 0f, halfZ - radius);
                return;
            }

            if (corner.x >= 0f)
            {
                entry = new Vector3(halfX, 0f, -halfZ + radius);
                exit = new Vector3(halfX - radius, 0f, -halfZ);
                return;
            }

            if (corner.z < 0f)
            {
                entry = new Vector3(-halfX + radius, 0f, -halfZ);
                exit = new Vector3(-halfX, 0f, -halfZ + radius);
                return;
            }

            entry = new Vector3(-halfX, 0f, halfZ - radius);
            exit = new Vector3(-halfX + radius, 0f, halfZ);
        }

        public static float ResolveArcRadius(float arcRadius) =>
            arcRadius > 0f ? arcRadius : CornerArcRadius;

        static void GetRoadWidthAxes(
            Vector3 point,
            Vector3 corner,
            float perimeterHalfSize,
            Vector3? pinnedEntry,
            Vector3? pinnedExit,
            Vector3 previous,
            Vector3 next,
            out Vector3 innerDir,
            out Vector3 outerDir)
        {
            if (pinnedEntry.HasValue)
            {
                GetStraightStripWidthAxes(pinnedEntry.Value, perimeterHalfSize, out innerDir, out outerDir);
                return;
            }

            if (pinnedExit.HasValue)
            {
                GetStraightStripWidthAxes(pinnedExit.Value, perimeterHalfSize, out innerDir, out outerDir);
                return;
            }

            var tangent = next - previous;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                GetStraightStripWidthAxes(point, perimeterHalfSize, out innerDir, out outerDir);
                return;
            }

            tangent.Normalize();
            var right = Vector3.Cross(Vector3.up, tangent).normalized;
            var towardCenter = new Vector3(-point.x, 0f, -point.z);
            if (towardCenter.sqrMagnitude < 0.0001f)
            {
                towardCenter = new Vector3(-Mathf.Sign(corner.x), 0f, -Mathf.Sign(corner.z));
            }

            towardCenter.Normalize();
            innerDir = Vector3.Dot(right, towardCenter) >= 0f ? right : -right;
            outerDir = -innerDir;
        }

        static void GetStraightStripWidthAxes(Vector3 edgePoint, float perimeterHalfSize, out Vector3 innerDir, out Vector3 outerDir)
        {
            const float epsilon = 0.01f;
            if (Mathf.Abs(edgePoint.z - perimeterHalfSize) < epsilon)
            {
                innerDir = Vector3.back;
                outerDir = Vector3.forward;
                return;
            }

            if (Mathf.Abs(edgePoint.z + perimeterHalfSize) < epsilon)
            {
                innerDir = Vector3.forward;
                outerDir = Vector3.back;
                return;
            }

            if (Mathf.Abs(edgePoint.x - perimeterHalfSize) < epsilon)
            {
                innerDir = Vector3.left;
                outerDir = Vector3.right;
                return;
            }

            innerDir = Vector3.right;
            outerDir = Vector3.left;
        }

        static List<Vector3> SampleArcBetween(
            Vector3 entry,
            Vector3 exit,
            Vector3 center,
            int segments,
            bool includeStart,
            float arcRadius)
        {
            var startAngle = Mathf.Atan2(entry.z - center.z, entry.x - center.x);
            var endAngle = Mathf.Atan2(exit.z - center.z, exit.x - center.x);
            var sweep = Mathf.DeltaAngle(startAngle * Mathf.Rad2Deg, endAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            var startIndex = includeStart ? 0 : 1;
            var samples = new List<Vector3>(segments + 1);

            for (var i = startIndex; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = startAngle + sweep * t;
                samples.Add(new Vector3(
                    center.x + arcRadius * Mathf.Cos(angle),
                    0f,
                    center.z + arcRadius * Mathf.Sin(angle)));
            }

            return samples;
        }

        static Vector3 GetArcCenter(Vector3 corner, float arcRadius)
        {
            var signX = corner.x >= 0f ? 1f : -1f;
            var signZ = corner.z >= 0f ? 1f : -1f;
            return new Vector3(
                corner.x - signX * arcRadius,
                0f,
                corner.z - signZ * arcRadius);
        }
    }
}
