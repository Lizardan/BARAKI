using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public static class MatchPickColliderUtility
    {
        public static Collider EnsurePickCollider(GameObject target, Vector3 center, Vector3 size)
        {
            DestroyImmediateComponent<MeshCollider>(target);

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

        public static Collider EnsureVisualPickCollider(GameObject target, Mesh mesh)
        {
            if (target == null || mesh == null)
            {
                return null;
            }

            var bounds = mesh.bounds;
            return EnsurePickCollider(target, bounds.center, bounds.size);
        }

        public static void RemovePickCollider(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            DestroyImmediateComponent<MeshCollider>(target);
            DestroyImmediateComponent<BoxCollider>(target);
            DestroyImmediateComponent<MatchPickHandle>(target);
        }

        static void ApplyPickableLayer(GameObject target)
        {
            if (MatchPickLayers.PickableLayer >= 0)
            {
                target.layer = MatchPickLayers.PickableLayer;
            }
        }

        static void DestroyImmediateComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
