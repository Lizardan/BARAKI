using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniRx
{
    public static partial class UnityUIToolkitVisualElementExtensions
    {
        public static IObservable<Unit> GeometryChangedAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<GeometryChangedEvent>().Select(_ => Unit.Default);
        }

        public static IDisposable BindVisibility(this VisualElement element, IObservable<bool> visible)
        {
            return visible.ObserveOnUiThread().DistinctUntilChanged().Subscribe(isVisible =>
            {
                element.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }

        public static IDisposable BindDisplay(this VisualElement element, IObservable<bool> flex)
        {
            return BindVisibility(element, flex);
        }
    }
}
