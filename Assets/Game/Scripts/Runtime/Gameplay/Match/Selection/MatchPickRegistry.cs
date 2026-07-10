using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public sealed class MatchPickRegistry
    {
        readonly Dictionary<Collider, MatchPickTarget> _colliders = new();

        public void Clear() => _colliders.Clear();

        public void Register(Collider collider, MatchPickTarget target)
        {
            if (collider == null)
            {
                return;
            }

            _colliders[collider] = target;
        }

        public void Unregister(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            _colliders.Remove(collider);
        }

        public bool TryResolve(Collider collider, out MatchPickTarget target)
        {
            if (collider == null)
            {
                target = MatchPickTarget.None;
                return false;
            }

            return _colliders.TryGetValue(collider, out target);
        }
    }
}
