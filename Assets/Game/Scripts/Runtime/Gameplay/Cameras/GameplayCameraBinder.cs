using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Cameras
{
    /// <summary>
    /// Assigns a follow target to a Cinemachine virtual camera (CM3).
    /// </summary>
    public sealed class GameplayCameraBinder : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _virtualCamera;
        [SerializeField] private GameplayCameraTarget _defaultTarget;

        private void Start()
        {
            if (_defaultTarget != null)
            {
                Bind(_defaultTarget);
            }
        }

        public void Bind(GameplayCameraTarget target)
        {
            if (_virtualCamera == null || target == null)
            {
                return;
            }

            var tracking = target.transform;
            _virtualCamera.Target.TrackingTarget = tracking;
            _virtualCamera.Target.LookAtTarget = tracking;
        }

        public void Bind(Transform target)
        {
            if (_virtualCamera == null || target == null)
            {
                return;
            }

            _virtualCamera.Target.TrackingTarget = target;
            _virtualCamera.Target.LookAtTarget = target;
        }
    }
}
