using System;
using System.Threading;
using UnityEngine.UIElements;

namespace Cysharp.Threading.Tasks
{
    public static partial class UnityAsyncExtensions
    {
        public static UniTask OnClickAsync(this Button button, CancellationToken cancellationToken)
        {
            if (button == null)
            {
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();

            void OnClick()
            {
                button.clicked -= OnClick;
                tcs.TrySetResult();
            }

            button.clicked += OnClick;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    button.clicked -= OnClick;
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }

        public static UniTask<bool> OnValueChangedAsync(this Toggle toggle, CancellationToken cancellationToken)
        {
            return WaitForValueChanged<Toggle, bool>(toggle, cancellationToken);
        }

        public static UniTask<float> OnValueChangedAsync(this Slider slider, CancellationToken cancellationToken)
        {
            return WaitForValueChanged<Slider, float>(slider, cancellationToken);
        }

        public static UniTask<string> OnValueChangedAsync(this TextField textField, CancellationToken cancellationToken)
        {
            return WaitForValueChanged<TextField, string>(textField, cancellationToken);
        }

        public static UniTask<string> OnEndEditAsync(this TextField textField, CancellationToken cancellationToken)
        {
            if (textField == null)
            {
                return UniTask.FromResult(string.Empty);
            }

            var tcs = new UniTaskCompletionSource<string>();
            EventCallback<FocusOutEvent> handler = null;
            handler = _ =>
            {
                textField.UnregisterCallback(handler);
                tcs.TrySetResult(textField.value);
            };

            textField.RegisterCallback(handler);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    textField.UnregisterCallback(handler);
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }

        public static UniTask<string> OnValueChangedAsync(this DropdownField dropdown, CancellationToken cancellationToken)
        {
            return WaitForValueChanged<DropdownField, string>(dropdown, cancellationToken);
        }

        public static UniTask<UnityEngine.Vector2> OnValueChangedAsync(
            this ScrollView scrollView,
            CancellationToken cancellationToken)
        {
            if (scrollView == null)
            {
                return UniTask.FromResult(UnityEngine.Vector2.zero);
            }

            var tcs = new UniTaskCompletionSource<UnityEngine.Vector2>();
            EventCallback<GeometryChangedEvent> handler = null;
            handler = _ =>
            {
                scrollView.UnregisterCallback(handler);
                tcs.TrySetResult(scrollView.scrollOffset);
            };

            scrollView.RegisterCallback(handler);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    scrollView.UnregisterCallback(handler);
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }

        private static UniTask<TValue> WaitForValueChanged<TElement, TValue>(
            TElement element,
            CancellationToken cancellationToken)
            where TElement : VisualElement, INotifyValueChanged<TValue>
        {
            if (element == null)
            {
                return UniTask.FromResult(default(TValue));
            }

            var tcs = new UniTaskCompletionSource<TValue>();
            EventCallback<ChangeEvent<TValue>> handler = null;
            handler = evt =>
            {
                element.UnregisterValueChangedCallback(handler);
                tcs.TrySetResult(evt.newValue);
            };

            element.RegisterValueChangedCallback(handler);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    element.UnregisterValueChangedCallback(handler);
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }
    }
}
