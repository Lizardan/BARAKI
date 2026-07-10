using Game.Gameplay.Match.Selection;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Owns pick registry, selection state, and click input for the active match.</summary>
    public sealed class MatchSelectionBridge : MonoBehaviour
    {
        readonly MatchPickRegistry _registry = new();
        readonly MatchSelection _selection = new();

        MatchSelectionInput _input;

        public MatchPickRegistry Registry => _registry;
        public MatchSelection Selection => _selection;

        public void BeginMatch()
        {
            _registry.Clear();
            _selection.Clear();
            EnsureInput();
        }

        public void EndMatch()
        {
            _registry.Clear();
            _selection.Clear();
        }

        public void RegisterPickCollider(Collider collider, MatchPickTarget target)
        {
            _registry.Register(collider, target);
        }

        public void UnregisterPickCollider(Collider collider)
        {
            _registry.Unregister(collider);
        }

        void EnsureInput()
        {
            if (_input == null)
            {
                _input = GetComponent<MatchSelectionInput>();
                if (_input == null)
                {
                    _input = gameObject.AddComponent<MatchSelectionInput>();
                }
            }

            _input.Initialize(_registry, _selection);
        }
    }
}
