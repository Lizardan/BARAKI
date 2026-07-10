using System;
using UnityEngine.UIElements;

namespace UniRx
{
    public static class UIToolkitTriggerExtensions
    {
        public static IObservable<PointerDownEvent> OnPointerDownAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<PointerDownEvent>();
        }

        public static IObservable<PointerUpEvent> OnPointerUpAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<PointerUpEvent>();
        }

        public static IObservable<ClickEvent> OnPointerClickAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<ClickEvent>();
        }

        public static IObservable<PointerEnterEvent> OnPointerEnterAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<PointerEnterEvent>();
        }

        public static IObservable<PointerLeaveEvent> OnPointerExitAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<PointerLeaveEvent>();
        }

        public static IObservable<FocusInEvent> OnSelectAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<FocusInEvent>();
        }

        public static IObservable<FocusOutEvent> OnDeselectAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<FocusOutEvent>();
        }

        public static IObservable<NavigationSubmitEvent> OnSubmitAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<NavigationSubmitEvent>();
        }

        public static IObservable<NavigationCancelEvent> OnCancelAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<NavigationCancelEvent>();
        }

        public static IObservable<WheelEvent> OnScrollAsObservable(this VisualElement element)
        {
            return element.RegisterCallbackAsObservable<WheelEvent>();
        }
    }
}
