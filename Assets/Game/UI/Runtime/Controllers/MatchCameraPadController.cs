using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// D-pad under the top bar: orients the gameplay camera so the local base
    /// sits on the chosen screen edge.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchCameraPadController : MonoBehaviour
    {
        [SerializeField] UIDocument _uiDocument;

        Button _upButton;
        Button _downButton;
        Button _leftButton;
        Button _rightButton;
        Label _hintLabel;

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _upButton = root.Q<Button>("CameraPadUpButton");
            _downButton = root.Q<Button>("CameraPadDownButton");
            _leftButton = root.Q<Button>("CameraPadLeftButton");
            _rightButton = root.Q<Button>("CameraPadRightButton");
            _hintLabel = root.Q<Label>("CameraPadHint");
            if (_hintLabel != null)
            {
                _hintLabel.text = "Ориентация базы\nна краю экрана";
            }
        }

        void OnEnable()
        {
            if (_upButton != null)
            {
                _upButton.clicked += OnUpClicked;
            }

            if (_downButton != null)
            {
                _downButton.clicked += OnDownClicked;
            }

            if (_leftButton != null)
            {
                _leftButton.clicked += OnLeftClicked;
            }

            if (_rightButton != null)
            {
                _rightButton.clicked += OnRightClicked;
            }
        }

        void OnDisable()
        {
            if (_upButton != null)
            {
                _upButton.clicked -= OnUpClicked;
            }

            if (_downButton != null)
            {
                _downButton.clicked -= OnDownClicked;
            }

            if (_leftButton != null)
            {
                _leftButton.clicked -= OnLeftClicked;
            }

            if (_rightButton != null)
            {
                _rightButton.clicked -= OnRightClicked;
            }
        }

        void OnUpClicked() => Orient(CameraBaseScreenEdge.Top);

        void OnDownClicked() => Orient(CameraBaseScreenEdge.Bottom);

        void OnLeftClicked() => Orient(CameraBaseScreenEdge.Left);

        void OnRightClicked() => Orient(CameraBaseScreenEdge.Right);

        static void Orient(CameraBaseScreenEdge edge)
        {
            var pan = GameplayCameraPanController.Current;
            var runtime = MatchRuntime.Current;
            if (pan == null || runtime?.Controller?.Layout == null)
            {
                return;
            }

            var slot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
            var layout = runtime.Controller.Layout;
            if (slot < 0 || slot >= layout.Slots.Count)
            {
                return;
            }

            var basePosition = GameplayCameraSettings.GetPlayerBaseFocusPosition(layout, slot);
            pan.OrientBaseToScreenEdge(basePosition, Vector3.zero, edge);
        }
    }
}
