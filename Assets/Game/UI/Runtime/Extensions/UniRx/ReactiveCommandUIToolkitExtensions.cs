using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace UniRx
{
    public static class ReactiveCommandUIToolkitExtensions
    {
        public static IDisposable BindTo(this IReactiveCommand<Unit> command, Button button)
        {
            var enabledBinding = command.CanExecute.SubscribeToEnabled(button);
            var clickBinding = button.OnClickAsObservable()
                .SubscribeWithState(command, (_, c) => c.Execute(Unit.Default));
            return StableCompositeDisposable.Create(enabledBinding, clickBinding);
        }

        public static IDisposable BindToOnClick(
            this IReactiveCommand<Unit> command,
            Button button,
            Action<Unit> onClick)
        {
            var enabledBinding = command.CanExecute.SubscribeToEnabled(button);
            var clickBinding = button.OnClickAsObservable()
                .SubscribeWithState(command, (_, c) => c.Execute(Unit.Default));
            var actionBinding = command.Subscribe(onClick);
            return StableCompositeDisposable.Create(enabledBinding, clickBinding, actionBinding);
        }

        public static IDisposable BindToButtonOnClick(
            this IObservable<bool> canExecuteSource,
            Button button,
            Action<Unit> onClick,
            bool initialValue = true)
        {
            return canExecuteSource.ToReactiveCommand(initialValue).BindToOnClick(button, onClick);
        }

        public static IDisposable BindTo(this IAsyncReactiveCommand<Unit> command, Button button)
        {
            var enabledBinding = command.CanExecute.SubscribeToEnabled(button);
            var clickBinding = button.OnClickAsObservable()
                .SubscribeWithState(command, (_, c) => c.Execute(Unit.Default));
            return StableCompositeDisposable.Create(enabledBinding, clickBinding);
        }

        public static IDisposable BindToOnClick(
            this Button button,
            Func<CancellationToken, UniTask> onClick,
            CancellationToken cancellationToken)
        {
            return button.OnClickAsObservable().Subscribe(_ =>
            {
                onClick(cancellationToken).Forget();
            });
        }
    }
}
