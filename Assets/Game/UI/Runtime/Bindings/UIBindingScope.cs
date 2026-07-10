using System;
using UniRx;
using UnityEngine.UIElements;

namespace Game.UI.Bindings
{
    /// <summary>
    /// Collects UI subscriptions and disposes them when the root element detaches or scope is disposed.
    /// </summary>
    public sealed class UIBindingScope : IDisposable
    {
        private readonly CompositeDisposable _disposables = new();
        private readonly VisualElement _root;
        private bool _isDisposed;

        public UIBindingScope(VisualElement root)
        {
            _root = root;
            if (_root != null)
            {
                _root.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            }
        }

        public void Add(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            if (_isDisposed)
            {
                disposable.Dispose();
                return;
            }

            _disposables.Add(disposable);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_root != null && _root.panel != null)
            {
                _root.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            }

            _disposables.Dispose();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent _)
        {
            Dispose();
        }
    }
}
