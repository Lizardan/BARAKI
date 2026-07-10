using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchSelectionUiGate : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        VisualElement _bottomDock;
        VisualElement _topBar;

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _bottomDock = root.Q<VisualElement>("BottomDock");
            _topBar = root.Q<VisualElement>("TopBar");
        }

        void OnEnable()
        {
            var bridge = FindAnyObjectByType<MatchSelectionBridge>();
            if (bridge == null)
            {
                return;
            }

            var input = bridge.GetComponent<MatchSelectionInput>();
            input?.SetUiBlocker(IsPointerOverBlockedUi);
        }

        bool IsPointerOverBlockedUi()
        {
            var panel = _uiDocument != null ? _uiDocument.rootVisualElement.panel : null;
            if (panel == null)
            {
                return false;
            }

            var screenPosition = (Vector2)UnityEngine.Input.mousePosition;
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
            var picked = panel.Pick(panelPosition);
            if (picked == null)
            {
                return false;
            }

            return IsDescendantOf(_bottomDock, picked) || IsDescendantOf(_topBar, picked);
        }

        static bool IsDescendantOf(VisualElement ancestor, VisualElement element)
        {
            if (ancestor == null)
            {
                return false;
            }

            var current = element;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
