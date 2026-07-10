using System;
using UniRx;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UniRx
{
    internal static class UIToolkitObservableUtility
    {
        internal static IObservable<T> ObserveOnUiThread<T>(this IObservable<T> source)
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                return source;
            }
#endif
            return source.ObserveOnMainThread();
        }

        internal static IObservable<TEvent> RegisterCallbackAsObservable<TEvent>(
            this VisualElement element,
            TrickleDown trickleDown = TrickleDown.NoTrickleDown)
            where TEvent : EventBase<TEvent>, new()
        {
            if (element == null)
            {
                return Observable.Empty<TEvent>();
            }

            return Observable.FromEvent<EventCallback<TEvent>, TEvent>(
                handler => new EventCallback<TEvent>(handler),
                handler => element.RegisterCallback(handler, trickleDown),
                handler => element.UnregisterCallback(handler, trickleDown));
        }
    }
}
