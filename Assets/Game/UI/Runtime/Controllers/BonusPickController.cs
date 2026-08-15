using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Post-race-pick bonus overlay (PRE-001). Shows the 12 bonus slots + countdown while the
    /// pick window is open and hides as soon as the local player picks or the deadline expires.
    /// Backdrop is picking-mode Ignore — the overlay never blocks pan camera / unit controls.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BonusPickController : MonoBehaviour
    {
        private const string HiddenClass = "bonus-pick--hidden";
        private const string PickedClass = "bonus-pick__slot--picked";

        [SerializeField] private UIDocument _uiDocument;

        private MatchRuntime _matchRuntime;
        private VisualElement _overlay;
        private Label _timerLabel;
        private readonly List<(Button button, int bonusSlot)> _slotButtons = new();

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _overlay = root.Q<VisualElement>("BonusPickOverlay");
            _timerLabel = root.Q<Label>("BonusPickTimer");
            BuildSlotButtons(root.Q<VisualElement>("BonusPickGrid"));
        }

        private void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }
        }

        private void LateUpdate()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null || !_matchRuntime.IsMatchStarted)
            {
                Hide();
                return;
            }

            var localSlot = ResolveLocalSlot();
            BonusPickHudRules.ResolveOverlay(
                useSnapshot: _matchRuntime.TickMode == MatchTickMode.Client,
                snapshot: _matchRuntime.LastNetworkSnapshot,
                localSlot,
                controller.BonusPickDeadlineSeconds,
                controller.GetBonusPickSlot(localSlot),
                out var deadline,
                out var ownPick);

            if (!BonusPickHudRules.IsPickWindowOpen(deadline, ownPick))
            {
                Hide();
                return;
            }

            _timerLabel.text = Mathf.CeilToInt(deadline).ToString();
            UpdateSlotButtons(ownPick);
            Show();
        }

        private void OnSlotClicked(int bonusSlot)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null)
            {
                return;
            }

            if (BonusPickNetworkFacade.TryRequestPick(bonusSlot))
            {
                return;
            }

            // Offline / single-player: apply straight to the local controller.
            controller.TrySetBonusPick(ResolveLocalSlot(), bonusSlot);
        }

        private void UpdateSlotButtons(int ownPick)
        {
            var locked = ownPick != BonusPickRules.NoneSlot;
            foreach (var (button, bonusSlot) in _slotButtons)
            {
                button.SetEnabled(!locked);
                button.EnableInClassList(PickedClass, ownPick == bonusSlot);
            }
        }

        private void BuildSlotButtons(VisualElement grid)
        {
            if (grid == null)
            {
                return;
            }

            foreach (var slot in BonusPickRules.DisplayOrderSlots)
            {
                var button = new Button(() => OnSlotClicked(slot))
                {
                    name = $"BonusSlot{slot}",
                    text = BonusPickRules.GetSlotDisplayName(slot),
                };
                button.AddToClassList("ui-btn");
                button.AddToClassList("ui-btn--square");
                button.AddToClassList("bonus-pick__slot");
                grid.Add(button);
                _slotButtons.Add((button, slot));
            }
        }

        private static int ResolveLocalSlot()
        {
            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            return setup.LocalPlayerSlot;
        }

        private void Show()
        {
            _overlay?.RemoveFromClassList(HiddenClass);
        }

        private void Hide()
        {
            _overlay?.AddToClassList(HiddenClass);
        }
    }
}
