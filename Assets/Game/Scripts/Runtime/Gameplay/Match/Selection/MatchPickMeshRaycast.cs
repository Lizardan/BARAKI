using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    /// <summary>
    /// Triangle pick against a readable mesh so AABB/box hits through empty silhouette are ignored.
    /// </summary>
    public static class MatchPickMeshRaycast
    {
        const float Epsilon = 1e-8f;

        public static bool TryResolveHit(Collider collider, Ray worldRay, float physicsDistance, float maxDistance, out float distance)
        {
            distance = physicsDistance;
            if (collider == null)
            {
                return false;
            }

            var meshFilter = collider.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return true;
            }

            return TryHit(
                meshFilter.transform,
                meshFilter.sharedMesh,
                worldRay,
                maxDistance,
                out distance);
        }

        public static bool TryHitColliderMesh(Collider collider, Ray worldRay, float maxDistance, out float worldDistance)
        {
            return TryResolveHit(collider, worldRay, maxDistance, maxDistance, out worldDistance);
        }

        public static bool TryHit(
            Transform meshTransform,
            Mesh mesh,
            Ray worldRay,
            float maxDistance,
            out float worldDistance)
        {
            worldDistance = maxDistance;
            if (meshTransform == null || mesh == null || !mesh.isReadable)
            {
                return false;
            }

            var localOrigin = meshTransform.InverseTransformPoint(worldRay.origin);
            var localEnd = meshTransform.InverseTransformPoint(worldRay.GetPoint(maxDistance));
            var localSpan = localEnd - localOrigin;
            var localMax = localSpan.magnitude;
            if (localMax < Epsilon)
            {
                return false;
            }

            var localDir = localSpan / localMax;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var bestT = float.MaxValue;
            var hit = false;

            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                if (!IntersectTriangle(
                        localOrigin,
                        localDir,
                        vertices[triangles[i]],
                        vertices[triangles[i + 1]],
                        vertices[triangles[i + 2]],
                        out var t)
                    || t < 0f
                    || t > localMax
                    || t >= bestT)
                {
                    continue;
                }

                bestT = t;
                hit = true;
            }

            if (!hit)
            {
                return false;
            }

            var worldHit = meshTransform.TransformPoint(localOrigin + localDir * bestT);
            worldDistance = Vector3.Distance(worldRay.origin, worldHit);
            return worldDistance <= maxDistance;
        }

        static bool IntersectTriangle(
            Vector3 origin,
            Vector3 direction,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float t)
        {
            t = 0f;
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var p = Vector3.Cross(direction, edge2);
            var det = Vector3.Dot(edge1, p);
            if (det > -Epsilon && det < Epsilon)
            {
                return false;
            }

            var invDet = 1f / det;
            var s = origin - v0;
            var u = Vector3.Dot(s, p) * invDet;
            if (u < 0f || u > 1f)
            {
                return false;
            }

            var q = Vector3.Cross(s, edge1);
            var v = Vector3.Dot(direction, q) * invDet;
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            t = Vector3.Dot(edge2, q) * invDet;
            return t >= 0f;
        }
    }
}
