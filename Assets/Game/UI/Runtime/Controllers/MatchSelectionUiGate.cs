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
        MatchSelectionInput _boundInput;

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
            TryBindUiBlocker();
        }

        void OnDisable()
        {
            if (_boundInput != null)
            {
                _boundInput.SetUiBlocker(null);
                _boundInput = null;
            }
        }

        void LateUpdate()
        {
            TryBindUiBlocker();
        }

        void TryBindUiBlocker()
        {
            var bridge = FindAnyObjectByType<MatchSelectionBridge>();
            if (bridge == null)
            {
                return;
            }

            var input = bridge.GetComponent<MatchSelectionInput>();
            if (input == null || input == _boundInput)
            {
                return;
            }

            if (_boundInput != null)
            {
                _boundInput.SetUiBlocker(null);
            }

            input.SetUiBlocker(IsPointerOverBlockedUi);
            _boundInput = input;
        }

        bool IsPointerOverBlockedUi()
        {
            var panel = _uiDocument != null ? _uiDocument.rootVisualElement.panel : null;
            if (panel == null)
            {
                return false;
            }

            if (!MatchSelectionUiPointer.TryGetScreenPosition(out var screenPosition))
            {
                return false;
            }

            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
            var picked = panel.Pick(panelPosition);
            if (picked == null)
            {
                return false;
            }

            return MatchSelectionUiPointer.IsDescendantOf(_bottomDock, picked)
                   || MatchSelectionUiPointer.IsDescendantOf(_topBar, picked);
        }
    }
}
