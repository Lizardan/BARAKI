using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    /// <summary>Clears unit selection when the selected unit is no longer alive in combat.</summary>
    public sealed class MatchSelectionValidityPresenter : MonoBehaviour
    {
        [SerializeField] private MatchRuntime _runtime;

        MatchSelection _selection;

        void Awake()
        {
            if (_runtime == null)
            {
                _runtime = GetComponent<MatchRuntime>();
            }
        }

        void OnEnable()
        {
            SubscribeSelection();
        }

        void OnDisable()
        {
            UnsubscribeSelection();
        }

        void LateUpdate()
        {
            if (_selection == null)
            {
                SubscribeSelection();
                return;
            }

            var current = _selection.Current;
            if (!current.HasTarget || !current.IsUnit)
            {
                return;
            }

            var combat = _runtime?.Controller?.Combat;
            if (!MatchSelectionTargetRules.IsUnitTargetAlive(combat?.Units, current.EntityId))
            {
                _selection.Clear();
            }
        }

        void SubscribeSelection()
        {
            UnsubscribeSelection();
            _selection = _runtime != null ? _runtime.Selection : null;
        }

        void UnsubscribeSelection()
        {
            _selection = null;
        }
    }
}
