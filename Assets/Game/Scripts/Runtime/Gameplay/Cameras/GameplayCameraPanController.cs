using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Cameras
{
    /// <summary>
    /// RTS camera on the follow target: edge-scroll pan, arrow-key pan, and smooth mouse-wheel zoom.
    /// </summary>
    public sealed class GameplayCameraPanController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float _edgeThresholdPixels = GameplayCameraSettings.DefaultEdgeScrollThresholdPixels;
        [SerializeField] private float _panSpeed = GameplayCameraSettings.DefaultPanSpeed;
        [SerializeField] private float _boundsRadius = GameplayCameraSettings.DefaultPanBoundsRadius;

        [Header("Zoom")]
        [SerializeField] private CinemachineFollow _cinemachineFollow;
        [SerializeField] private float _minZoomDistance = GameplayCameraSettings.DefaultMinZoomDistance;
        [SerializeField] private float _maxZoomDistance = GameplayCameraSettings.DefaultMaxZoomDistance;
        [SerializeField] private float _zoomScrollSpeed = GameplayCameraSettings.DefaultZoomScrollSpeed;
        [SerializeField] private float _zoomSmoothTime = GameplayCameraSettings.DefaultZoomSmoothTime;

        [Header("Focus")]
        [SerializeField] private float _focusMoveSpeed = GameplayCameraSettings.DefaultFocusMoveSpeed;

        private float _zoomDistance;
        private float _targetZoomDistance;
        private float _zoomVelocity;
        private bool _hasFocusTarget;
        private Vector3 _focusTarget;
        private bool _externalPanLock;

        public bool IsPanLocked => _hasFocusTarget || _externalPanLock;

        public void SetPanInputLocked(bool locked)
        {
            _externalPanLock = locked;
        }

        private void Awake()
        {
            if (_cinemachineFollow == null)
            {
                var virtualCamera = FindAnyObjectByType<CinemachineCamera>();
                if (virtualCamera != null)
                {
                    _cinemachineFollow = virtualCamera.GetComponent<CinemachineFollow>();
                }
            }

            if (_cinemachineFollow != null)
            {
                _zoomDistance = GameplayCameraSettings.GetZoomDistanceFromFollowOffset(_cinemachineFollow.FollowOffset);
            }
            else
            {
                _zoomDistance = GameplayCameraSettings.DefaultZoomDistance;
            }

            _zoomDistance = GameplayCameraSettings.ClampZoomDistance(
                _zoomDistance,
                _minZoomDistance,
                _maxZoomDistance);
            _targetZoomDistance = _zoomDistance;
            ApplyZoom();
        }

        private void Update()
        {
            if (!Application.isFocused)
            {
                return;
            }

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null)
            {
                ApplyScrollZoomTarget(mouse.scroll.ReadValue().y);
            }

            UpdateSmoothZoom();
            UpdateFocus();

            if (!IsPanLocked)
            {
                ApplyPan(mouse, keyboard);
            }
        }

        /// <summary>Moves camera to world XZ; pan input is blocked until arrival.</summary>
        public void FocusOnPosition(Vector3 worldPosition)
        {
            _focusTarget = GameplayCameraSettings.ClampPanPosition(worldPosition, _boundsRadius);
            _hasFocusTarget = true;
        }

        private void UpdateFocus()
        {
            if (!_hasFocusTarget)
            {
                return;
            }

            var current = transform.position;
            var target = new Vector3(_focusTarget.x, current.y, _focusTarget.z);
            var next = Vector3.MoveTowards(current, target, _focusMoveSpeed * Time.deltaTime);
            transform.position = next;

            if ((next - target).sqrMagnitude <= 0.0001f)
            {
                transform.position = target;
                _hasFocusTarget = false;
            }
        }

        private void ApplyScrollZoomTarget(float scrollDelta)
        {
            if (_cinemachineFollow == null || Mathf.Abs(scrollDelta) < 0.01f)
            {
                return;
            }

            _targetZoomDistance -= scrollDelta * _zoomScrollSpeed;
            _targetZoomDistance = GameplayCameraSettings.ClampZoomDistance(
                _targetZoomDistance,
                _minZoomDistance,
                _maxZoomDistance);
        }

        private void UpdateSmoothZoom()
        {
            if (_cinemachineFollow == null)
            {
                return;
            }

            _zoomDistance = Mathf.SmoothDamp(
                _zoomDistance,
                _targetZoomDistance,
                ref _zoomVelocity,
                _zoomSmoothTime);

            ApplyZoom();
        }

        private void ApplyPan(Mouse mouse, Keyboard keyboard)
        {
            var edgeInput = mouse != null
                ? GameplayCameraSettings.ReadEdgeScrollInput(
                    mouse.position.ReadValue(),
                    _edgeThresholdPixels)
                : Vector2.zero;
            var keyboardInput = GameplayCameraSettings.ReadKeyboardPanInput(keyboard);
            var panInput = GameplayCameraSettings.CombinePanInput(edgeInput, keyboardInput);

            if (panInput.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var worldDirection = GameplayCameraSettings.ComputeEdgePanDirection(camera, panInput);
            var delta = worldDirection * (_panSpeed * Time.deltaTime);
            transform.position = GameplayCameraSettings.ClampPanPosition(transform.position + delta, _boundsRadius);
        }

        private void ApplyZoom()
        {
            if (_cinemachineFollow == null)
            {
                return;
            }

            _cinemachineFollow.FollowOffset = GameplayCameraSettings.FollowOffsetFromZoomDistance(_zoomDistance);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_maxZoomDistance < _minZoomDistance)
            {
                _maxZoomDistance = _minZoomDistance;
            }

            _zoomSmoothTime = Mathf.Max(0.01f, _zoomSmoothTime);
            _focusMoveSpeed = Mathf.Max(1f, _focusMoveSpeed);
        }
#endif
    }
}
