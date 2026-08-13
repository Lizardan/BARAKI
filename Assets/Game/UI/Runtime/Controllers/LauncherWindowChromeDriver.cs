using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Borderless launcher chrome: close button + threshold drag over the UI
    /// without swallowing Button clicks.
    /// </summary>
    public sealed class LauncherWindowChromeDriver : IDisposable
    {
        const string CloseButtonName = "CloseButton";

        VisualElement _root;
        Button _closeButton;
        bool _armed;
        bool _dragging;
        Vector2 _pressPanelPosition;
        Vector2Int _lastCursorScreen;
        int _pointerId = -1;
        Action _onClose;

        public void Attach(VisualElement root, Button closeButton, Action onClose)
        {
            Detach();
            _root = root;
            _closeButton = closeButton;
            _onClose = onClose;
            if (_root == null)
            {
                return;
            }

            _root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.NoTrickleDown);
            _root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.NoTrickleDown);
            _root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.NoTrickleDown);
            _root.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            if (_closeButton != null)
            {
                _closeButton.clicked += OnCloseClicked;
            }
        }

        public void Detach()
        {
            if (_root != null)
            {
                if (_pointerId >= 0 && _root.HasPointerCapture(_pointerId))
                {
                    _root.ReleasePointer(_pointerId);
                }

                _root.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                _root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                _root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                _root.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            if (_closeButton != null)
            {
                _closeButton.clicked -= OnCloseClicked;
            }

            ResetDragState();
            _root = null;
            _closeButton = null;
            _onClose = null;
        }

        public void Dispose() => Detach();

        void OnCloseClicked() => _onClose?.Invoke();

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _root == null)
            {
                return;
            }

            var target = evt.target as VisualElement;
            if (LauncherWindowDragRules.ShouldBlockDragArm(
                    IsTextInput(target),
                    IsCloseControl(target)))
            {
                return;
            }

            _armed = true;
            _dragging = false;
            _pressPanelPosition = evt.position;
            _pointerId = evt.pointerId;
            GameNativeWindowChrome.TryGetCursorScreenPosition(out _lastCursorScreen);
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_armed || evt.pointerId != _pointerId)
            {
                return;
            }

            if (!_dragging)
            {
                if (!LauncherWindowDragRules.ShouldBeginDrag(_pressPanelPosition, evt.position))
                {
                    return;
                }

                _dragging = true;
                _root.CapturePointer(_pointerId);
                GameNativeWindowChrome.TryGetCursorScreenPosition(out _lastCursorScreen);
            }

            if (!GameNativeWindowChrome.TryGetCursorScreenPosition(out var cursor))
            {
                return;
            }

            var delta = new Vector2Int(cursor.x - _lastCursorScreen.x, cursor.y - _lastCursorScreen.y);
            if (delta.x == 0 && delta.y == 0)
            {
                return;
            }

            if (GameNativeWindowChrome.TryMoveBy(delta))
            {
                _lastCursorScreen = cursor;
            }

            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointerId)
            {
                return;
            }

            var wasDragging = _dragging;
            if (_root != null && _root.HasPointerCapture(_pointerId))
            {
                _root.ReleasePointer(_pointerId);
            }

            ResetDragState();

            // Swallow only after a real drag so Button.clicked still fires on short presses.
            if (wasDragging)
            {
                evt.StopPropagation();
            }
        }

        void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _pointerId)
            {
                ResetDragState();
            }
        }

        void ResetDragState()
        {
            _armed = false;
            _dragging = false;
            _pointerId = -1;
        }

        static bool IsTextInput(VisualElement target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                if (current is TextField or TextElement)
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsCloseControl(VisualElement target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                if (current.name == CloseButtonName || current.ClassListContains("ln-btn--close"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
