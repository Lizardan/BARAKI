using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Dims the level until the player presses Play, then eases lighting in.
    /// </summary>
    public sealed class GameplayReveal : MonoBehaviour
    {
        [SerializeField] private Light _keyLight;
        [SerializeField] private float _menuIntensity = 0.22f;
        [SerializeField] private float _playIntensity = 1.15f;
        [SerializeField] private float _duration = 0.65f;

        private void Awake()
        {
            if (_keyLight == null)
            {
                TryGetComponent(out _keyLight);
            }

            if (_keyLight != null)
            {
                _keyLight.intensity = _menuIntensity;
            }
        }

        private void OnEnable()
        {
            if (GameSession.IsPlaying)
            {
                ApplyPlayIntensity();
                return;
            }

            GameSession.Started += OnSessionStarted;
        }

        private void OnDisable()
        {
            GameSession.Started -= OnSessionStarted;
        }

        private void OnSessionStarted()
        {
            RevealAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void ApplyPlayIntensity()
        {
            if (_keyLight != null)
            {
                _keyLight.intensity = _playIntensity;
            }
        }

        private async UniTask RevealAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (_keyLight == null)
            {
                return;
            }

            var start = _keyLight.intensity;
            var elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _duration));
                _keyLight.intensity = Mathf.Lerp(start, _playIntensity, t);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            _keyLight.intensity = _playIntensity;
        }
    }
}
