using System;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Post-bonus-pick hero order overlay: a row of the three heroes (idle 3D previews,
    /// veteran variants baked when a matching bonus pick was made). The player reorders the
    /// cards by drag &amp; drop; the hero on position 1 unlocks for hire at main level 1, etc.
    /// Confirmed via <see cref="HeroOrderNetworkFacade"/> (ServerRpc on pure clients, local
    /// <see cref="MatchController.TrySetHeroOrder"/> else). Appears once the bonus window
    /// closes and stays until the order is confirmed.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HeroOrderPickController : MonoBehaviour
    {
        private const string HiddenClass = "hero-order--hidden";
        private const string CardClass = "hero-order__card";
        private const string CardDraggingClass = "hero-order__card--dragging";
        private const string CardSwapClass = "hero-order__card--swap";
        private const string PreviewClass = "hero-order__preview";
        private const string BadgeClass = "hero-order__badge";
        private const string VeteranHiddenClass = "hero-order__badge--veteran-hidden";
        private const string VeteranBadgeClass = "hero-order__badge--veteran";
        private const string NameClass = "hero-order__name";

        [SerializeField] private UIDocument _uiDocument;

        private MatchRuntime _matchRuntime;
        private HeroOrderPreviewRuntime _previewRuntime;
        private VisualElement _overlay;
        private VisualElement _row;
        private Button _confirmButton;

        private HeroOrderCard[] _cards = Array.Empty<HeroOrderCard>();
        private int _dragSourceIndex = -1;
        private int _dragTargetIndex = -1;
        private int _dragPointerId = -1;
        private bool _initialized;
        private bool _windowsResolved;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            // Ensure the hero order overlay draws above every other panel (hud, race/bonus
            // pick, etc.). Without an explicit sort order the default panel sits on the same
            // layer as the hud, so regen frames / timer circles could paint on top.
            if (_uiDocument != null)
            {
                _uiDocument.sortingOrder = 100;
            }

            ResolveElementReferences();

            if (!TryGetComponent(out _previewRuntime))
            {
                _previewRuntime = gameObject.AddComponent<HeroOrderPreviewRuntime>();
            }
        }

        private void ResolveElementReferences()
        {
            var root = _uiDocument.rootVisualElement;
            if (_confirmButton != null)
            {
                _confirmButton.clicked -= TryConfirm;
            }

            _overlay = root.Q<VisualElement>("HeroOrderOverlay");
            _row = root.Q<VisualElement>("HeroOrderRow");
            _confirmButton = root.Q<Button>("HeroOrderConfirm");
            if (_confirmButton != null)
            {
                _confirmButton.clicked += TryConfirm;
            }
        }

        private void OnDisable()
        {
            CancelDrag();
            HidePreview();
        }

        private void LateUpdate()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            if (_overlay == null)
            {
                ResolveElementReferences();
            }

            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null || !_matchRuntime.IsMatchStarted)
            {
                Hide();
                return;
            }

            var localSlot = ResolveLocalSlot();
            if (controller.Players == null || localSlot < 0 || localSlot >= controller.Players.Count)
            {
                Hide();
                return;
            }

            var useSnapshot = _matchRuntime.TickMode == MatchTickMode.Client;
            BonusPickHudRules.ResolveOverlay(
                useSnapshot: useSnapshot,
                snapshot: _matchRuntime.LastNetworkSnapshot,
                localSlot,
                controller.BonusPickDeadlineSeconds,
                controller.GetBonusPickSlot(localSlot),
                controller.GetBonusPickSlot(localSlot, panel: 1),
                controller.GetBonusPickOffer(localSlot),
                out var deadline,
                out var autoPick,
                out var pick2,
                out _);

            HeroOrderHudRules.ResolveHeroOrder(
                useSnapshot,
                _matchRuntime.LastNetworkSnapshot,
                localSlot,
                controller.Players[localSlot].HeroOrder,
                controller.Players[localSlot].HeroOrderConfirmed,
                out var order,
                out var confirmed);

            var bonusPickResolved = pick2 != BonusPickRules.NoneSlot;
            if (!HeroOrderHudRules.IsOrderWindowOpen(confirmed, bonusPickResolved))
            {
                Hide();
                return;
            }

            if (!_initialized)
            {
                Initialize(order, autoPick, pick2);
            }

            if (!_windowsResolved)
            {
                _windowsResolved = true;
                ShowPreview();
            }

            Show();
        }

        private void Initialize(int[] order, int pick1, int pick2)
        {
            if (_row == null)
            {
                return;
            }

            _initialized = true;
            _cards = Array.Empty<HeroOrderCard>();
            _row.Clear();

            if (!HeroOrderPickRules.IsValidOrder(order))
            {
                order = HeroOrderPickRules.DefaultOrder();
            }

            var raceId = ResolveLocalRaceId();
            _cards = new HeroOrderCard[order.Length];
            var previewTargets = new VisualElement[order.Length];
            for (var i = 0; i < order.Length; i++)
            {
                var card = BuildCard(order[i]);
                _cards[i] = card;
                _row.Add(card.Root);
                previewTargets[i] = card.Preview;

                var veteran = BonusKitRules.EffectiveBonusSlotForHero(raceId, pick1, pick2, card.HeroSlot) != 0;
                card.Veteran.EnableInClassList(VeteranHiddenClass, !veteran);
            }

            _previewRuntime.Configure(previewTargets, order, raceId, pick1, pick2);
            RefreshBadges();
        }

        private HeroOrderCard BuildCard(int heroSlot)
        {
            var root = new VisualElement { name = $"HeroOrderCard{heroSlot}" };
            root.AddToClassList(CardClass);
            root.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
            root.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
            root.RegisterCallback<PointerCancelEvent>(OnCardPointerCancel);

            var preview = new VisualElement { name = "HeroOrderPreview" };
            preview.AddToClassList(PreviewClass);
            root.Add(preview);

            var badge = new Label { name = "HeroOrderPosition", text = "1" };
            badge.AddToClassList(BadgeClass);
            root.Add(badge);

            var veteran = new Label { name = "HeroOrderVeteran", text = "ВЕТЕРАН" };
            veteran.AddToClassList(VeteranBadgeClass);
            veteran.AddToClassList(VeteranHiddenClass);
            root.Add(veteran);

            var name = new Label { name = "HeroOrderName", text = $"Герой {heroSlot}" };
            name.AddToClassList(NameClass);
            root.Add(name);

            return new HeroOrderCard(root, preview, badge, veteran, name, heroSlot);
        }

        private void OnCardPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _dragPointerId >= 0)
            {
                return;
            }

            if (!(evt.currentTarget is VisualElement source))
            {
                return;
            }

            var sourceIndex = IndexOfCard(source);
            if (sourceIndex < 0)
            {
                return;
            }

            _dragPointerId = evt.pointerId;
            _dragSourceIndex = sourceIndex;
            _dragTargetIndex = sourceIndex;
            _cards[sourceIndex].Root.CapturePointer(evt.pointerId);
            _cards[sourceIndex].Root.AddToClassList(CardDraggingClass);
            evt.StopPropagation();
        }

        private void OnCardPointerMove(PointerMoveEvent evt)
        {
            if (_dragPointerId < 0 || evt.pointerId != _dragPointerId)
            {
                return;
            }

            var targetIndex = IndexOfCardAt(evt.position);
            if (targetIndex >= 0 && targetIndex != _dragTargetIndex)
            {
                if (_dragTargetIndex >= 0 && _dragTargetIndex < _cards.Length)
                {
                    _cards[_dragTargetIndex].Root.RemoveFromClassList(CardSwapClass);
                }

                _dragTargetIndex = targetIndex;
                ApplyDragTarget();
                RefreshBadges();
            }
        }

        private void OnCardPointerUp(PointerUpEvent evt)
        {
            if (_dragPointerId < 0 || evt.pointerId != _dragPointerId)
            {
                return;
            }

            EndDrag(evt.currentTarget as VisualElement);
            evt.StopPropagation();
        }

        private void OnCardPointerCancel(PointerCancelEvent evt)
        {
            if (_dragPointerId < 0 || evt.pointerId != _dragPointerId)
            {
                return;
            }

            EndDrag(evt.currentTarget as VisualElement);
            evt.StopPropagation();
        }

        private void ApplyDragTarget()
        {
            if (_dragTargetIndex < 0 || _dragTargetIndex >= _cards.Length)
            {
                return;
            }

            var holder = _cards[_dragSourceIndex].Root;
            _row.Remove(holder);
            _row.Insert(_dragTargetIndex, holder);

            var reordered = new HeroOrderCard[_cards.Length];
            for (var i = 0; i < _row.childCount && i < reordered.Length; i++)
            {
                reordered[i] = FindCardByRoot(_row[i]);
            }

            for (var i = 0; i < reordered.Length; i++)
            {
                _cards[i] = reordered[i];
            }

            _dragSourceIndex = _dragTargetIndex;
            _cards[_dragTargetIndex].Root.AddToClassList(CardSwapClass);
        }

        private void EndDrag(VisualElement captured)
        {
            if (_dragPointerId >= 0 && captured != null)
            {
                captured.ReleasePointer(_dragPointerId);
            }

            if (_dragSourceIndex >= 0 && _dragSourceIndex < _cards.Length)
            {
                _cards[_dragSourceIndex].Root.RemoveFromClassList(CardDraggingClass);
            }

            if (_dragTargetIndex >= 0 && _dragTargetIndex < _cards.Length)
            {
                _cards[_dragTargetIndex].Root.RemoveFromClassList(CardSwapClass);
            }

            _dragPointerId = -1;
            _dragSourceIndex = -1;
            _dragTargetIndex = -1;
            RefreshBadges();
        }

        private void CancelDrag()
        {
            if (_dragPointerId < 0)
            {
                return;
            }

            foreach (var card in _cards)
            {
                card.Root.RemoveFromClassList(CardDraggingClass);
                card.Root.RemoveFromClassList(CardSwapClass);
            }

            _dragPointerId = -1;
            _dragSourceIndex = -1;
            _dragTargetIndex = -1;
        }

        private void RefreshBadges()
        {
            for (var i = 0; i < _cards.Length; i++)
            {
                _cards[i].Badge.text = (i + 1).ToString();
            }
        }

        private int[] BuildDraftOrder()
        {
            var order = new int[_cards.Length];
            for (var i = 0; i < _cards.Length; i++)
            {
                order[i] = _cards[i].HeroSlot;
            }

            return order;
        }

        private int IndexOfCard(VisualElement element)
        {
            for (var i = 0; i < _cards.Length; i++)
            {
                if (_cards[i].Root == element)
                {
                    return i;
                }
            }

            return -1;
        }

        private int IndexOfCardAt(Vector2 position)
        {
            for (var i = 0; i < _cards.Length; i++)
            {
                if (_cards[i].Root.worldBound.Contains(position))
                {
                    return i;
                }
            }

            return -1;
        }

        private HeroOrderCard FindCardByRoot(VisualElement root)
        {
            for (var i = 0; i < _cards.Length; i++)
            {
                if (_cards[i].Root == root)
                {
                    return _cards[i];
                }
            }

            return null;
        }

        private void TryConfirm()
        {
            var order = BuildDraftOrder();
            if (!HeroOrderPickRules.IsValidOrder(order))
            {
                order = HeroOrderPickRules.DefaultOrder();
            }

            if (HeroOrderNetworkFacade.TryRequestHeroOrder(order))
            {
                return;
            }

            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null)
            {
                return;
            }

            controller.TrySetHeroOrder(ResolveLocalSlot(), order);
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

        private void ShowPreview()
        {
            _previewRuntime?.SetVisible(true);
        }

        private void HidePreview()
        {
            _previewRuntime?.SetVisible(false);
        }

        private void Show()
        {
            _overlay?.RemoveFromClassList(HiddenClass);
        }

        private void Hide()
        {
            CancelDrag();
            if (_windowsResolved)
            {
                _windowsResolved = false;
                HidePreview();
            }

            _overlay?.AddToClassList(HiddenClass);
        }

        private sealed class HeroOrderCard
        {
            public readonly VisualElement Root;
            public readonly VisualElement Preview;
            public readonly Label Badge;
            public readonly Label Veteran;
            public readonly Label Name;
            public readonly int HeroSlot;

            public HeroOrderCard(
                VisualElement root,
                VisualElement preview,
                Label badge,
                Label veteran,
                Label name,
                int heroSlot)
            {
                Root = root;
                Preview = preview;
                Badge = badge;
                Veteran = veteran;
                Name = name;
                HeroSlot = heroSlot;
            }
        }
    }
}