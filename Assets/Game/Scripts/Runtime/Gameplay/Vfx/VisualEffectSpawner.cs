using UnityEngine;
using UnityEngine.VFX;

namespace Game.Gameplay.Vfx
{
    /// <summary>
    /// Spawns and plays VFX Graph effects via VisualEffect (no ParticleSystem).
    /// </summary>
    public sealed class VisualEffectSpawner : MonoBehaviour
    {
        [SerializeField] private VisualEffect _effectPrefab;
        [SerializeField] private Transform _spawnPoint;

        public VisualEffect Play()
        {
            if (_effectPrefab == null)
            {
                return null;
            }

            var point = _spawnPoint != null ? _spawnPoint : transform;
            var instance = Instantiate(_effectPrefab, point.position, point.rotation, point);
            instance.Play();
            return instance;
        }

        public VisualEffect PlayAt(Vector3 position, Quaternion rotation)
        {
            if (_effectPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(_effectPrefab, position, rotation);
            instance.Play();
            return instance;
        }
    }
}
