using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Animations
{
    /// <summary>
    /// Lightweight UI Toolkit tweens inspired by Airy UI entrance animations.
    /// </summary>
    public static class UiToolkitElementAnimator
    {
        public static async UniTask FadeAsync(
            VisualElement element,
            float from,
            float to,
            float duration,
            float delay = 0f,
            CancellationToken cancellationToken = default)
        {
            if (element == null || duration <= 0f)
            {
                if (element != null)
                {
                    element.style.opacity = to;
                }

                return;
            }

            if (delay > 0f)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            element.style.opacity = from;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                element.style.opacity = Mathf.Lerp(from, to, EaseOutCubic(t));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            element.style.opacity = to;
        }

        public static async UniTask TranslateAsync(
            VisualElement element,
            Vector2 from,
            Vector2 to,
            float duration,
            float delay = 0f,
            CancellationToken cancellationToken = default)
        {
            if (element == null || duration <= 0f)
            {
                if (element != null)
                {
                    element.style.translate = new Translate(to.x, to.y);
                }

                return;
            }

            if (delay > 0f)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            element.style.translate = new Translate(from.x, from.y);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                var position = Vector2.Lerp(from, to, eased);
                element.style.translate = new Translate(position.x, position.y);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            element.style.translate = new Translate(to.x, to.y);
        }

        public static async UniTask ScaleAsync(
            VisualElement element,
            Vector2 from,
            Vector2 to,
            float duration,
            float delay = 0f,
            bool bounce = false,
            CancellationToken cancellationToken = default)
        {
            if (element == null || duration <= 0f)
            {
                if (element != null)
                {
                    element.style.scale = new Scale(new Vector3(to.x, to.y, 1f));
                }

                return;
            }

            if (delay > 0f)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            element.style.scale = new Scale(new Vector3(from.x, from.y, 1f));
            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = bounce ? EaseOutBack(t) : EaseOutCubic(t);
                var scale = Vector2.Lerp(from, to, eased);
                element.style.scale = new Scale(new Vector3(scale.x, scale.y, 1f));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            element.style.scale = new Scale(new Vector3(to.x, to.y, 1f));
        }

        public static async UniTask FadeTranslateAsync(
            VisualElement element,
            float fromOpacity,
            float toOpacity,
            Vector2 fromTranslate,
            Vector2 toTranslate,
            float duration,
            float delay = 0f,
            CancellationToken cancellationToken = default)
        {
            if (element == null)
            {
                return;
            }

            if (delay > 0f)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            var elapsed = 0f;
            element.style.opacity = fromOpacity;
            element.style.translate = new Translate(fromTranslate.x, fromTranslate.y);

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                element.style.opacity = Mathf.Lerp(fromOpacity, toOpacity, eased);
                var position = Vector2.Lerp(fromTranslate, toTranslate, eased);
                element.style.translate = new Translate(position.x, position.y);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            element.style.opacity = toOpacity;
            element.style.translate = new Translate(toTranslate.x, toTranslate.y);
        }

        public static async UniTask FadeScaleAsync(
            VisualElement element,
            float fromOpacity,
            float toOpacity,
            Vector2 fromScale,
            Vector2 toScale,
            float duration,
            float delay = 0f,
            bool bounce = false,
            CancellationToken cancellationToken = default)
        {
            if (element == null)
            {
                return;
            }

            if (delay > 0f)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            var elapsed = 0f;
            element.style.opacity = fromOpacity;
            element.style.scale = new Scale(new Vector3(fromScale.x, fromScale.y, 1f));

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = bounce ? EaseOutBack(t) : EaseOutCubic(t);
                element.style.opacity = Mathf.Lerp(fromOpacity, toOpacity, eased);
                var scale = Vector2.Lerp(fromScale, toScale, eased);
                element.style.scale = new Scale(new Vector3(scale.x, scale.y, 1f));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            element.style.opacity = toOpacity;
            element.style.scale = new Scale(new Vector3(toScale.x, toScale.y, 1f));
        }

        public static async UniTask StaggerFadeScaleAsync(
            IReadOnlyList<VisualElement> elements,
            float fromOpacity,
            float toOpacity,
            Vector2 fromScale,
            Vector2 toScale,
            float duration,
            float staggerDelay,
            bool bounce = false,
            CancellationToken cancellationToken = default)
        {
            if (elements == null || elements.Count == 0)
            {
                return;
            }

            var tasks = new List<UniTask>(elements.Count);
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null)
                {
                    continue;
                }

                tasks.Add(FadeScaleAsync(
                    element,
                    fromOpacity,
                    toOpacity,
                    fromScale,
                    toScale,
                    duration,
                    staggerDelay * i,
                    bounce,
                    cancellationToken));
            }

            await UniTask.WhenAll(tasks);
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
