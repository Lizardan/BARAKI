using Game.Gameplay.Cameras;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Keeps the key directional light screen-relative when the gameplay camera yaws,
    /// so specular/shadow look stays stable across compass turns.
    /// </summary>
    public sealed class GameplayCameraYawLightFollower : MonoBehaviour
    {
        [SerializeField] Transform _lightTransform;

        Quaternion _baseRotation;
        float _lastAppliedYaw = float.NaN;

        void Awake()
        {
            if (_lightTransform == null)
            {
                _lightTransform = transform;
            }

            _baseRotation = _lightTransform.rotation;
        }

        void OnDisable()
        {
            if (_lightTransform != null)
            {
                _lightTransform.rotation = _baseRotation;
            }

            _lastAppliedYaw = float.NaN;
        }

        void LateUpdate()
        {
            var pan = GameplayCameraPanController.Current;
            var yaw = pan != null ? pan.YawDegrees : 0f;
            if (!float.IsNaN(_lastAppliedYaw) && Mathf.Approximately(_lastAppliedYaw, yaw))
            {
                return;
            }

            _lastAppliedYaw = yaw;
            _lightTransform.rotation = Quaternion.Euler(0f, yaw, 0f) * _baseRotation;
        }
    }
}
