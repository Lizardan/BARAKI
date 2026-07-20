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
        VisualElement _debugHudPanel;
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
            _debugHudPanel = root.Q<VisualElement>("DebugHudPanel");
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
            if (input == null)
            {
                return;
            }

            if (_boundInput != null && _boundInput != input)
            {
                _boundInput.SetUiBlocker(null);
            }

            // Re-apply every frame so BeginMatch/Initialize cannot leave the gate unbound.
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

            var panelPosition = MatchSelectionUiPointer.ScreenToPanelPosition(panel, screenPosition);
            var picked = panel.Pick(panelPosition);
            if (MatchSelectionUiPointer.IsPointerOverBlockedUi(
                    picked,
                    _bottomDock,
                    _topBar,
                    _debugHudPanel))
            {
                return true;
            }

            // Layout fallback when Pick misses (Ignore overlays / scale edge cases).
            return ContainsPanelPoint(_bottomDock, panelPosition)
                   || ContainsPanelPoint(_topBar, panelPosition)
                   || ContainsPanelPoint(_debugHudPanel, panelPosition);
        }

        static bool ContainsPanelPoint(VisualElement element, Vector2 panelPosition) =>
            element != null
            && element.resolvedStyle.display != DisplayStyle.None
            && element.worldBound.Contains(panelPosition);
    }
}
