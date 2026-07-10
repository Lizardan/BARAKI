using System;
using UnityEngine;
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

        public static IObservable<bool> OnValueChangedAsObservable(this Toggle toggle)
        {
            if (toggle == null)
            {
                return Observable.Empty<bool>();
            }

            return Observable.CreateWithState<bool, Toggle>(toggle, (t, observer) =>
            {
                observer.OnNext(t.value);
                EventCallback<ChangeEvent<bool>> handler = evt => observer.OnNext(evt.newValue);
                t.RegisterValueChangedCallback(handler);
                return Disposable.Create(() => t.UnregisterValueChangedCallback(handler));
            });
        }

        public static IObservable<float> OnValueChangedAsObservable(this Slider slider)
        {
            if (slider == null)
            {
                return Observable.Empty<float>();
            }

            return Observable.CreateWithState<float, Slider>(slider, (s, observer) =>
            {
                observer.OnNext(s.value);
                EventCallback<ChangeEvent<float>> handler = evt => observer.OnNext(evt.newValue);
                s.RegisterValueChangedCallback(handler);
                return Disposable.Create(() => s.UnregisterValueChangedCallback(handler));
            });
        }

        public static IObservable<string> OnValueChangedAsObservable(this TextField textField)
        {
            if (textField == null)
            {
                return Observable.Empty<string>();
            }

            return Observable.CreateWithState<string, TextField>(textField, (field, observer) =>
            {
                observer.OnNext(field.value);
                EventCallback<ChangeEvent<string>> handler = evt => observer.OnNext(evt.newValue);
                field.RegisterValueChangedCallback(handler);
                return Disposable.Create(() => field.UnregisterValueChangedCallback(handler));
            });
        }

        public static IObservable<string> OnEndEditAsObservable(this TextField textField)
        {
            if (textField == null)
            {
                return Observable.Empty<string>();
            }

            return Observable.CreateWithState<string, TextField>(textField, (field, observer) =>
            {
                EventCallback<FocusOutEvent> handler = _ => observer.OnNext(field.value);
                field.RegisterCallback(handler);
                return Disposable.Create(() => field.UnregisterCallback(handler));
            });
        }

        public static IObservable<string> OnValueChangedAsObservable(this DropdownField dropdown)
        {
            if (dropdown == null)
            {
                return Observable.Empty<string>();
            }

            return Observable.CreateWithState<string, DropdownField>(dropdown, (field, observer) =>
            {
                observer.OnNext(field.value);
                EventCallback<ChangeEvent<string>> handler = evt => observer.OnNext(evt.newValue);
                field.RegisterValueChangedCallback(handler);
                return Disposable.Create(() => field.UnregisterValueChangedCallback(handler));
            });
        }

        public static IObservable<int> OnIndexChangedAsObservable(this DropdownField dropdown)
        {
            if (dropdown == null)
            {
                return Observable.Empty<int>();
            }

            return Observable.CreateWithState<int, DropdownField>(dropdown, (field, observer) =>
            {
                observer.OnNext(field.index);
                EventCallback<ChangeEvent<string>> handler = _ => observer.OnNext(field.index);
                field.RegisterValueChangedCallback(handler);
                return Disposable.Create(() => field.UnregisterValueChangedCallback(handler));
            });
        }

        public static IObservable<Vector2> OnValueChangedAsObservable(this ScrollView scrollView)
        {
            if (scrollView == null)
            {
                return Observable.Empty<Vector2>();
            }

            return Observable.CreateWithState<Vector2, ScrollView>(scrollView, (view, observer) =>
            {
                observer.OnNext(view.scrollOffset);
                EventCallback<GeometryChangedEvent> handler = _ => observer.OnNext(view.scrollOffset);
                view.RegisterCallback(handler);
                return Disposable.Create(() => view.UnregisterCallback(handler));
            });
        }
    }
}
