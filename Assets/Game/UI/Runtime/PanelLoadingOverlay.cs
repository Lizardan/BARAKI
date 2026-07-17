using System;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>Inline panel cover with centered "Загрузка" text and animated dots.</summary>
    public sealed class PanelLoadingOverlay : IDisposable
    {
        private const string HiddenClass = "ui-panel-loading-overlay--hidden";
        private static readonly string[] DotFrames = { "", ".", "..", "..." };

        private readonly VisualElement _root;
        private readonly Label _dotsLabel;
        private int _frame;
        private IVisualElementScheduledItem _schedule;

        public PanelLoadingOverlay(VisualElement overlayRoot)
        {
            _root = overlayRoot;
            _dotsLabel = overlayRoot?.Q<Label>(className: "ui-panel-loading-overlay__dots");
        }

        public bool IsVisible => _root != null && !_root.ClassListContains(HiddenClass);

        public void SetVisible(bool visible)
        {
            if (_root == null)
            {
                return;
            }

            if (visible)
            {
                _root.RemoveFromClassList(HiddenClass);
                StartDots();
            }
            else
            {
                _root.AddToClassList(HiddenClass);
                StopDots();
            }
        }

        private void StartDots()
        {
            StopDots();
            _frame = 0;
            TickDots();
            _schedule = _root.schedule.Execute(TickDots).Every(380);
        }

        private void TickDots()
        {
            if (_dotsLabel == null)
            {
                return;
            }

            _dotsLabel.text = DotFrames[_frame % DotFrames.Length];
            _frame++;
        }

        private void StopDots()
        {
            _schedule?.Pause();
            _schedule = null;

            if (_dotsLabel != null)
            {
                _dotsLabel.text = string.Empty;
            }
        }

        public void Dispose()
        {
            StopDots();
        }
    }
}
