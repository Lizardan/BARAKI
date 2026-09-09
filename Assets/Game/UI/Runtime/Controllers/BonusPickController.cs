using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Post-race-pick bonus overlay (PRE-001 / PRE-006) with two windows. Both windows appear
    /// together once the base-focus fly-in ends. Left: the auto-fate pick — one random bonus
    /// rolled for the player at that moment (read-only; the other 11 cells stay empty). Right:
    /// a random subset of <see cref="BonusPickRules.OfferSize"/> slots out of the remaining 11 —
    /// the player picks one (the other 6 cells stay empty). The overlay closes once the choice
    /// is made or the 60s deadline expires (the server fills the offer randomly). Both picked
    /// bonuses stack. Backdrop is picking-mode Ignore — the overlay never blocks pan camera /
    /// unit controls.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BonusPickController : MonoBehaviour
    {
        private const string HiddenClass = "bonus-pick--hidden";
        private const string PickedClass = "bonus-pick__slot--picked";
        private const string LockedClass = "bonus-pick__slot--locked";
        private const string PortraitClass = "bonus-pick__portrait";
        private const string EmptyCellClass = "bonus-pick__cell--empty";
        private const string TooltipHiddenClass = "bonus-pick-tooltip--hidden";

        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private UnitVisualCatalog _visualCatalog;

        private MatchRuntime _matchRuntime;
        private VisualElement _overlay;
        private Label _timerLabel;
        private VisualElement _tooltip;
        private Label _tooltipLabel;
        private VisualElement _autoGrid;
        private VisualElement _offerGrid;
        private int _hoveredSlot = BonusPickRules.NoneSlot;
        private int _builtAutoPick = BonusPickRules.NoneSlot;
        private int[] _builtOffer = System.Array.Empty<int>();

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
            _autoGrid = root.Q<VisualElement>("BonusPickAutoGrid");
            _offerGrid = root.Q<VisualElement>("BonusPickOfferGrid");
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
                controller.GetBonusPickSlot(localSlot, panel: 1),
                controller.GetBonusPickOffer(localSlot),
                out var deadline,
                out var autoPick,
                out var pick2,
                out var offer);

            if (!BonusPickHudRules.IsPickWindowOpen(deadline, pick2))
            {
                Hide();
                return;
            }

            _timerLabel.text = Mathf.CeilToInt(deadline).ToString();
            EnsureAutoGrid(autoPick);
            EnsureOfferGrid(offer);
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

        private void EnsureAutoGrid(int autoPick)
        {
            if (_autoGrid == null)
            {
                return;
            }

            if (_builtAutoPick == autoPick)
            {
                return;
            }

            _builtAutoPick = autoPick;
            _autoGrid.Clear();

            var raceId = ResolveLocalRaceId();
            foreach (var slot in BonusPickRules.DisplayOrderSlots)
            {
                if (slot == autoPick)
                {
                    var button = CreateSlotButton(slot, raceId, selectable: false);
                    button.AddToClassList(PickedClass);
                    _autoGrid.Add(button);
                    ApplyPortrait(button, slot, raceId);
                    RegisterTooltip(button, slot);
                }
                else
                {
                    _autoGrid.Add(CreateEmptyCell());
                }
            }
        }

        private void EnsureOfferGrid(int[] offer)
        {
            if (_offerGrid == null)
            {
                return;
            }

            if (offer is not { Length: > 0 })
            {
                return;
            }

            if (SameOffer(_builtOffer, offer))
            {
                return;
            }

            _builtOffer = offer;
            _offerGrid.Clear();

            var raceId = ResolveLocalRaceId();
            foreach (var slot in BonusPickRules.DisplayOrderSlots)
            {
                if (BonusPickRules.IsSlotInOffer(offer, slot))
                {
                    var clickedSlot = slot;
                    var button = CreateSlotButton(slot, raceId, selectable: true);
                    button.clicked += () => OnSlotClicked(clickedSlot);
                    _offerGrid.Add(button);
                    ApplyPortrait(button, slot, raceId);
                    RegisterTooltip(button, slot);
                }
                else
                {
                    _offerGrid.Add(CreateEmptyCell());
                }
            }
        }

        private Button CreateSlotButton(int slot, string raceId, bool selectable)
        {
            var button = new Button
            {
                name = $"BonusSlot{slot}",
                text = BonusPickRules.GetSlotDisplayName(slot, raceId),
            };
            button.AddToClassList("ui-btn");
            button.AddToClassList("ui-btn--square");
            button.AddToClassList("bonus-pick__slot");

            if (!selectable)
            {
                button.SetEnabled(false);
                button.AddToClassList(LockedClass);
            }

            return button;
        }

        private static VisualElement CreateEmptyCell()
        {
            var cell = new VisualElement { name = "BonusPickEmpty" };
            cell.AddToClassList("bonus-pick__cell");
            cell.AddToClassList(EmptyCellClass);
            return cell;
        }

        private static bool SameOffer(int[] a, int[] b)
        {
            if (a == b)
            {
                return true;
            }

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyPortrait(Button button, int slot, string raceId)
        {
            ResolveVisualCatalog();
            if (_visualCatalog == null || button == null)
            {
                return;
            }

            if (!BonusKitRules.IsBonusSlot(slot) && !BonusKitRules.IsChampionBonusSlot(slot))
            {
                return;
            }

            if (!_visualCatalog.TryGetBonusPortrait(raceId, slot, out var portrait) || portrait == null)
            {
                return;
            }

            var existing = button.Q<VisualElement>("BonusPortrait");
            if (existing == null)
            {
                existing = new VisualElement { name = "BonusPortrait" };
                existing.AddToClassList(PortraitClass);
                button.Add(existing);
            }

            existing.style.backgroundImage = new StyleBackground(portrait);
            button.text = string.Empty;
        }

        private void RegisterTooltip(Button button, int slot)
        {
            if (button == null)
            {
                return;
            }

            var anchor = button;
            button.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(slot, anchor));
            button.RegisterCallback<PointerLeaveEvent>(_ => HideTooltipIfSlot(slot));
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

            controller.TrySetBonusPick(ResolveLocalSlot(), bonusSlot);
        }

        private void ShowTooltip(int bonusSlot, VisualElement anchor)
        {
            if (_tooltip == null || _tooltipLabel == null || anchor == null)
            {
                return;
            }

            var raceId = ResolveLocalRaceId();
            var description = BonusPickRules.GetSlotDescription(bonusSlot, raceId);
            if (string.IsNullOrEmpty(description))
            {
                HideTooltip();
                return;
            }

            if (!BonusPickRules.IsSlotImplemented(bonusSlot, raceId))
            {
                description += "\n(в разработке)";
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