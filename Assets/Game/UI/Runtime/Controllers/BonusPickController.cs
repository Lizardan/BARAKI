using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Post-race-pick bonus overlay (PRE-001 / PRE-006). Shows the 12 bonus slots + countdown while the
    /// pick window is open and hides as soon as the local player picks or the deadline expires.
    /// Slots 1–10 show portraits + tooltips; race uniques 11–12 show name + tooltip only.
    /// Backdrop is picking-mode Ignore — the overlay never blocks pan camera / unit controls.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BonusPickController : MonoBehaviour
    {
        private const string HiddenClass = "bonus-pick--hidden";
        private const string PickedClass = "bonus-pick__slot--picked";
        private const string LockedClass = "bonus-pick__slot--locked";
        private const string TooltipHiddenClass = "bonus-pick-tooltip--hidden";

        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private UnitVisualCatalog _visualCatalog;

        private MatchRuntime _matchRuntime;
        private VisualElement _overlay;
        private Label _timerLabel;
        private VisualElement _tooltip;
        private Label _tooltipLabel;
        private readonly List<(Button button, int bonusSlot)> _slotButtons = new();
        private int _hoveredSlot = BonusPickRules.NoneSlot;
        private bool _portraitsApplied;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _overlay = root.Q<VisualElement>("BonusPickOverlay");
            _timerLabel = root.Q<Label>("BonusPickTimer");
            _tooltip = root.Q<VisualElement>("BonusPickTooltip");
            _tooltipLabel = root.Q<Label>("BonusPickTooltipLabel");
            BuildSlotButtons(root.Q<VisualElement>("BonusPickGrid"));

            foreach (var (button, bonusSlot) in _slotButtons)
            {
                var slot = bonusSlot;
                var anchor = button;
                button.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(slot, anchor));
                button.RegisterCallback<PointerLeaveEvent>(_ => HideTooltipIfSlot(slot));
            }
        }

        private void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            ResolveVisualCatalog();
        }

        private void OnDisable()
        {
            HideTooltip();
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

            RefreshPortraitsIfNeeded();
            _timerLabel.text = Mathf.CeilToInt(deadline).ToString();
            UpdateSlotButtons(ownPick);
            Show();
        }

        private void ResolveVisualCatalog()
        {
            if (_visualCatalog != null)
            {
                return;
            }

            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller != null && controller.UnitVisualCatalog != null)
            {
                _visualCatalog = controller.UnitVisualCatalog;
                return;
            }

            var presenter = MatchCombatPresenter.Current;
            _visualCatalog = presenter != null ? presenter.VisualCatalog : null;
        }

        private void RefreshPortraitsIfNeeded()
        {
            ResolveVisualCatalog();
            if (_visualCatalog == null || _portraitsApplied)
            {
                return;
            }

            var raceId = ResolveLocalRaceId();
            var anyApplied = false;
            foreach (var (button, slot) in _slotButtons)
            {
                if (!HumanBonusUnitRules.IsBonusSlot(slot)
                    && !HumanBonusUnitRules.IsChampionBonusSlot(slot))
                {
                    continue;
                }

                if (!_visualCatalog.TryGetBonusPortrait(raceId, slot, out var portrait) || portrait == null)
                {
                    continue;
                }

                var existing = button.Q<VisualElement>("BonusPortrait");
                if (existing == null)
                {
                    existing = new VisualElement { name = "BonusPortrait" };
                    existing.AddToClassList("bonus-pick__portrait");
                    button.Add(existing);
                }

                existing.style.backgroundImage = new StyleBackground(portrait);
                button.text = string.Empty;
                anyApplied = true;
            }

            if (anyApplied)
            {
                _portraitsApplied = true;
            }
        }

        private void OnSlotClicked(int bonusSlot)
        {
            if (!BonusPickRules.IsValidSlot(bonusSlot))
            {
                return;
            }

            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null)
            {
                return;
            }

            if (BonusPickNetworkFacade.TryRequestPick(bonusSlot))
            {
                return;
            }

            controller.TrySetBonusPick(ResolveLocalSlot(), bonusSlot);
        }

        private void UpdateSlotButtons(int ownPick)
        {
            var locked = ownPick != BonusPickRules.NoneSlot;
            foreach (var (button, bonusSlot) in _slotButtons)
            {
                var selectable = BonusPickRules.IsValidSlot(bonusSlot);
                button.SetEnabled(selectable && !locked);
                button.EnableInClassList(PickedClass, ownPick == bonusSlot);
                button.EnableInClassList(LockedClass, !selectable || locked);
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

                if (!BonusPickRules.IsValidSlot(slot))
                {
                    button.SetEnabled(false);
                    button.AddToClassList(LockedClass);
                }

                grid.Add(button);
                _slotButtons.Add((button, slot));
            }
        }

        private void ShowTooltip(int bonusSlot, VisualElement anchor)
        {
            if (_tooltip == null || _tooltipLabel == null || anchor == null)
            {
                return;
            }

            var description = BonusPickRules.GetSlotDescription(bonusSlot);
            if (string.IsNullOrEmpty(description))
            {
                HideTooltip();
                return;
            }

            _hoveredSlot = bonusSlot;
            _tooltipLabel.text = description;
            _tooltip.RemoveFromClassList(TooltipHiddenClass);
            _tooltip.BringToFront();
            _tooltip.schedule.Execute(() => PositionTooltip(anchor));
        }

        private void HideTooltipIfSlot(int bonusSlot)
        {
            if (_hoveredSlot == bonusSlot)
            {
                HideTooltip();
            }
        }

        private void HideTooltip()
        {
            _hoveredSlot = BonusPickRules.NoneSlot;
            _tooltip?.AddToClassList(TooltipHiddenClass);
        }

        private void PositionTooltip(VisualElement anchor)
        {
            if (_tooltip == null || anchor == null)
            {
                return;
            }

            var buttonBound = anchor.worldBound;
            var width = _tooltip.resolvedStyle.width;
            var height = _tooltip.resolvedStyle.height;
            if (width <= 0f || float.IsNaN(width))
            {
                width = 220f;
            }

            if (height <= 0f || float.IsNaN(height))
            {
                height = 48f;
            }

            var panelWidth = _overlay != null ? _overlay.resolvedStyle.width : 0f;
            var topLeft = MatchCommandTooltipRules.GetTooltipTopLeft(
                buttonBound,
                new Vector2(width, height),
                panelWidth);
            _tooltip.style.left = topLeft.x;
            _tooltip.style.top = topLeft.y;
        }

        private static int ResolveLocalSlot()
        {
            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            return setup.LocalPlayerSlot;
        }

        private static string ResolveLocalRaceId()
        {
            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            var slot = setup.LocalPlayerSlot;
            if (setup.RaceIds != null && slot >= 0 && slot < setup.RaceIds.Count)
            {
                var raceId = setup.RaceIds[slot];
                if (!string.IsNullOrEmpty(raceId))
                {
                    return raceId;
                }
            }

            return GameIds.Races.Human;
        }

        private void Show()
        {
            _overlay?.RemoveFromClassList(HiddenClass);
        }

        private void Hide()
        {
            HideTooltip();
            _overlay?.AddToClassList(HiddenClass);
        }
    }
}
