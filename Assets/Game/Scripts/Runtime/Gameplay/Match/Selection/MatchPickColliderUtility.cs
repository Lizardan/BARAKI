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

            if (MatchPickLayers.PickableLayer >= 0)
            {
                target.layer = MatchPickLayers.PickableLayer;
            }

            return collider;
        }
    }
}
