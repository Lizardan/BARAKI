using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchPickColliderUtility
    {
        public static Collider EnsurePickCollider(GameObject target, Vector3 center, Vector3 size)
        {
            var collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = target.AddComponent<BoxCollider>();
            }

            collider.isTrigger = true;
            collider.center = center;
            collider.size = size;
            collider.enabled = true;
            ApplyPickableLayer(target);
            return collider;
        }

        /// <summary>
        /// Mesh-accurate pick for buildings: ray hits the visible silhouette, not the AABB empty space.
        /// Non-convex MeshCollider cannot be a trigger; MatchPickable is query-only (no Rigidbodies on it).
        /// </summary>
        public static Collider EnsureMeshPickCollider(GameObject target, Mesh mesh)
        {
            if (target == null || mesh == null)
            {
                return null;
            }

            var box = target.GetComponent<BoxCollider>();
            if (box != null)
            {
                DestroyCollider(box);
            }

            var collider = target.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = target.AddComponent<MeshCollider>();
            }

            collider.sharedMesh = mesh;
            collider.convex = false;
            collider.isTrigger = false;
            collider.enabled = true;
            ApplyPickableLayer(target);
            return collider;
        }

        public static void RemovePickCollider(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            var meshCollider = target.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                DestroyCollider(meshCollider);
            }

            var boxCollider = target.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                DestroyCollider(boxCollider);
            }

            var handle = target.GetComponent<MatchPickHandle>();
            if (handle != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(handle);
                }
                else
                {
                    Object.DestroyImmediate(handle);
                }
            }
        }

        static void ApplyPickableLayer(GameObject target)
        {
            if (MatchPickLayers.PickableLayer >= 0)
            {
                target.layer = MatchPickLayers.PickableLayer;
            }
        }

        static void DestroyCollider(Collider collider)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
