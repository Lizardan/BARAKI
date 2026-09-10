using Game.Core;
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
        public static GameplayCameraPanController Current { get; private set; }

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
        [SerializeField] private float _minimapFocusMoveSpeed = GameplayCameraSettings.DefaultMinimapFocusMoveSpeed;
        [SerializeField] private float _minimapFocusSmoothTime = GameplayCameraSettings.DefaultMinimapFocusSmoothTime;

        [Header("Yaw")]
        [SerializeField] private float _yawSmoothTime = GameplayCameraSettings.DefaultYawSmoothTime;

        [SerializeField] private float _zoomDistance;
        private float _targetZoomDistance;
        private float _zoomVelocity;
        private float _yawDegrees;
        private float _targetYawDegrees;
        private float _yawVelocity;
        private bool _hasFocusTarget;
        private bool _minimapFocus;
        private Vector3 _focusTarget;
        private Vector3 _focusVelocity;
        private bool _externalPanLock;

        /// <summary>
        /// Арена стоит далеко за пределами игровой карты, поэтому кламп по радиусу
        /// можно снимать на время перелёта (<see cref="SetPanBoundsEnabled"/>).
        /// </summary>
        bool _panBoundsEnabled = true;

        public bool IsPanLocked => _hasFocusTarget || _externalPanLock;

        /// <summary>Текущая позиция следования камеры (XZ-якорь).</summary>
        public Vector3 PanPosition => transform.position;

        /// <summary>Включить/выключить ограничение области панорамирования.</summary>
        public void SetPanBoundsEnabled(bool enabled) => _panBoundsEnabled = enabled;

        Vector3 ClampPan(Vector3 position) =>
            _panBoundsEnabled
                ? GameplayCameraSettings.ClampPanPosition(position, _boundsRadius)
                : position;

        /// <summary>True while a scripted/minimap focus move is still in flight.</summary>
        public bool IsFocusInProgress => _hasFocusTarget;

        /// <summary>Current visual yaw (may be mid-tween toward <see cref="TargetYawDegrees"/>).</summary>
        public float YawDegrees => _yawDegrees;

        public float TargetYawDegrees => _targetYawDegrees;

        public void SetPanInputLocked(bool locked)
        {
            _externalPanLock = locked;
        }

        /// <summary>Snaps horizontal camera yaw immediately (no tween).</summary>
        public void SetYawDegrees(float yawDegrees)
        {
            _yawDegrees = yawDegrees;
            _targetYawDegrees = yawDegrees;
            _yawVelocity = 0f;
            ApplyZoom();
        }

        /// <summary>Starts a short SmoothDampAngle turn toward the given yaw.</summary>
        public void SetYawDegreesSmooth(float yawDegrees)
        {
            _targetYawDegrees = yawDegrees;
        }

        /// <summary>
        /// Rotates the camera so the base lies on the chosen screen edge
        /// (center of the arena toward the opposite edge). Uses a fast smooth turn.
        /// </summary>
        public void OrientBaseToScreenEdge(
            Vector3 baseWorldPosition,
            Vector3 arenaCenter,
            CameraBaseScreenEdge edge)
        {
            SetYawDegreesSmooth(GameplayCameraSettings.ComputeYawDegreesForBaseAtScreenEdge(
                baseWorldPosition,
                arenaCenter,
                edge));
        }

        private void Awake()
        {
            Current = this;
            if (_cinemachineFollow == null)
            {
                _cinemachineFollow = GetComponentInParent<CinemachineFollow>()
                    ?? GetComponentInChildren<CinemachineFollow>();
            }

            if (_cinemachineFollow != null)
            {
                _zoomDistance = GameplayCameraSettings.GetZoomDistanceFromFollowOffset(_cinemachineFollow.FollowOffset);
                _yawDegrees = GameplayCameraSettings.GetYawDegreesFromFollowOffset(_cinemachineFollow.FollowOffset);
            }
            else
            {
                _zoomDistance = GameplayCameraSettings.DefaultZoomDistance;
            }

            _targetYawDegrees = _yawDegrees;
            _zoomDistance = GameplayCameraSettings.ClampZoomDistance(
                _zoomDistance,
                _minZoomDistance,
                _maxZoomDistance);
            _targetZoomDistance = _zoomDistance;
            if (_minimapFocusSmoothTime < 0.01f)
            {
                _minimapFocusSmoothTime = GameplayCameraSettings.DefaultMinimapFocusSmoothTime;
            }

            if (_minimapFocusMoveSpeed < 1f)
            {
                _minimapFocusMoveSpeed = GameplayCameraSettings.DefaultMinimapFocusMoveSpeed;
            }

            if (_yawSmoothTime < 0.01f)
            {
                _yawSmoothTime = GameplayCameraSettings.DefaultYawSmoothTime;
            }

            ApplyZoom();
        }

        private void Update()
        {
            UpdateFocus();
            UpdateSmoothYaw();

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

            if (!IsPanLocked)
            {
                ApplyPan(mouse, keyboard);
            }
        }

        /// <summary>
        /// Moves camera to world XZ at the normal focus speed (match start / scripted focus).
        /// Pan input is blocked until arrival.
        /// </summary>
        public void FocusOnPosition(Vector3 worldPosition)
        {
            _focusTarget = ClampPan(worldPosition);
            _hasFocusTarget = true;
            _minimapFocus = false;
            _focusVelocity = Vector3.zero;
        }

        /// <summary>Fast smooth chase used while dragging/clicking the minimap.</summary>
        public void FocusOnMinimapPosition(Vector3 worldPosition)
        {
            _focusTarget = ClampPan(worldPosition);
            _hasFocusTarget = true;
            _minimapFocus = true;
        }

        /// <summary>Snaps camera to world XZ immediately and cancels any in-flight focus.</summary>
        public void SetPanPosition(Vector3 worldPosition)
        {
            _hasFocusTarget = false;
            _minimapFocus = false;
            _focusVelocity = Vector3.zero;
            var clamped = ClampPan(worldPosition);
            transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
        }

        private void UpdateFocus()
        {
            if (!_hasFocusTarget)
            {
                return;
            }

            var current = transform.position;
            var target = new Vector3(_focusTarget.x, current.y, _focusTarget.z);

            Vector3 next;
            if (_minimapFocus)
            {
                next = Vector3.SmoothDamp(
                    current,
                    target,
                    ref _focusVelocity,
                    Mathf.Max(0.01f, _minimapFocusSmoothTime),
                    _minimapFocusMoveSpeed,
                    Time.deltaTime);
                next.y = current.y;
                _focusVelocity.y = 0f;
            }
            else
            {
                next = Vector3.MoveTowards(current, target, _focusMoveSpeed * Time.deltaTime);
                _focusVelocity = Vector3.zero;
            }

            transform.position = next;

            var arrived = _minimapFocus
                ? (next - target).sqrMagnitude <= 0.0025f && _focusVelocity.sqrMagnitude <= 0.25f
                : (next - target).sqrMagnitude <= 0.0001f;

            if (arrived)
            {
                transform.position = target;
                _focusVelocity = Vector3.zero;
                _hasFocusTarget = false;
                _minimapFocus = false;
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

        private void UpdateSmoothYaw()
        {
            if (Mathf.Abs(Mathf.DeltaAngle(_yawDegrees, _targetYawDegrees)) < 0.01f
                && Mathf.Abs(_yawVelocity) < 0.01f)
            {
                if (!Mathf.Approximately(_yawDegrees, _targetYawDegrees))
                {
                    _yawDegrees = _targetYawDegrees;
                    _yawVelocity = 0f;
                    ApplyZoom();
                }

                return;
            }

            _yawDegrees = Mathf.SmoothDampAngle(
                _yawDegrees,
                _targetYawDegrees,
                ref _yawVelocity,
                Mathf.Max(0.01f, _yawSmoothTime),
                float.PositiveInfinity,
                Time.unscaledDeltaTime);
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

            var camera = CameraCache.Main;
            if (camera == null)
            {
                return;
            }

            var worldDirection = GameplayCameraSettings.ComputeEdgePanDirection(camera, panInput);
            var delta = worldDirection * (_panSpeed * Time.deltaTime);
            transform.position = ClampPan(transform.position + delta);
        }

        private void ApplyZoom()
        {
            if (_cinemachineFollow == null)
            {
                return;
            }

            _cinemachineFollow.FollowOffset = GameplayCameraSettings.FollowOffsetFromZoomDistance(
                _zoomDistance,
                _yawDegrees);
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_maxZoomDistance < _minZoomDistance)
            {
                _maxZoomDistance = _minZoomDistance;
            }

            _zoomSmoothTime = Mathf.Max(0.01f, _zoomSmoothTime);
            _yawSmoothTime = Mathf.Max(0.01f, _yawSmoothTime);
            _focusMoveSpeed = Mathf.Max(1f, _focusMoveSpeed);
            _minimapFocusMoveSpeed = Mathf.Max(1f, _minimapFocusMoveSpeed);
            _minimapFocusSmoothTime = Mathf.Max(0.01f, _minimapFocusSmoothTime);
        }
#endif
    }
}
