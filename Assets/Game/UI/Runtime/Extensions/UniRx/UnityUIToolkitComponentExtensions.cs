using System;
using UnityEngine.UIElements;

namespace UniRx
{
    public static partial class UnityUIToolkitComponentExtensions
    {
        public static IDisposable SubscribeToText(this IObservable<string> source, TextElement text)
        {
            if (source is IReadOnlyReactiveProperty<string> reactiveProperty)
            {
                text.text = reactiveProperty.Value;
            }

            return source.ObserveOnUiThread().Subscribe(value => text.text = value);
        }

        public static IDisposable SubscribeToText<T>(this IObservable<T> source, TextElement text)
        {
            if (source is IReadOnlyReactiveProperty<T> reactiveProperty)
            {
                text.text = reactiveProperty.Value?.ToString();
            }

            return source.ObserveOnUiThread().Subscribe(value => text.text = value.ToString());
        }

        public static IDisposable SubscribeToText<T>(
            this IObservable<T> source,
            TextElement text,
            Func<T, string> selector)
        {
            return source.ObserveOnUiThread().Subscribe(value => text.text = selector(value));
        }

        public static IDisposable SubscribeToEnabled(this IObservable<bool> source, VisualElement element)
        {
            return source.ObserveOnUiThread().DistinctUntilChanged().Subscribe(element.SetEnabled);
        }

        public static IObservable<Unit> OnClickAsObservable(this Button button)
        {
            if (button == null)
            {
                return Observable.Empty<Unit>();
            }

            return Observable.FromEvent(
                handler => button.clicked += handler,
                handler => button.clicked -= handler).Select(_ => Unit.Default);
        }
    }
}
